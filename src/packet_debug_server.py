#!/usr/bin/env python3
"""
Enhanced TCP server for debugging Minecraft protocol packets.
Listens on port 25565 and prints both raw hex and parsed packet data.
"""

import socket
import threading
import queue
import json
import os
from datetime import datetime
from dataclasses import dataclass
from typing import Dict, Optional, Tuple
from .minecraft_protocol import (
    PacketParser, ConnectionState,
    HandshakePacket, LoginStartPacket,
    ClientInformationPacket,
    SetPlayerPositionPacket,
    SetPlayerPositionAndRotationPacket,
    SetPlayerRotationPacket,
    KeepAlivePacket,
    PlayerActionPacket,
    ClickContainerPacket,
    UseItemOnPacket,
    SetHeldItemPacket,
    PacketBuilder, GameProfile
)
import uuid
import time
import threading
import math
import random
from .web_server import run_web_server
from .block_manager import BlockManager

def read_varint(data, offset=0):
    """Read a VarInt from the data starting at offset."""
    result = 0
    shift = 0
    pos = offset
    
    while pos < len(data):
        byte = data[pos]
        result |= (byte & 0x7F) << shift
        pos += 1
        
        if (byte & 0x80) == 0:
            break
            
        shift += 7
        if shift >= 32:
            raise ValueError("VarInt too long")
    
    return result, pos

def format_hex(data, max_bytes=64):
    """Format bytes as hex string."""
    if len(data) <= max_bytes:
        return ' '.join(f'{b:02x}' for b in data)
    else:
        hex_str = ' '.join(f'{b:02x}' for b in data[:max_bytes])
        return f"{hex_str} ... ({len(data) - max_bytes} more bytes)"


class ChunkManager:
    """Manages chunk loading and unloading based on player position."""
    
    def __init__(self, view_distance: int = 10):
        """
        Initialize chunk manager.
        
        Args:
            view_distance: Server view distance (chunks)
        """
        self.view_distance = view_distance
        # Loading radius: view_distance + buffer for neighbors
        # We add 2 extra chunks to ensure all visible chunks have neighbors
        self.loading_radius = view_distance + 2
    
    def world_to_chunk(self, world_x: float, world_z: float) -> tuple:
        """Convert world coordinates to chunk coordinates."""
        chunk_x = int(world_x) // 16
        chunk_z = int(world_z) // 16
        return chunk_x, chunk_z
    
    def get_chunks_in_range(self, center_chunk_x: int, center_chunk_z: int) -> list:
        """
        Get all chunks that should be loaded around a center chunk.
        
        Returns:
            List of (chunk_x, chunk_z) tuples
        """
        chunks = []
        radius = self.loading_radius
        for dx in range(-radius, radius + 1):
            for dz in range(-radius, radius + 1):
                chunk_x = center_chunk_x + dx
                chunk_z = center_chunk_z + dz
                chunks.append((chunk_x, chunk_z))
        return chunks
    
    def get_chunks_to_load(self, center_chunk_x: int, center_chunk_z: int, loaded_chunks: set) -> list:
        """
        Get chunks that need to be loaded.
        
        Args:
            center_chunk_x: Center chunk X coordinate
            center_chunk_z: Center chunk Z coordinate
            loaded_chunks: Set of (chunk_x, chunk_z) tuples that are already loaded
        
        Returns:
            List of (chunk_x, chunk_z) tuples that need to be loaded
        """
        all_chunks = self.get_chunks_in_range(center_chunk_x, center_chunk_z)
        return [chunk for chunk in all_chunks if chunk not in loaded_chunks]
    
    def get_chunks_to_unload(self, center_chunk_x: int, center_chunk_z: int, loaded_chunks: set) -> list:
        """
        Get chunks that are too far away and should be unloaded.
        
        Args:
            center_chunk_x: Center chunk X coordinate
            center_chunk_z: Center chunk Z coordinate
            loaded_chunks: Set of (chunk_x, chunk_z) tuples that are currently loaded
        
        Returns:
            List of (chunk_x, chunk_z) tuples that should be unloaded
        """
        # Keep chunks within loading radius + 1 (buffer)
        keep_radius = self.loading_radius + 1
        chunks_to_keep = set()
        for dx in range(-keep_radius, keep_radius + 1):
            for dz in range(-keep_radius, keep_radius + 1):
                chunk_x = center_chunk_x + dx
                chunk_z = center_chunk_z + dz
                chunks_to_keep.add((chunk_x, chunk_z))
        
        return [chunk for chunk in loaded_chunks if chunk not in chunks_to_keep]


class ChunkLoader:
    """Background thread for asynchronous chunk loading."""
    
    def __init__(self, client_socket: socket.socket, player, stop_event: threading.Event):
        """
        Initialize chunk loader.
        
        Args:
            client_socket: Socket to send chunks to (must be thread-safe)
            player: Player instance for tracking loaded chunks
            stop_event: Event to signal when to stop
        """
        self.client_socket = client_socket
        self.player = player
        self.stop_event = stop_event
        self.chunk_queue = queue.Queue()
        self.socket_lock = threading.Lock()  # Lock for thread-safe socket operations
        self.thread = None
    
    def start(self):
        """Start the chunk loader thread."""
        self.thread = threading.Thread(target=self._worker, daemon=True)
        self.thread.start()
    
    def queue_chunks(self, chunks: list, center_chunk: tuple = None):
        """
        Queue chunks to be loaded.
        
        Args:
            chunks: List of (chunk_x, chunk_z) tuples to load
            center_chunk: Optional (chunk_x, chunk_z) tuple for Set Center Chunk
        """
        self.chunk_queue.put(('load', chunks, center_chunk))
    
    def queue_unload(self, chunks: list):
        """
        Queue chunks to be unloaded.
        
        Args:
            chunks: List of (chunk_x, chunk_z) tuples to unload
        """
        self.chunk_queue.put(('unload', chunks, None))
    
    def _worker(self):
        """Background worker thread that loads chunks."""
        while not self.stop_event.is_set():
            try:
                # Wait for chunk loading request with timeout
                try:
                    action, chunks, center_chunk = self.chunk_queue.get(timeout=0.1)
                except queue.Empty:
                    continue
                
                if action == 'load':
                    self._load_chunks(chunks, center_chunk)
                elif action == 'unload':
                    self._unload_chunks(chunks)
                
                self.chunk_queue.task_done()
            except Exception as e:
                print(f"  │  ✗ Error in chunk loader: {e}")
                import traceback
                traceback.print_exc()
    
    def _load_chunks(self, chunks: list, center_chunk: tuple = None):
        """Load chunks and send them to the client."""
        if not chunks:
            return
        
        # Sort chunks by distance from center (closer first)
        if center_chunk:
            center_x, center_z = center_chunk
            chunks.sort(key=lambda c: abs(c[0] - center_x) + abs(c[1] - center_z))
        
        print(f"  │  → [Chunk Loader] Loading {len(chunks)} chunk(s) asynchronously...")
        
        chunks_sent = 0
        for chunk_x, chunk_z in chunks:
            if self.stop_event.is_set():
                break
            
            try:
                # Phase 1 & 2: Load chunk into BlockManager first, then generate packet from it
                block_manager = None
                if hasattr(self.player, 'world') and self.player.world:
                    # Load chunk into BlockManager first
                    self.player.world.load_chunk_blocks(chunk_x, chunk_z, ground_y=64)
                    block_manager = self.player.world.block_manager
                
                # Phase 3: Generate packet from BlockManager (required)
                chunk_data = PacketBuilder.build_chunk_data(
                    chunk_x=chunk_x,
                    chunk_z=chunk_z,
                    block_manager=block_manager
                )
                
                # Thread-safe socket send
                with self.socket_lock:
                    self.client_socket.sendall(chunk_data)
                
                self.player.mark_chunk_loaded(chunk_x, chunk_z)
                chunks_sent += 1
                
                if chunks_sent <= 10 or chunks_sent % 10 == 0:
                    print(f"  │  ✓ [Chunk Loader] Chunk ({chunk_x}, {chunk_z}) loaded ({len(chunk_data)} bytes)")
            except Exception as e:
                print(f"  │  ✗ [Chunk Loader] Error loading chunk ({chunk_x}, {chunk_z}): {e}")
        
        # Send Set Center Chunk after loading (if provided)
        if center_chunk and not self.stop_event.is_set():
            try:
                center_chunk_packet = PacketBuilder.build_set_center_chunk(
                    chunk_x=center_chunk[0],
                    chunk_z=center_chunk[1]
                )
                with self.socket_lock:
                    self.client_socket.sendall(center_chunk_packet)
                print(f"  │  ✓ [Chunk Loader] Set Center Chunk sent after loading chunks")
            except Exception as e:
                print(f"  │  ✗ [Chunk Loader] Error sending Set Center Chunk: {e}")
        
        print(f"  │  ✓ [Chunk Loader] Loaded {chunks_sent} chunk(s)")
    
    def _unload_chunks(self, chunks: list):
        """Unload chunks (just mark them as unloaded)."""
        for chunk_x, chunk_z in chunks:
            self.player.mark_chunk_unloaded(chunk_x, chunk_z)
        print(f"  │  ✓ [Chunk Loader] Unloaded {len(chunks)} chunk(s)")
    
    def wait_for_completion(self, timeout: float = None):
        """Wait for all queued chunks to be loaded."""
        self.chunk_queue.join()


@dataclass
class ItemEntity:
    """Represents a dropped item entity in the world."""
    entity_id: int
    uuid: uuid.UUID
    x: float
    y: float
    z: float
    item_id: int  # Item protocol ID
    velocity_x: float = 0.0
    velocity_y: float = 0.0
    velocity_z: float = 0.0
    count: int = 1  # Item count
    spawn_time: float = 0.0  # Timestamp for potential despawn logic
    last_update_time: float = 0.0  # Timestamp of last position update
    pickup_delay: float = 0.5  # Pickup delay in seconds (10 ticks = 0.5 seconds at 20 TPS)


class Player:
    """
    Represents a player in the world.
    Encapsulates all player-specific state including position, rotation, and inventory.
    """
    
    def __init__(self, player_uuid: uuid.UUID, view_distance: int = 10, world=None):
        """
        Initialize a player.
        
        Args:
            player_uuid: Unique identifier for the player
            view_distance: Server view distance for this player
            world: Reference to the World instance (for chunk loading and block storage)
        """
        self.uuid = player_uuid
        self.world = world  # Reference to world for storing block data
        
        # Position and rotation
        self.x = 0.0
        self.y = 64.0
        self.z = 0.0
        self.chunk_x = 0
        self.chunk_z = 0
        self.yaw = 0.0  # Horizontal rotation (degrees)
        self.pitch = 0.0  # Vertical rotation (degrees)
        
        # Inventory
        self.inventory: Dict[int, int] = {}  # Track inventory: item_id -> count (server-side tracking)
        self.inventory_slots: Dict[int, tuple] = {}  # Track inventory slots: slot_index -> (item_id, count)
        # Note: Valid item slots are 9-44 (main inventory 9-35, hotbar 36-44)
        # Slots 0-8 are crafting/armor slots, not for regular items
        self.inventory_state_id = 0  # State ID for container synchronization
        self.selected_hotbar_slot = 0  # Currently selected hotbar slot (0-8, maps to slots 36-44)
        self.cursor_item: Optional[Tuple[int, int]] = None  # Track cursor item: (item_id, count) or None if empty
        
        # Chunk management (per-player)
        self.loaded_chunks = set()  # Set of (chunk_x, chunk_z) tuples
        self.chunk_manager = ChunkManager(view_distance=view_distance)
        self.last_center_chunk = (0, 0)
        self.view_distance = view_distance
    
    def calculate_drop_velocity(self) -> Tuple[float, float, float]:
        """
        Calculate velocity for a dropped item based on player's look direction.
        
        Returns:
            Tuple of (velocity_x, velocity_y, velocity_z)
        """
        # Convert yaw and pitch to radians
        yaw_rad = math.radians(self.yaw)
        pitch_rad = math.radians(self.pitch)
        
        # Calculate forward direction vector
        # Minecraft: yaw 0 = south, 90 = west, 180 = north, 270 = east
        forward_x = -math.sin(yaw_rad) * math.cos(pitch_rad)
        forward_y = -math.sin(pitch_rad)
        forward_z = math.cos(yaw_rad) * math.cos(pitch_rad)
        
        # Base velocity magnitude for dropped items (similar to vanilla)
        base_velocity = 0.2
        
        # Add some random spread (small random offset)
        spread = 0.02
        velocity_x = forward_x * base_velocity + (random.random() - 0.5) * spread
        velocity_y = forward_y * base_velocity + 0.1 + (random.random() - 0.5) * spread  # Add slight upward bias
        velocity_z = forward_z * base_velocity + (random.random() - 0.5) * spread
        
        return (velocity_x, velocity_y, velocity_z)
    
    def update_position(self, x: float, y: float, z: float) -> tuple:
        """
        Update player position and check if chunk boundary was crossed.
        
        Args:
            x: World X coordinate
            y: World Y coordinate
            z: World Z coordinate
        
        Returns:
            (old_chunk, new_chunk) tuple, or None if no boundary crossed
        """
        self.x = x
        self.y = y
        self.z = z
        
        # Calculate new chunk coordinates
        new_chunk_x, new_chunk_z = self.chunk_manager.world_to_chunk(x, z)
        
        old_chunk = (self.chunk_x, self.chunk_z)
        new_chunk = (new_chunk_x, new_chunk_z)
        
        self.chunk_x = new_chunk_x
        self.chunk_z = new_chunk_z
        
        # Check if chunk boundary was crossed
        if old_chunk != new_chunk:
            return (old_chunk, new_chunk)
        return None
    
    def get_chunks_in_range(self) -> list:
        """Get all chunks that should be loaded around current position."""
        return self.chunk_manager.get_chunks_in_range(
            self.chunk_x, self.chunk_z
        )
    
    def get_chunks_to_load(self) -> list:
        """Get chunks that need to be loaded for current position."""
        return self.chunk_manager.get_chunks_to_load(
            self.chunk_x, self.chunk_z, self.loaded_chunks
        )
    
    def get_chunks_to_unload(self) -> list:
        """Get chunks that should be unloaded."""
        return self.chunk_manager.get_chunks_to_unload(
            self.chunk_x, self.chunk_z, self.loaded_chunks
        )
    
    def mark_chunk_loaded(self, chunk_x: int, chunk_z: int):
        """Mark a chunk as loaded."""
        self.loaded_chunks.add((chunk_x, chunk_z))
    
    def mark_chunk_unloaded(self, chunk_x: int, chunk_z: int):
        """Mark a chunk as unloaded."""
        self.loaded_chunks.discard((chunk_x, chunk_z))
    
    def check_item_pickups(self, item_entities: Dict[int, 'ItemEntity'], horizontal_range: float = 1.0, vertical_range_up: float = 1.62, vertical_range_down: float = 0.5) -> list:
        """
        Check for item entities within pickup range of the player.
        Uses a box-based detection similar to vanilla Minecraft.
        
        In vanilla, the pickup box extends 1 block horizontally and 0.5 blocks up/down.
        However, we spawn items at eye level (1.62 blocks up), so we need a larger vertical range.
        
        Args:
            item_entities: Dictionary of item entities to check
            horizontal_range: Horizontal pickup range in blocks (default 1.0)
            vertical_range_up: Vertical pickup range upward in blocks (default 1.62 to reach eye level)
            vertical_range_down: Vertical pickup range downward in blocks (default 0.5)
        
        Returns:
            List of ItemEntity objects within pickup range
        """
        items_in_range = []
        current_time = time.time()
        
        for entity_id, item_entity in item_entities.items():
            # Check pickup delay (items can't be picked up immediately after being dropped)
            # In vanilla Minecraft, items have a 10-tick (0.5 second) pickup delay
            time_since_spawn = current_time - item_entity.spawn_time
            if time_since_spawn < item_entity.pickup_delay:
                continue  # Item is still in pickup delay period
            
            # Calculate distances
            dx = abs(item_entity.x - self.x)
            dy = item_entity.y - self.y  # Vertical distance (positive = above player, negative = below)
            dz = abs(item_entity.z - self.z)
            
            # Check if within pickup box
            # Horizontal: inclusive (distance <= range)
            # Vertical: exclusive downward (dy > -vertical_range_down), inclusive upward (dy <= vertical_range_up)
            if dx <= horizontal_range and dz <= horizontal_range:
                if -vertical_range_down < dy <= vertical_range_up:
                    items_in_range.append(item_entity)
        
        return items_in_range
    
    def add_to_inventory(self, item_id: int, count: int):
        """Add items to player inventory (server-side tracking only)."""
        if item_id in self.inventory:
            self.inventory[item_id] += count
        else:
            self.inventory[item_id] = count
    
    def find_slot_for_item(self, item_id: int, count: int) -> int:
        """
        Find a slot to place an item. Tries to stack with existing items first,
        then finds the first empty slot in hotbar (36-44), then main inventory (9-35).
        
        Minecraft inventory slot layout:
        - 0: Crafting output
        - 1-4: Crafting input
        - 5-8: Armor slots
        - 9-35: Main inventory
        - 36-44: Hotbar
        - 45: Offhand
        
        Returns:
            Slot index (9-44 for main inventory + hotbar), or None if inventory is full
        """
        # Valid slots for items: 9-44 (main inventory + hotbar)
        valid_slots = list(range(9, 45))  # Slots 9-44
        
        # First, try to find an existing stack of the same item with space
        for slot_idx in valid_slots:
            if slot_idx in self.inventory_slots:
                existing_item_id, existing_count = self.inventory_slots[slot_idx]
                if existing_item_id == item_id and existing_count < 64:  # Stack size limit
                    # Can stack here
                    return slot_idx
        
        # If no stackable slot found, find first empty slot in hotbar (36-44) first
        for slot_idx in range(36, 45):  # Hotbar first
            if slot_idx not in self.inventory_slots:
                return slot_idx
        
        # If hotbar is full, use main inventory (9-35)
        for slot_idx in range(9, 36):  # Main inventory
            if slot_idx not in self.inventory_slots:
                return slot_idx
        
        # Inventory is full
        return None
    
    def update_slot(self, slot_idx: int, item_id: int, count: int):
        """
        Update an inventory slot.
        
        Args:
            slot_idx: Slot index (0-35)
            item_id: Item ID (0 for empty)
            count: Item count (0 for empty)
        """
        if count > 0 and item_id > 0:
            self.inventory_slots[slot_idx] = (item_id, count)
        else:
            self.inventory_slots.pop(slot_idx, None)


class World:
    """
    Encapsulates all world state including players, entities, and world logic.
    Represents the internal state of the game as the server sees it.
    """
    
    def __init__(self, view_distance: int = 10, use_terrain_generation: bool = False):
        """
        Initialize world state.
        
        Args:
            view_distance: Server view distance
            use_terrain_generation: If True, use terrain generation instead of flat world
        """
        # Player management
        self.players: Dict[uuid.UUID, Player] = {}  # Dictionary of player UUID -> Player instance
        
        # Entity management
        self.next_entity_id = 1000  # Start entity IDs at 1000 (player is usually 1)
        self.item_entities: Dict[int, ItemEntity] = {}  # Track all item entities: entity_id -> ItemEntity
        
        # BlockManager - single source of truth for block data
        # Phase 7: Migration complete - BlockManager is now the only block storage system
        self.block_manager = BlockManager()
        self.use_terrain_generation = use_terrain_generation
        
        # Entity collision cache - store last collision check results per entity
        # Key: entity_id -> { 'blocks_checked': set of (x,y,z), 'result': bool, 'position': (x,y,z), 'velocity': (vx,vy,vz), 'gravity_disabled': bool }
        self.entity_collision_cache: Dict[int, dict] = {}
        
        # Entity update thread - continuously updates entities at 20 TPS
        self.entity_update_stop_event = threading.Event()
        self.entity_update_pause_event = threading.Event()  # Pause automatic updates
        self.entity_update_pause_event.set()  # Start unpaused
        self.entity_update_thread = threading.Thread(target=self._entity_update_worker, daemon=True)
        self.entity_update_thread.start()
    
    def add_player(self, player: Player):
        """Add a player to the world."""
        self.players[player.uuid] = player
    
    def remove_player(self, player_uuid: uuid.UUID):
        """Remove a player from the world."""
        self.players.pop(player_uuid, None)
    
    def get_player(self, player_uuid: uuid.UUID) -> Optional[Player]:
        """Get a player by UUID."""
        return self.players.get(player_uuid)
    
    def get_all_players(self) -> list:
        """Get all players in the world."""
        return list(self.players.values())
    
    def update_item_entities(self, delta_time: float = 0.05):
        """
        Update item entity positions based on velocity and gravity.
        Should be called periodically (e.g., every tick).
        Uses fixed delta_time for consistent tick-based physics at 20 TPS.
        
        Args:
            delta_time: Time step in seconds (default 0.05 = 1 tick at 20 TPS)
        """
        GRAVITY = -0.04  # Minecraft gravity per tick (blocks per tick^2)
        DRAG = 0.98  # Air resistance factor per tick
        
        current_time = time.time()
        
        for entity_id, item_entity in list(self.item_entities.items()):
            # Initialize last_update_time if this is the first update
            if item_entity.last_update_time == 0.0:
                item_entity.last_update_time = current_time
            
            # Use fixed delta_time for consistent tick-based physics
            # This ensures entities update at exactly 20 TPS
            step_delta = delta_time
            
            # Check if gravity should be disabled for this entity (frozen due to collision)
            cache = self.entity_collision_cache.get(entity_id)
            gravity_disabled = cache is not None and cache.get('gravity_disabled', False)
            
            # Update velocity (apply gravity and drag)
            # Only apply gravity if not disabled (entity is not frozen)
            if not gravity_disabled:
                item_entity.velocity_y += GRAVITY  # Gravity is per tick, no scaling needed
            item_entity.velocity_x *= DRAG
            item_entity.velocity_y *= DRAG
            item_entity.velocity_z *= DRAG
            
            # Check for horizontal collisions before moving (prevent moving into blocks)
            entity_x_floor = int(item_entity.x)
            entity_y_floor = int(item_entity.y)
            entity_z_floor = int(item_entity.z)
            
            # Ensure chunk is loaded (lazy loading for collision detection)
            chunk_x = entity_x_floor // 16
            chunk_z = entity_z_floor // 16
            # Phase 2: Use BlockManager to check if chunk is loaded
            chunk_loaded = self.block_manager.is_chunk_loaded(chunk_x, chunk_z)
            if not chunk_loaded:
                # Chunk not loaded yet, load it now
                # load_chunk_blocks() wrapper will delegate to block_manager AND update old block_data
                self.load_chunk_blocks(chunk_x, chunk_z, ground_y=64)
            
            # Check horizontal movement in X direction
            if item_entity.velocity_x != 0:
                next_x = item_entity.x + item_entity.velocity_x * 1.0
                next_x_floor = int(next_x)
                # Check if entity would move into a block horizontally
                if next_x_floor != entity_x_floor:
                    check_x = next_x_floor
                    # Check block at entity's Y level and one block above (entity might be pushed up)
                    if self.is_block_solid(check_x, entity_y_floor, entity_z_floor) or \
                       self.is_block_solid(check_x, entity_y_floor + 1, entity_z_floor):
                        # Blocked - stop horizontal movement
                        item_entity.velocity_x = 0.0
                        # Push entity back to the edge of the current block to prevent getting stuck
                        if item_entity.velocity_x > 0:  # Was moving positive
                            item_entity.x = float(entity_x_floor + 1) - 0.01
                        else:  # Was moving negative
                            item_entity.x = float(entity_x_floor) + 0.01
            
            # Check horizontal movement in Z direction
            if item_entity.velocity_z != 0:
                next_z = item_entity.z + item_entity.velocity_z * 1.0
                next_z_floor = int(next_z)
                # Check if entity would move into a block horizontally
                if next_z_floor != entity_z_floor:
                    check_z = next_z_floor
                    # Check block at entity's Y level and one block above (entity might be pushed up)
                    if self.is_block_solid(entity_x_floor, entity_y_floor, check_z) or \
                       self.is_block_solid(entity_x_floor, entity_y_floor + 1, check_z):
                        # Blocked - stop horizontal movement
                        item_entity.velocity_z = 0.0
                        # Push entity back to the edge of the current block to prevent getting stuck
                        if item_entity.velocity_z > 0:  # Was moving positive
                            item_entity.z = float(entity_z_floor + 1) - 0.01
                        else:  # Was moving negative
                            item_entity.z = float(entity_z_floor) + 0.01
            
            # Store position before movement for collision detection
            old_x = item_entity.x
            old_y = item_entity.y
            old_z = item_entity.z
            
            # Calculate where entity would be after movement
            next_x = item_entity.x + item_entity.velocity_x * 1.0
            next_y = item_entity.y + item_entity.velocity_y * 1.0
            next_z = item_entity.z + item_entity.velocity_z * 1.0
            
            # Check if we can use cached collision result
            current_position = (old_x, old_y, old_z)
            current_velocity = (item_entity.velocity_x, item_entity.velocity_y, item_entity.velocity_z)
            cache = self.entity_collision_cache.get(entity_id)
            
            needs_recheck = True
            intersecting_solid_block = False
            
            if cache is not None:
                # Check if entity has moved
                position_changed = (abs(cache['position'][0] - old_x) > 1e-6 or
                                  abs(cache['position'][1] - old_y) > 1e-6 or
                                  abs(cache['position'][2] - old_z) > 1e-6)
                velocity_changed = (abs(cache['velocity'][0] - item_entity.velocity_x) > 1e-6 or
                                  abs(cache['velocity'][1] - item_entity.velocity_y) > 1e-6 or
                                  abs(cache['velocity'][2] - item_entity.velocity_z) > 1e-6)
                
                # Check if any of the previously checked blocks have been updated
                # Phase 4: Use BlockManager's updated_blocks
                blocks_updated = False
                if cache['blocks_checked']:
                    updated_blocks = self.block_manager.get_updated_blocks()
                    blocks_updated = bool(cache['blocks_checked'] & updated_blocks)
                
                # If blocks were updated, re-enable gravity (entity might be able to fall now)
                if blocks_updated and cache.get('gravity_disabled', False):
                    cache['gravity_disabled'] = False
                
                # Can use cache if entity hasn't moved, velocity hasn't changed, and no blocks updated
                if not position_changed and not velocity_changed and not blocks_updated:
                    needs_recheck = False
                    intersecting_solid_block = cache['result']
            
            if needs_recheck:
                # Check if the line segment from current position to next position intersects any solid block
                # Use the optimized line traversal method
                line_start = (old_x, old_y, old_z)
                line_end = (next_x, next_y, next_z)
                intersecting_solid_block, debug_info = self.check_line_intersects_solid_block(
                    line_start, line_end, return_debug=True
                )
            
            if intersecting_solid_block:
                # Entity would be intersecting a solid block - stop all movement
                # (Just detecting, not pushing out yet)
                item_entity.velocity_x = 0.0
                item_entity.velocity_y = 0.0
                item_entity.velocity_z = 0.0
                # Don't update position - keep it at old position
                
                # Entity is now at rest - cache the collision result and disable gravity
                if needs_recheck and debug_info:
                    blocks_checked = set()
                    if debug_info.get('blocks_checked'):
                        blocks_checked = {tuple(block['pos']) for block in debug_info['blocks_checked']}
                    
                    self.entity_collision_cache[entity_id] = {
                        'blocks_checked': blocks_checked,
                        'result': intersecting_solid_block,
                        'position': current_position,
                        'velocity': (0.0, 0.0, 0.0),  # Entity is at rest
                        'gravity_disabled': True  # Disable gravity while frozen
                    }
            else:
                # No collision - update position
                item_entity.x = next_x
                item_entity.y = next_y
                item_entity.z = next_z
                
                # Entity is moving - clear cache (don't cache while moving)
                self.entity_collision_cache.pop(entity_id, None)
            
            item_entity.last_update_time = current_time
        
        # Clear updated blocks set after processing all entities (they've all been checked)
        # Phase 4: Use BlockManager's updated_blocks
        self.block_manager.clear_updated_blocks()
    
    def get_block_at(self, x: int, y: int, z: int) -> int:
        """
        Get the block ID at the given world coordinates.
        Returns 0 (air) if the block is not loaded or out of bounds.
        
        Phase 3: Now delegates to BlockManager.
        Keeping as wrapper for backward compatibility during migration.
        
        Args:
            x: World X coordinate
            y: World Y coordinate
            z: World Z coordinate
            
        Returns:
            Block ID (0 = air, 10 = dirt, 9 = grass_block, etc.)
        """
        # Delegate to BlockManager
        return self.block_manager.get_block(x, y, z)
    
    def check_line_intersects_solid_block(self, line_start: tuple, line_end: tuple, return_debug: bool = False) -> tuple:
        """
        Check if a line segment intersects any solid block.
        
        Args:
            line_start: (x, y, z) tuple for start of line segment
            line_end: (x, y, z) tuple for end of line segment
            return_debug: If True, return (intersects, debug_info) tuple instead of just bool
            
        Returns:
            If return_debug is False: True if the line segment intersects any solid block, False otherwise
            If return_debug is True: (intersects: bool, debug_info: dict)
        """
        old_x, old_y, old_z = line_start
        next_x, next_y, next_z = line_end
        
        # Find the range of blocks the line segment passes through
        # Use math.floor to correctly handle negative coordinates
        # A block at integer coordinate (bx, by, bz) occupies [bx, bx+1) x [by, by+1) x [bz, bz+1)
        # So we need to floor the coordinates to find which block contains a point
        check_x_min = int(math.floor(min(old_x, next_x)))
        check_x_max = int(math.floor(max(old_x, next_x))) + 1
        check_y_min = int(math.floor(min(old_y, next_y)))
        check_y_max = int(math.floor(max(old_y, next_y))) + 1
        check_z_min = int(math.floor(min(old_z, next_z)))
        check_z_max = int(math.floor(max(old_z, next_z))) + 1
        
        # Calculate which block contains the start position (for debugging)
        start_block_x = int(math.floor(old_x))
        start_block_y = int(math.floor(old_y))
        start_block_z = int(math.floor(old_z))
        
        debug_info = {
            'line_start': line_start,
            'line_end': line_end,
            'start_block': (start_block_x, start_block_y, start_block_z),  # Block containing start position
            'check_range': {
                'x': [check_x_min, check_x_max],
                'y': [check_y_min, check_y_max],
                'z': [check_z_min, check_z_max]
            },
            'blocks_checked': []
        }
        
        # Helper function to check if line segment intersects AABB
        def line_segment_intersects_aabb(line_start, line_end, box_min, box_max):
            """Check if line segment from line_start to line_end intersects AABB [box_min, box_max]"""
            # Use slab method for line-AABB intersection
            dir_x = line_end[0] - line_start[0]
            dir_y = line_end[1] - line_start[1]
            dir_z = line_end[2] - line_start[2]
            
            # If line has zero length, check if point is inside box
            if abs(dir_x) < 1e-9 and abs(dir_y) < 1e-9 and abs(dir_z) < 1e-9:
                return (box_min[0] <= line_start[0] < box_max[0] and
                        box_min[1] <= line_start[1] < box_max[1] and
                        box_min[2] <= line_start[2] < box_max[2])
            
            # Calculate intersection with each slab
            t_min = 0.0
            t_max = 1.0
            
            # X axis
            if abs(dir_x) < 1e-9:
                if line_start[0] < box_min[0] or line_start[0] >= box_max[0]:
                    return False
            else:
                inv_dir = 1.0 / dir_x
                t1 = (box_min[0] - line_start[0]) * inv_dir
                t2 = (box_max[0] - line_start[0]) * inv_dir
                if t1 > t2:
                    t1, t2 = t2, t1
                t_min = max(t_min, t1)
                t_max = min(t_max, t2)
                if t_min > t_max:
                    return False
            
            # Y axis
            if abs(dir_y) < 1e-9:
                if line_start[1] < box_min[1] or line_start[1] >= box_max[1]:
                    return False
            else:
                inv_dir = 1.0 / dir_y
                t1 = (box_min[1] - line_start[1]) * inv_dir
                t2 = (box_max[1] - line_start[1]) * inv_dir
                if t1 > t2:
                    t1, t2 = t2, t1
                t_min = max(t_min, t1)
                t_max = min(t_max, t2)
                if t_min > t_max:
                    return False
            
            # Z axis
            if abs(dir_z) < 1e-9:
                if line_start[2] < box_min[2] or line_start[2] >= box_max[2]:
                    return False
            else:
                inv_dir = 1.0 / dir_z
                t1 = (box_min[2] - line_start[2]) * inv_dir
                t2 = (box_max[2] - line_start[2]) * inv_dir
                if t1 > t2:
                    t1, t2 = t2, t1
                t_min = max(t_min, t1)
                t_max = min(t_max, t2)
                if t_min > t_max:
                    return False
            
            return True
        
        # Use 3D DDA (Digital Differential Analyzer) to traverse only blocks that the line passes through
        # This is more efficient than checking all blocks in a bounding box
        dx = next_x - old_x
        dy = next_y - old_y
        dz = next_z - old_z
        
        # Calculate step direction and distances
        step_x = 1 if dx >= 0 else -1
        step_y = 1 if dy >= 0 else -1
        step_z = 1 if dz >= 0 else -1
        
        # Current block coordinates
        current_x = int(math.floor(old_x))
        current_y = int(math.floor(old_y))
        current_z = int(math.floor(old_z))
        
        # End block coordinates
        end_x = int(math.floor(next_x))
        end_y = int(math.floor(next_y))
        end_z = int(math.floor(next_z))
        
        # Calculate delta distances to next block boundary
        if dx != 0:
            delta_x = abs(1.0 / dx)
            next_x_boundary = (current_x + (1 if step_x > 0 else 0) - old_x) / dx if dx != 0 else float('inf')
        else:
            delta_x = float('inf')
            next_x_boundary = float('inf')
            
        if dy != 0:
            delta_y = abs(1.0 / dy)
            next_y_boundary = (current_y + (1 if step_y > 0 else 0) - old_y) / dy if dy != 0 else float('inf')
        else:
            delta_y = float('inf')
            next_y_boundary = float('inf')
            
        if dz != 0:
            delta_z = abs(1.0 / dz)
            next_z_boundary = (current_z + (1 if step_z > 0 else 0) - old_z) / dz if dz != 0 else float('inf')
        else:
            delta_z = float('inf')
            next_z_boundary = float('inf')
        
        # Traverse the line, checking only blocks it passes through
        max_steps = abs(end_x - current_x) + abs(end_y - current_y) + abs(end_z - current_z) + 1
        steps = 0
        
        while steps < max_steps:
            # Check current block
            check_x = current_x
            check_y = current_y
            check_z = current_z
            
            is_solid = self.is_block_solid(check_x, check_y, check_z)
            block_id = self.get_block_at(check_x, check_y, check_z)
            
            block_info = {
                'pos': (check_x, check_y, check_z),
                'block_id': block_id,
                'is_solid': is_solid,
                'intersects': False
            }
            
            if is_solid:
                # Block is solid - check if line segment actually intersects it
                block_min = (float(check_x), float(check_y), float(check_z))
                block_max = (float(check_x + 1), float(check_y + 1), float(check_z + 1))
                
                intersects = line_segment_intersects_aabb(line_start, line_end, block_min, block_max)
                block_info['intersects'] = intersects
                
                if return_debug:
                    debug_info['blocks_checked'].append(block_info)
                
                if intersects:
                    if return_debug:
                        return True, debug_info
                    return True
            else:
                if return_debug:
                    debug_info['blocks_checked'].append(block_info)
            
            # Move to next block along the line
            if next_x_boundary < next_y_boundary and next_x_boundary < next_z_boundary:
                current_x += step_x
                next_x_boundary += delta_x
            elif next_y_boundary < next_z_boundary:
                current_y += step_y
                next_y_boundary += delta_y
            else:
                current_z += step_z
                next_z_boundary += delta_z
            
            # Check if we've reached the end
            if (step_x > 0 and current_x > end_x) or (step_x < 0 and current_x < end_x):
                break
            if (step_y > 0 and current_y > end_y) or (step_y < 0 and current_y < end_y):
                break
            if (step_z > 0 and current_z > end_z) or (step_z < 0 and current_z < end_z):
                break
            
            steps += 1
        
        if return_debug:
            return False, debug_info
        return False
    
    def is_block_solid(self, x: int, y: int, z: int) -> bool:
        """
        Check if a block at the given coordinates is solid (not air).
        
        Phase 5: Now directly delegates to BlockManager.
        Keeping as wrapper for backward compatibility during migration.
        
        Args:
            x: World X coordinate
            y: World Y coordinate
            z: World Z coordinate
            
        Returns:
            True if block is solid, False if air or not loaded
        """
        # Delegate directly to BlockManager
        return self.block_manager.is_block_solid(x, y, z)  # 0 = air, anything else is solid
    
    def load_chunk_blocks(self, chunk_x: int, chunk_z: int, ground_y: int = 64, use_terrain: Optional[bool] = None):
        """
        Generate and store block data for all sections in a chunk.
        This should be called when a chunk is loaded.
        
        Phase 7: Now fully delegates to BlockManager.
        Keeping as wrapper for backward compatibility.
        
        Args:
            chunk_x: Chunk X coordinate
            chunk_z: Chunk Z coordinate
            ground_y: Y coordinate of ground level (default 64)
            use_terrain: If True, use terrain generation instead of flat world.
                        If None, uses self.use_terrain_generation (set in __init__)
        """
        # Use instance setting if not explicitly provided
        if use_terrain is None:
            use_terrain = getattr(self, 'use_terrain_generation', False)
        
        # Delegate to BlockManager
        self.block_manager.load_chunk(chunk_x, chunk_z, ground_y, flat_world=not use_terrain, use_terrain=use_terrain)
    
    def set_block(self, x: int, y: int, z: int, block_id: int):
        """
        Set a block at the given world coordinates.
        Updates the internal block data for collision detection.
        
        Phase 7: Now fully delegates to BlockManager.
        Keeping as wrapper for backward compatibility.
        
        Args:
            x: World X coordinate
            y: World Y coordinate
            z: World Z coordinate
            block_id: Block ID to set (0 = air, 10 = dirt, 9 = grass_block, etc.)
        """
        # Delegate to BlockManager
        self.block_manager.set_block(x, y, z, block_id)
    
    def _entity_update_worker(self):
        """
        Background worker thread that continuously updates entities at 20 TPS (every 0.05 seconds).
        Can be paused for step-through debugging.
        """
        TICK_INTERVAL = 0.05  # 20 TPS = 1 tick per 0.05 seconds
        
        while not self.entity_update_stop_event.is_set():
            # Wait for pause event to be set (unpaused) or stop event
            self.entity_update_pause_event.wait()
            
            if self.entity_update_stop_event.is_set():
                break
            
            start_time = time.time()
            
            # Update all entities with fixed tick interval
            self.update_item_entities(delta_time=TICK_INTERVAL)
            
            # Sleep to maintain 20 TPS
            elapsed = time.time() - start_time
            sleep_time = max(0, TICK_INTERVAL - elapsed)
            if sleep_time > 0:
                time.sleep(sleep_time)
    
    def pause_entity_updates(self):
        """Pause automatic entity updates (for step-through debugging)."""
        self.entity_update_pause_event.clear()
    
    def resume_entity_updates(self):
        """Resume automatic entity updates."""
        self.entity_update_pause_event.set()
    
    def step_entity_tick(self):
        """Manually step forward one tick of entity updates."""
        import time
        current_time = time.time()
        self.update_item_entities(delta_time=0.05)
    
    def remove_item_entity(self, entity_id: int):
        """Remove an item entity from tracking."""
        self.item_entities.pop(entity_id, None)
        # Clean up collision cache
        self.entity_collision_cache.pop(entity_id, None)


def get_entity_type_id(entity_name: str) -> int:
    """
    Get the protocol ID for an entity type from the generated registries.json file.
    
    Args:
        entity_name: Entity type name (e.g., 'minecraft:item', 'minecraft:item_display')
    
    Returns:
        Protocol ID for the entity type, or None if not found
    """
    import json
    import os
    
    registries_file = os.path.join(os.path.dirname(os.path.dirname(__file__)), 'extracted_data', 'registries.json')
    
    # Cache for entity type IDs
    if not hasattr(get_entity_type_id, '_entity_type_cache'):
        get_entity_type_id._entity_type_cache = {}
        
        if os.path.exists(registries_file):
            try:
                with open(registries_file, 'r') as f:
                    registries_data = json.load(f)
                
                if 'minecraft:entity_type' in registries_data:
                    entity_registry = registries_data['minecraft:entity_type']
                    if 'entries' in entity_registry:
                        entries = entity_registry['entries']
                        for entity_type_name, entity_data in entries.items():
                            protocol_id = entity_data.get('protocol_id')
                            if protocol_id is not None:
                                get_entity_type_id._entity_type_cache[entity_type_name] = protocol_id
            except Exception as e:
                print(f"  │  ⚠ Warning: Could not load entity type registry: {e}")
    
    return get_entity_type_id._entity_type_cache.get(entity_name)


def load_loot_tables():
    """
    Load block loot tables from extracted_data/loot_table_mappings.json.
    Returns a dictionary mapping block names to item names.
    Thread-safe: if loading is in progress, returns empty dict to avoid blocking.
    """
    import json
    
    script_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    loot_mappings_file = os.path.join(script_dir, 'extracted_data', 'loot_table_mappings.json')
    
    # Initialize cache and loading lock if not exists
    if not hasattr(load_loot_tables, '_loot_table_cache'):
        load_loot_tables._loot_table_cache = {}
        load_loot_tables._loading_lock = threading.Lock()
        load_loot_tables._is_loading = False
    
    # If cache is already populated, return it immediately
    if load_loot_tables._loot_table_cache:
        return load_loot_tables._loot_table_cache
    
    # If loading is in progress, return empty dict to avoid blocking
    if load_loot_tables._is_loading:
        return {}
    
    # Try to acquire lock and load
    if not load_loot_tables._loading_lock.acquire(blocking=False):
        # Another thread is loading, return empty dict
        return {}
    
    try:
        load_loot_tables._is_loading = True
        
        if not os.path.exists(loot_mappings_file):
            print(f"  │  ⚠ Warning: Loot table mappings not found at {loot_mappings_file}")
            print(f"  │  Run src/extract_server_data.py to generate extracted data")
        else:
            try:
                with open(loot_mappings_file, 'r') as f:
                    load_loot_tables._loot_table_cache = json.load(f)
                print(f"  │  ✓ Loaded {len(load_loot_tables._loot_table_cache)} loot table mappings")
            except Exception as e:
                print(f"  │  ⚠ Warning: Could not load loot tables: {e}")
    finally:
        load_loot_tables._is_loading = False
        load_loot_tables._loading_lock.release()
    
    return load_loot_tables._loot_table_cache


def get_block_state_id_from_item_id(item_id: int) -> Optional[int]:
    """
    Get the block state ID from an item ID.
    Maps item registry entries to block state IDs using blocks.json.
    
    Args:
        item_id: Item protocol ID
        
    Returns:
        Block state ID, or None if not found or not a block item
    """
    import json
    import os
    
    registries_file = os.path.join(os.path.dirname(os.path.dirname(__file__)), 'extracted_data', 'registries.json')
    blocks_file = os.path.join(os.path.dirname(os.path.dirname(__file__)), 'extracted_data', 'blocks.json')
    
    # Cache for item ID -> block state ID mapping
    if not hasattr(get_block_state_id_from_item_id, '_item_to_block_cache'):
        get_block_state_id_from_item_id._item_to_block_cache = {}
        
        if os.path.exists(registries_file) and os.path.exists(blocks_file):
            try:
                # Load item registry
                with open(registries_file, 'r') as f:
                    registries_data = json.load(f)
                
                # Load blocks registry
                with open(blocks_file, 'r') as f:
                    blocks_data = json.load(f)
                
                # Build mapping: item ID -> block name -> block state ID
                if 'minecraft:item' in registries_data:
                    item_registry = registries_data['minecraft:item']
                    if 'entries' in item_registry:
                        entries = item_registry['entries']
                        for item_name, item_data in entries.items():
                            protocol_id = item_data.get('protocol_id')
                            if protocol_id is not None and item_name in blocks_data:
                                # This item corresponds to a block
                                block_info = blocks_data[item_name]
                                if 'states' in block_info and len(block_info['states']) > 0:
                                    # Use the first/default state
                                    block_state_id = block_info['states'][0].get('id')
                                    if block_state_id is not None:
                                        get_block_state_id_from_item_id._item_to_block_cache[protocol_id] = block_state_id
            except Exception as e:
                print(f"  │  ⚠ Warning: Could not load item/block registry for mapping: {e}")
    
    return get_block_state_id_from_item_id._item_to_block_cache.get(item_id)


def get_block_name_from_state_id(block_state_id: int) -> Optional[str]:
    """
    Get the block name (identifier) from a block state ID.
    Loads the mapping from blocks.json.
    
    Args:
        block_state_id: Block state ID
        
    Returns:
        Block identifier (e.g., 'minecraft:grass_block', 'minecraft:dirt'), or None if not found
    """
    import json
    import os
    
    registries_file = os.path.join(os.path.dirname(os.path.dirname(__file__)), 'extracted_data', 'blocks.json')
    
    # Cache for block state ID -> block name mapping
    if not hasattr(get_block_name_from_state_id, '_block_name_cache'):
        get_block_name_from_state_id._block_name_cache = {}
        
        if os.path.exists(registries_file):
            try:
                with open(registries_file, 'r') as f:
                    blocks_data = json.load(f)
                
                # Build reverse mapping: block state ID -> block name
                for block_name, block_info in blocks_data.items():
                    if 'states' in block_info:
                        for state in block_info['states']:
                            state_id = state.get('id')
                            if state_id is not None:
                                get_block_name_from_state_id._block_name_cache[state_id] = block_name
            except Exception as e:
                print(f"  │  ⚠ Warning: Could not load block registry: {e}")
    
    return get_block_name_from_state_id._block_name_cache.get(block_state_id)


def get_item_for_block(block_name: str) -> str:
    """
    Get the item name that should drop from a block using loot tables.
    
    Args:
        block_name: Block identifier (e.g., 'minecraft:grass_block', 'minecraft:dirt')
        
    Returns:
        Item identifier (e.g., 'minecraft:dirt', 'minecraft:cobblestone'), or None if no drop
    """
    loot_tables = load_loot_tables()
    return loot_tables.get(block_name)


def get_item_id_from_name(item_name: str) -> int:
    """
    Get the protocol ID for an item from its name.
    
    Args:
        item_name: Item identifier (e.g., 'minecraft:dirt', 'minecraft:cobblestone')
    
    Returns:
        Protocol ID for the item, or None if not found
    """
    import json
    import os
    
    registries_file = os.path.join(os.path.dirname(os.path.dirname(__file__)), 'extracted_data', 'registries.json')
    
    # Cache for item IDs
    if not hasattr(get_item_id_from_name, '_item_id_cache'):
        get_item_id_from_name._item_id_cache = {}
        
        if os.path.exists(registries_file):
            try:
                with open(registries_file, 'r') as f:
                    registries_data = json.load(f)
                
                if 'minecraft:item' in registries_data:
                    item_registry = registries_data['minecraft:item']
                    if 'entries' in item_registry:
                        entries = item_registry['entries']
                        for item_name_key, item_data in entries.items():
                            protocol_id = item_data.get('protocol_id')
                            if protocol_id is not None:
                                get_item_id_from_name._item_id_cache[item_name_key] = protocol_id
            except Exception as e:
                print(f"  │  ⚠ Warning: Could not load item registry: {e}")
    
    return get_item_id_from_name._item_id_cache.get(item_name)


def get_item_id_for_block(block_state_id: int) -> int:
    """
    Map block state ID to item ID.
    This is a simplified mapping - in reality, block states and items have separate registries.
    We load item IDs from the generated registries.json file.
    """
    if block_state_id == 0:  # Air
        return None  # No item drop
    
    # Load item registry from generated reports
    import json
    import os
    
    registries_file = os.path.join(os.path.dirname(os.path.dirname(__file__)), 'extracted_data', 'registries.json')
    
    # Cache for item IDs
    if not hasattr(get_item_id_for_block, '_item_id_cache'):
        get_item_id_for_block._item_id_cache = {}
        get_item_id_for_block._block_to_item_map = {}
        
        if os.path.exists(registries_file):
            try:
                with open(registries_file, 'r') as f:
                    registries_data = json.load(f)
                
                if 'minecraft:item' in registries_data:
                    item_registry = registries_data['minecraft:item']
                    if 'entries' in item_registry:
                        entries = item_registry['entries']
                        for item_name, item_data in entries.items():
                            protocol_id = item_data.get('protocol_id')
                            if protocol_id is not None:
                                get_item_id_for_block._item_id_cache[item_name] = protocol_id
                                
                                # Create a simple mapping: block name -> item name
                                # For blocks, the item name is usually the same as the block name
                                if item_name.startswith('minecraft:'):
                                    block_name = item_name  # Same name for blocks
                                    get_item_id_for_block._block_to_item_map[block_name] = item_name
            except Exception as e:
                print(f"  │  ⚠ Warning: Could not load item registry: {e}")
    
    # Map block state ID to block name using the blocks.json lookup
    block_name = get_block_name_from_state_id(block_state_id)
    if block_name:
        # For blocks, the item name is usually the same as the block name
        item_name = block_name
    else:
        item_name = None
    
    if item_name and item_name in get_item_id_for_block._item_id_cache:
        return get_item_id_for_block._item_id_cache[item_name]
    
    # Fallback: return None (no item drop) if we can't find the item
    return None


def handle_client(client_socket, client_address):
    """Handle a single client connection."""
    print(f"\n{'='*60}")
    print(f"[{datetime.now().strftime('%H:%M:%S')}] New connection from {client_address}")
    print(f"{'='*60}\n")
    
    # Track connection state
    connection_state = ConnectionState.HANDSHAKING
    known_packs_sent = False
    known_packs_received = False
    
    # Initialize world and player state
    world = None  # Will be initialized when entering PLAY state
    player_uuid = None  # Will be set during login
    player = None  # Will be initialized when entering PLAY state
    web_server_thread = None  # Web server thread for visualization
    
    # Keep alive tracking
    keep_alive_thread = None
    keep_alive_stop_event = threading.Event()
    last_keep_alive_id = None
    
    try:
        buffer = b''
        
        while True:
            # Receive data
            data = client_socket.recv(4096)
            if not data:
                print(f"[{datetime.now().strftime('%H:%M:%S')}] Connection closed by client")
                break
            
            buffer += data
            
            # Try to parse packets from buffer
            offset = 0
            while offset < len(buffer):
                try:
                    # Try to read packet length
                    if len(buffer) - offset < 1:
                        break  # Not enough data for even a VarInt
                    
                    # Read packet length (VarInt)
                    packet_length, length_end = read_varint(buffer, offset)
                    
                    if packet_length < 0 or packet_length > 2097151:  # Max packet size
                        print(f"[{datetime.now().strftime('%H:%M:%S')}] Invalid packet length: {packet_length}")
                        offset += 1
                        continue
                    
                    # Check if we have the full packet
                    total_packet_size = length_end + packet_length
                    if len(buffer) < total_packet_size:
                        # Not enough data yet, wait for more
                        break
                    
                    # Extract the packet (including length prefix for parser)
                    full_packet = buffer[offset:total_packet_size]
                    packet_data = buffer[length_end:total_packet_size]
                    
                    # Try to read packet ID for display
                    packet_id = None
                    payload = b''
                    if len(packet_data) > 0:
                        packet_id, id_end = read_varint(packet_data, 0)
                        payload = packet_data[id_end:]
                    
                    # Print raw packet info
                    print(f"[{datetime.now().strftime('%H:%M:%S')}] Packet received:")
                    print(f"  State: {connection_state.name}")
                    print(f"  Length: {packet_length} bytes")
                    if packet_id is not None:
                        print(f"  Packet ID: {packet_id} (0x{packet_id:02x})")
                    print(f"  Full packet (hex): {format_hex(full_packet)}")
                    if len(payload) > 0:
                        print(f"  Payload (hex): {format_hex(payload)}")
                    
                    # Try to parse using protocol parser
                    try:
                        # Parser expects full packet including length prefix
                        # full_packet = buffer[offset:total_packet_size] includes the length
                        # Ensure it's bytes
                        if not isinstance(full_packet, bytes):
                            raise TypeError(f"Expected bytes, got {type(full_packet)}")
                        parsed_packet_id, parsed_packet = PacketParser.parse_packet(
                            full_packet, connection_state
                        )
                        
                        if parsed_packet is not None:
                            print(f"  ┌─ Parsed Packet Data:")
                            
                            if isinstance(parsed_packet, HandshakePacket):
                                print(f"  │  Type: Handshake")
                                print(f"  │  Protocol Version: {parsed_packet.protocol_version}")
                                print(f"  │  Server Address: {parsed_packet.server_address}")
                                print(f"  │  Server Port: {parsed_packet.server_port}")
                                intent_names = {1: "Status", 2: "Login", 3: "Transfer"}
                                intent_name = intent_names.get(parsed_packet.intent, f"Unknown ({parsed_packet.intent})")
                                print(f"  │  Intent: {parsed_packet.intent} ({intent_name})")
                                
                                # Update state based on intent
                                if parsed_packet.intent == 2:  # Login
                                    connection_state = ConnectionState.LOGIN
                                    print(f"  │  → State transition: HANDSHAKING → LOGIN")
                                elif parsed_packet.intent == 1:  # Status
                                    connection_state = ConnectionState.STATUS
                                    print(f"  │  → State transition: HANDSHAKING → STATUS")
                            
                            elif isinstance(parsed_packet, LoginStartPacket):
                                print(f"  │  Type: Login Start")
                                print(f"  │  Username: {parsed_packet.username}")
                                print(f"  │  Player UUID: {parsed_packet.player_uuid}")
                                
                                # Store player UUID for later use
                                player_uuid = parsed_packet.player_uuid
                                
                                # Respond with Login Success
                                print(f"  │  → Sending Login Success response...")
                                try:
                                    profile = GameProfile(
                                        uuid=parsed_packet.player_uuid,  # Use UUID from client
                                        username=parsed_packet.username,
                                        properties=[]  # Empty for offline mode
                                    )
                                    login_success = PacketBuilder.build_login_success(profile)
                                    client_socket.send(login_success)
                                    print(f"  │  ✓ Login Success sent ({len(login_success)} bytes)")
                                    print(f"  │  → Waiting for Login Acknowledged...")
                                except Exception as send_error:
                                    print(f"  │  ✗ Error sending Login Success: {send_error}")
                            
                            elif isinstance(parsed_packet, ClientInformationPacket):
                                print(f"  │  Type: Client Information")
                                print(f"  │  Locale: {parsed_packet.locale}")
                                print(f"  │  View Distance: {parsed_packet.view_distance} chunks")
                                print(f"  │  Chat Mode: {parsed_packet.chat_mode}")
                                print(f"  │  Chat Colors: {parsed_packet.chat_colors}")
                                print(f"  │  Main Hand: {'Left' if parsed_packet.main_hand == 0 else 'Right'}")
                                
                                # Send Known Packs first (allows us to omit NBT data)
                                if not known_packs_sent:
                                    print(f"  │  → Sending Known Packs...")
                                    try:
                                        known_packs = PacketBuilder.build_known_packs([
                                            ("minecraft", "core", "1.21.10")
                                        ])
                                        client_socket.send(known_packs)
                                        known_packs_sent = True
                                        print(f"  │  ✓ Known Packs sent ({len(known_packs)} bytes)")
                                        print(f"  │  → Waiting for client's Known Packs response...")
                                    except Exception as send_error:
                                        print(f"  │  ✗ Error sending Known Packs: {send_error}")
                                        import traceback
                                        traceback.print_exc()
                            
                            elif isinstance(parsed_packet, dict) and connection_state == ConnectionState.CONFIGURATION and parsed_packet_id == 2:
                                # Plugin Message (Configuration)
                                channel = parsed_packet.get("channel", "")
                                data = parsed_packet.get("data", b"")
                                print(f"  │  Type: Plugin Message")
                                print(f"  │  Channel: {channel}")
                                if channel == "minecraft:brand":
                                    try:
                                        brand = data.decode('utf-8')
                                        print(f"  │  Brand: {brand}")
                                    except:
                                        print(f"  │  Data: {len(data)} bytes")
                                else:
                                    print(f"  │  Data: {len(data)} bytes")
                            
                            elif isinstance(parsed_packet, list) and connection_state == ConnectionState.CONFIGURATION and parsed_packet_id == 0x07:
                                # Serverbound Known Packs (parsed as list)
                                print(f"  │  Type: Serverbound Known Packs")
                                packs = parsed_packet
                                print(f"  │  Client knows {len(packs)} pack(s):")
                                for namespace, pack_id, version in packs:
                                    print(f"  │    - {namespace}:{pack_id} (version {version})")
                                known_packs_received = True
                                
                                # Load registry data from JSON file
                                registry_data_file = os.path.join(os.path.dirname(os.path.dirname(__file__)), 'extracted_data', 'registry_data.json')
                                registry_json_data = {}
                                if os.path.exists(registry_data_file):
                                    try:
                                        with open(registry_data_file, 'r') as f:
                                            registry_json_data = json.load(f)
                                    except Exception as e:
                                        print(f"  │  ⚠ Warning: Could not load registry_data.json: {e}")
                                
                                def load_json_list(file_path):
                                    """Load a JSON list file from extracted_data/."""
                                    if os.path.exists(file_path):
                                        try:
                                            with open(file_path, 'r') as f:
                                                return json.load(f)
                                        except Exception:
                                            return []
                                    return []
                                
                                def get_biome_entries():
                                    """Load all biome entries from extracted_data/biomes.json."""
                                    script_dir = os.path.dirname(os.path.dirname(__file__))
                                    biomes_file = os.path.join(script_dir, 'extracted_data', 'biomes.json')
                                    return load_json_list(biomes_file)
                                
                                def get_damage_type_entries():
                                    """Load all damage_type entries from extracted_data/damage_types.json."""
                                    script_dir = os.path.dirname(os.path.dirname(__file__))
                                    damage_types_file = os.path.join(script_dir, 'extracted_data', 'damage_types.json')
                                    return load_json_list(damage_types_file)
                                
                                # Build required registries list with actual entry names
                                def get_registry_entries(registry_id):
                                    """Get all entries for a registry from JSON, or return default."""
                                    # Special handling for biome registry (extract from JAR)
                                    if registry_id == "minecraft:worldgen/biome":
                                        biome_entries = get_biome_entries()
                                        if biome_entries:
                                            return [(entry, None) for entry in biome_entries]
                                        else:
                                            # Fallback: at least include plains
                                            return [("minecraft:plains", None)]
                                    
                                    # Special handling for damage_type registry (extract from JAR)
                                    if registry_id == "minecraft:damage_type":
                                        damage_entries = get_damage_type_entries()
                                        if damage_entries:
                                            return [(entry, None) for entry in damage_entries]
                                        else:
                                            # Fallback: at least include in_fire (required)
                                            return [("minecraft:in_fire", None)]
                                    
                                    if registry_id in registry_json_data:
                                        entries = list(registry_json_data[registry_id].keys())
                                        return [(entry, None) for entry in entries]  # All entries, no NBT
                                    else:
                                        # Fallback for registries not in JSON
                                        # NOTE: These entry names may not match actual Minecraft 1.21.10 entries
                                        # If you get "Failed to parse local data" errors, you need to update these
                                        # with the actual entry names from Minecraft's data files
                                        fallbacks = {
                                            "minecraft:dimension_type": [("minecraft:overworld", None)],
                                            # Cat variants - these seem to work (11 entries sent successfully)
                                            "minecraft:cat_variant": [("minecraft:tabby", None), ("minecraft:black", None), ("minecraft:red", None), ("minecraft:siamese", None), ("minecraft:british_shorthair", None), ("minecraft:calico", None), ("minecraft:persian", None), ("minecraft:ragdoll", None), ("minecraft:white", None), ("minecraft:jellie", None), ("minecraft:all_black", None)],
                                            # Frog variants - these seem to work (3 entries sent successfully)
                                            "minecraft:frog_variant": [("minecraft:temperate", None), ("minecraft:warm", None), ("minecraft:cold", None)],
                                            # These registries extracted from Minecraft 1.21.10 server JAR
                                            "minecraft:chicken_variant": [("minecraft:cold", None), ("minecraft:temperate", None), ("minecraft:warm", None)],
                                            "minecraft:cow_variant": [("minecraft:cold", None), ("minecraft:temperate", None), ("minecraft:warm", None)],
                                            "minecraft:pig_variant": [("minecraft:cold", None), ("minecraft:temperate", None), ("minecraft:warm", None)],
                                            "minecraft:wolf_sound_variant": [("minecraft:angry", None), ("minecraft:big", None), ("minecraft:classic", None), ("minecraft:cute", None), ("minecraft:grumpy", None), ("minecraft:puglin", None), ("minecraft:sad", None)],
                                            # Painting variants - common painting names
                                            "minecraft:painting_variant": [("minecraft:kebab", None), ("minecraft:aztec", None), ("minecraft:alban", None), ("minecraft:aztec2", None), ("minecraft:bomb", None), ("minecraft:plant", None), ("minecraft:wasteland", None), ("minecraft:pool", None), ("minecraft:courbet", None), ("minecraft:sea", None), ("minecraft:sunset", None), ("minecraft:creebet", None), ("minecraft:wanderer", None), ("minecraft:graham", None), ("minecraft:match", None), ("minecraft:bust", None), ("minecraft:stage", None), ("minecraft:void", None), ("minecraft:skull_and_roses", None), ("minecraft:wither", None), ("minecraft:fighters", None), ("minecraft:pointer", None), ("minecraft:pigscene", None), ("minecraft:burning_skull", None), ("minecraft:skeleton", None), ("minecraft:donkey_kong", None)],
                                            # Wolf variants - common wolf variants
                                            "minecraft:wolf_variant": [("minecraft:striped", None), ("minecraft:chestnut", None), ("minecraft:rusty", None), ("minecraft:spotted", None), ("minecraft:snowy", None), ("minecraft:black", None), ("minecraft:ash", None), ("minecraft:wood", None)],
                                        }
                                        return fallbacks.get(registry_id, [])
                                
                                # Required non-empty registries
                                required_registry_ids = [
                                    "minecraft:dimension_type",
                                    "minecraft:cat_variant",
                                    "minecraft:chicken_variant",
                                    "minecraft:cow_variant",
                                    "minecraft:frog_variant",
                                    "minecraft:painting_variant",
                                    "minecraft:pig_variant",
                                    "minecraft:wolf_variant",
                                    "minecraft:wolf_sound_variant",
                                    "minecraft:worldgen/biome",  # REQUIRED - must include minecraft:plains
                                    "minecraft:damage_type",  # REQUIRED - must include minecraft:in_fire and others
                                ]
                                
                                print(f"  │  → Sending Registry Data for {len(required_registry_ids)} registries...")
                                for registry_id in required_registry_ids:
                                    entries = get_registry_entries(registry_id)
                                    if not entries:
                                        print(f"  │  ⚠ Warning: No entries found for {registry_id}, skipping")
                                        continue
                                    try:
                                        registry_data = PacketBuilder.build_registry_data(
                                            registry_id=registry_id,
                                            entries=entries
                                        )
                                        client_socket.send(registry_data)
                                        print(f"  │  ✓ {registry_id}: {len(entries)} entry(ies) ({len(registry_data)} bytes)")
                                    except Exception as send_error:
                                        print(f"  │  ✗ Error sending {registry_id}: {send_error}")
                                        import traceback
                                        traceback.print_exc()
                                
                                # Send Finish Configuration
                                print(f"  │  → Sending Finish Configuration...")
                                try:
                                    finish_config = PacketBuilder.build_finish_configuration()
                                    client_socket.send(finish_config)
                                    print(f"  │  ✓ Finish Configuration sent ({len(finish_config)} bytes)")
                                    print(f"  │  → Waiting for Acknowledge Finish Configuration...")
                                except Exception as send_error:
                                    print(f"  │  ✗ Error sending Finish Configuration: {send_error}")
                                
                                print(f"  └─")
                            
                            # Handle PLAY state packets
                            elif connection_state == ConnectionState.PLAY:
                                if parsed_packet_id == 0x1D:  # Set Player Position
                                    if isinstance(parsed_packet, SetPlayerPositionPacket):
                                        print(f"  │  Type: Set Player Position")
                                        print(f"  │  Position: ({parsed_packet.x:.2f}, {parsed_packet.y:.2f}, {parsed_packet.z:.2f})")
                                        
                                        # Update player state and handle chunk loading
                                        if world and player:
                                            chunk_change = player.update_position(
                                                parsed_packet.x, parsed_packet.y, parsed_packet.z
                                            )
                                            
                                            # Check for item pickups (entities are updated by background thread)
                                            items_to_pickup = player.check_item_pickups(world.item_entities)
                                            if items_to_pickup:
                                                for item_entity in items_to_pickup:
                                                    try:
                                                        # Send Pickup Item packet (for animation)
                                                        pickup_packet = PacketBuilder.build_pickup_item(
                                                            collected_entity_id=item_entity.entity_id,
                                                            collector_entity_id=1,  # Player entity ID is usually 1
                                                            pickup_count=item_entity.count
                                                        )
                                                        client_socket.sendall(pickup_packet)
                                                        
                                                        # Send Destroy Entities packet (to remove from world)
                                                        destroy_packet = PacketBuilder.build_destroy_entities([item_entity.entity_id])
                                                        client_socket.sendall(destroy_packet)
                                                        
                                                        # Find a slot for the item
                                                        slot_idx = player.find_slot_for_item(item_entity.item_id, item_entity.count)
                                                        
                                                        if slot_idx is not None:
                                                            # Determine final item ID and count
                                                            if slot_idx in player.inventory_slots:
                                                                # Stacking with existing items
                                                                existing_item_id, existing_count = player.inventory_slots[slot_idx]
                                                                new_count = existing_count + item_entity.count
                                                                # Cap at stack size of 64
                                                                if new_count > 64:
                                                                    new_count = 64
                                                                final_item_id = existing_item_id
                                                                final_count = new_count
                                                            else:
                                                                # New slot
                                                                final_item_id = item_entity.item_id
                                                                final_count = item_entity.count
                                                            
                                                            # Update slot tracking
                                                            player.update_slot(slot_idx, final_item_id, final_count)
                                                            
                                                            # Increment state ID
                                                            player.inventory_state_id += 1
                                                            
                                                            # Send Set Container Slot packet to update client inventory
                                                            container_slot_packet = PacketBuilder.build_set_container_slot(
                                                                window_id=0,  # 0 = player inventory
                                                                state_id=player.inventory_state_id,
                                                                slot=slot_idx,
                                                                item_id=final_item_id,
                                                                count=final_count
                                                            )
                                                            client_socket.sendall(container_slot_packet)
                                                            
                                                            # Add to server-side inventory tracking
                                                            player.add_to_inventory(item_entity.item_id, item_entity.count)
                                                            
                                                            print(f"  │  ✓ Item picked up (Entity ID: {item_entity.entity_id}, Item ID: {item_entity.item_id}, Count: {item_entity.count}, Slot: {slot_idx})")
                                                        else:
                                                            print(f"  │  ⚠ Inventory full, item not picked up (Entity ID: {item_entity.entity_id})")
                                                        
                                                        # Remove from tracking
                                                        world.remove_item_entity(item_entity.entity_id)
                                                    except Exception as e:
                                                        print(f"  │  ✗ Error picking up item {item_entity.entity_id}: {e}")
                                            
                                            if chunk_change:
                                                old_chunk, new_chunk = chunk_change
                                                print(f"  │  → Player crossed chunk boundary: {old_chunk} → {new_chunk}")
                                                
                                                # Send Set Center Chunk
                                                try:
                                                    center_chunk = PacketBuilder.build_set_center_chunk(
                                                        chunk_x=new_chunk[0],
                                                        chunk_z=new_chunk[1]
                                                    )
                                                    client_socket.sendall(center_chunk)
                                                    print(f"  │  ✓ Set Center Chunk sent ({len(center_chunk)} bytes)")
                                                except Exception as e:
                                                    print(f"  │  ✗ Error sending Set Center Chunk: {e}")
                                                
                                                # Queue new chunks for async loading
                                                chunks_to_load = player.get_chunks_to_load()
                                                if chunks_to_load:
                                                    print(f"  │  → Queueing {len(chunks_to_load)} new chunk(s) for async loading...")
                                                    chunk_loader.queue_chunks(chunks_to_load, center_chunk=new_chunk)
                                                
                                                # Queue distant chunks for unloading
                                                chunks_to_unload = player.get_chunks_to_unload()
                                                if chunks_to_unload:
                                                    print(f"  │  → Queueing {len(chunks_to_unload)} distant chunk(s) for unloading...")
                                                    chunk_loader.queue_unload(chunks_to_unload)
                                        
                                        print(f"  └─")
                                
                                elif parsed_packet_id == 0x1B:  # Serverbound Keep Alive
                                    if isinstance(parsed_packet, KeepAlivePacket):
                                        print(f"  │  Type: Keep Alive Response")
                                        print(f"  │  Keep Alive ID: {parsed_packet.keep_alive_id}")
                                        print(f"  └─")
                                
                                elif parsed_packet_id == 0x28:  # Player Action
                                    if isinstance(parsed_packet, PlayerActionPacket):
                                        status_names = {
                                            0: "Started digging",
                                            1: "Cancelled digging",
                                            2: "Finished digging",
                                            3: "Drop item stack",
                                            4: "Drop item",
                                            5: "Shoot arrow / finish eating",
                                            6: "Swap item in hand"
                                        }
                                        status_name = status_names.get(parsed_packet.status, f"Unknown ({parsed_packet.status})")
                                        print(f"  │  Type: Player Action")
                                        print(f"  │  Status: {parsed_packet.status} ({status_name})")
                                        print(f"  │  Location: {parsed_packet.location}")
                                        print(f"  │  Face: {parsed_packet.face}")
                                        print(f"  │  Sequence: {parsed_packet.sequence}")
                                        
                                        # Handle item dropping (Status 3 = drop stack, Status 4 = drop item)
                                        if parsed_packet.status == 3 or parsed_packet.status == 4:
                                            if world and player:
                                                # Determine which slot to use (selected hotbar slot)
                                                slot_idx = 36 + player.selected_hotbar_slot
                                                
                                                # Get item from slot
                                                if slot_idx in player.inventory_slots:
                                                    item_id, count = player.inventory_slots[slot_idx]
                                                    
                                                    if item_id > 0 and count > 0:
                                                        # Determine how many items to drop
                                                        if parsed_packet.status == 3:  # Drop stack
                                                            drop_count = count
                                                        else:  # status == 4, drop single item
                                                            drop_count = 1
                                                        
                                                        # Calculate new count
                                                        new_count = count - drop_count
                                                        
                                                        # Update slot
                                                        if new_count > 0:
                                                            player.update_slot(slot_idx, item_id, new_count)
                                                        else:
                                                            # Slot is now empty
                                                            player.update_slot(slot_idx, 0, 0)
                                                        
                                                        # Increment state ID
                                                        player.inventory_state_id += 1
                                                        
                                                        # Send Set Container Slot to update client inventory
                                                        container_slot_packet = PacketBuilder.build_set_container_slot(
                                                            window_id=0,  # Player inventory
                                                            state_id=player.inventory_state_id,
                                                            slot=slot_idx,
                                                            item_id=item_id if new_count > 0 else 0,
                                                            count=new_count
                                                        )
                                                        client_socket.sendall(container_slot_packet)
                                                        
                                                        # Spawn item entity in the world
                                                        try:
                                                            # Calculate spawn position (at player's eye level)
                                                            # Player eye level is approximately 1.52 blocks above feet (slightly below top of head)
                                                            spawn_x = player.x + (random.random() - 0.5) * 0.3
                                                            spawn_y = player.y + 1.52
                                                            spawn_z = player.z + (random.random() - 0.5) * 0.3
                                                            
                                                            # Generate entity ID
                                                            entity_id = world.next_entity_id
                                                            world.next_entity_id += 1
                                                            
                                                            # Generate UUID
                                                            item_uuid = uuid.uuid4()
                                                            
                                                            # Calculate velocity based on player's look direction
                                                            velocity_x, velocity_y, velocity_z = player.calculate_drop_velocity()
                                                            
                                                            # Get entity type ID
                                                            item_entity_type_id = get_entity_type_id('minecraft:item')
                                                            if item_entity_type_id is None:
                                                                item_entity_type_id = 70  # Fallback
                                                            
                                                            # Spawn item entity
                                                            spawn_packet = PacketBuilder.build_spawn_entity(
                                                                entity_id=entity_id,
                                                                entity_uuid=item_uuid,
                                                                entity_type=item_entity_type_id,
                                                                x=spawn_x,
                                                                y=spawn_y,
                                                                z=spawn_z,
                                                                velocity_x=velocity_x,
                                                                velocity_y=velocity_y,
                                                                velocity_z=velocity_z,
                                                                pitch=0.0,
                                                                yaw=0.0,
                                                                head_yaw=0.0,
                                                                is_living_entity=True,  # Required for item entities (protocol quirk)
                                                                has_data_field=True
                                                            )
                                                            client_socket.sendall(spawn_packet)
                                                            
                                                            # Send Entity Metadata to set the item stack
                                                            metadata_packet = PacketBuilder.build_set_entity_metadata(
                                                                entity_id=entity_id,
                                                                metadata=[
                                                                    (8, 7, (item_id, drop_count))  # Index 8, type 7 (Slot), (item_id, count)
                                                                ]
                                                            )
                                                            client_socket.sendall(metadata_packet)
                                                            
                                                            # Track the item entity
                                                            item_entity = ItemEntity(
                                                                entity_id=entity_id,
                                                                uuid=item_uuid,
                                                                x=spawn_x,
                                                                y=spawn_y,
                                                                z=spawn_z,
                                                                velocity_x=velocity_x,
                                                                velocity_y=velocity_y,
                                                                velocity_z=velocity_z,
                                                                item_id=item_id,
                                                                count=drop_count,
                                                                spawn_time=time.time(),
                                                                last_update_time=time.time()
                                                            )
                                                            world.item_entities[entity_id] = item_entity
                                                            
                                                            print(f"  │  ✓ Item dropped: {drop_count}x item ID {item_id} from slot {slot_idx}")
                                                            print(f"  │  ✓ Item entity spawned (ID: {entity_id}, Pos: ({spawn_x:.1f}, {spawn_y:.1f}, {spawn_z:.1f}))")
                                                        except Exception as e:
                                                            print(f"  │  ✗ Error spawning dropped item: {e}")
                                                            import traceback
                                                            traceback.print_exc()
                                                    else:
                                                        print(f"  │  ⚠ No item in selected slot to drop")
                                                else:
                                                    print(f"  │  ⚠ Selected slot {slot_idx} is empty")
                                        
                                        # Handle block breaking
                                        if parsed_packet.status == 2:  # Finished digging
                                            x, y, z = parsed_packet.location
                                            print(f"  │  → Block broken at ({x}, {y}, {z})")
                                            
                                            # Get the block state ID BEFORE breaking it (for loot table lookup)
                                            block_state_id = None
                                            block_name = None
                                            if world:
                                                # Get the actual block that's being broken
                                                block_state_id = world.get_block_at(x, y, z)
                                                
                                                # Get block name from block state ID
                                                block_name = get_block_name_from_state_id(block_state_id)
                                                
                                                if block_name:
                                                    print(f"  │  → Breaking block: {block_name} (state ID: {block_state_id})")
                                                else:
                                                    print(f"  │  ⚠ Could not find block name for state ID {block_state_id}")
                                            
                                            # Update block data in world (set to air)
                                            if world:
                                                world.set_block(x, y, z, 0)  # 0 = air
                                            
                                            # Send Block Update to set block to air (block state ID 0)
                                            try:
                                                block_update = PacketBuilder.build_block_update(
                                                    x=x,
                                                    y=y,
                                                    z=z,
                                                    block_state_id=0  # Air
                                                )
                                                client_socket.sendall(block_update)
                                                print(f"  │  ✓ Block Update sent (set to air)")
                                            except Exception as e:
                                                print(f"  │  ✗ Error sending Block Update: {e}")
                                            
                                            # Spawn item drop using loot tables
                                            if world and block_name:
                                                # Get item name from loot table
                                                item_name = get_item_for_block(block_name)
                                                
                                                if item_name is None:
                                                    print(f"  │  ⚠ No loot table entry for {block_name}, skipping item drop")
                                                    continue
                                                
                                                # Get item ID from registry
                                                item_id = get_item_id_from_name(item_name)
                                                
                                                if item_id is None:
                                                    print(f"  │  ⚠ Could not find item ID for {item_name} (from {block_name}), skipping item drop")
                                                    continue
                                                
                                                print(f"  │  → Block {block_name} drops {item_name} (ID: {item_id})")
                                                
                                                try:
                                                    # Generate entity ID
                                                    entity_id = world.next_entity_id
                                                    world.next_entity_id += 1
                                                    
                                                    # Generate UUID for the item entity
                                                    import uuid as uuid_module
                                                    item_uuid = uuid_module.uuid4()
                                                    
                                                    # Calculate spawn position (center of block + small offset)
                                                    spawn_x = x + 0.5
                                                    spawn_y = y + 0.5
                                                    spawn_z = z + 0.5
                                                    
                                                    # Calculate velocity for block break drops (small random spread, mostly downward)
                                                    # Block break drops should fall straight down, not be thrown in player's look direction
                                                    # Calculate velocity for block break drops (small random spread, mostly downward)
                                                    # Block break drops should fall straight down, not be thrown in player's look direction
                                                    velocity_x = (random.random() - 0.5) * 0.1  # Small random horizontal spread
                                                    velocity_y = 0.1  # Slight upward velocity
                                                    velocity_z = (random.random() - 0.5) * 0.1  # Small random horizontal spread
                                                    
                                                    # Spawn item entity
                                                    # Get entity type ID from registries.json (extracted from server JAR)
                                                    # For 1.21.10: minecraft:item = 70, minecraft:item_display = 71
                                                    # NOTE: Even though item entities are NOT living entities according to the protocol,
                                                    # they still require the Head Yaw field (client expects 54 bytes, not 53)
                                                    # Item entities are not listed in Object data docs, so Data field should be 0
                                                    item_entity_type_id = get_entity_type_id('minecraft:item')
                                                    if item_entity_type_id is None:
                                                        print(f"  │  ⚠ Warning: Could not find entity type ID for minecraft:item, using 70 as fallback")
                                                        item_entity_type_id = 70
                                                    
                                                    spawn_packet = PacketBuilder.build_spawn_entity(
                                                        entity_id=entity_id,
                                                        entity_uuid=item_uuid,
                                                        entity_type=item_entity_type_id,  # Item entity type (extracted from registries.json)
                                                        x=spawn_x,
                                                        y=spawn_y,
                                                        z=spawn_z,
                                                        velocity_x=velocity_x,
                                                        velocity_y=velocity_y,
                                                        velocity_z=velocity_z,
                                                        pitch=0.0,
                                                        yaw=0.0,
                                                        head_yaw=0.0,
                                                        is_living_entity=True,  # Include Head Yaw (required for item entities despite not being living)
                                                        has_data_field=True  # Include Data field (will be 0)
                                                    )
                                                    client_socket.sendall(spawn_packet)
                                                    
                                                    # Send Entity Metadata to set the item stack
                                                    # For item entities, index 8 is the item stack (Slot type 7)
                                                    metadata_packet = PacketBuilder.build_set_entity_metadata(
                                                        entity_id=entity_id,
                                                        metadata=[
                                                            (8, 7, (item_id, 1))  # Index 8, type 7 (Slot), (item_id, count)
                                                        ]
                                                    )
                                                    client_socket.sendall(metadata_packet)
                                                    
                                                    # Track the item entity
                                                    item_entity = ItemEntity(
                                                        entity_id=entity_id,
                                                        uuid=item_uuid,
                                                        x=spawn_x,
                                                        y=spawn_y,
                                                        z=spawn_z,
                                                        velocity_x=velocity_x,
                                                        velocity_y=velocity_y,
                                                        velocity_z=velocity_z,
                                                        item_id=item_id,
                                                        count=1,
                                                        spawn_time=time.time(),
                                                        last_update_time=time.time()
                                                    )
                                                    world.item_entities[entity_id] = item_entity
                                                    
                                                    print(f"  │  ✓ Item entity spawned and tracked (ID: {entity_id}, Item: {item_id}, Pos: ({spawn_x:.1f}, {spawn_y:.1f}, {spawn_z:.1f}))")
                                                except Exception as e:
                                                    print(f"  │  ✗ Error spawning item: {e}")
                                                    import traceback
                                                    traceback.print_exc()
                                        
                                        print(f"  └─")
                                
                                elif parsed_packet_id == 0x11:  # Click Container
                                    if isinstance(parsed_packet, ClickContainerPacket):
                                        print(f"  │  Type: Click Container")
                                        print(f"  │  Window ID: {parsed_packet.window_id}")
                                        print(f"  │  State ID: {parsed_packet.state_id}")
                                        print(f"  │  Slot: {parsed_packet.slot}")
                                        print(f"  │  Button: {parsed_packet.button}")
                                        print(f"  │  Mode: {parsed_packet.mode}")
                                        print(f"  │  Changed Slots: {len(parsed_packet.changed_slots)}")
                                        print(f"  │  Carried Item: {parsed_packet.carried_item}")
                                        
                                        # Handle item dropping by dragging outside inventory (Mode 0, Slot -999)
                                        if parsed_packet.mode == 0 and parsed_packet.slot == -999 and world and parsed_packet.window_id == 0:
                                            # Dragging item outside inventory to drop it
                                            # When dragging out, the client sends carried_item as (0, 0) because it's already dropped
                                            # We need to check the previous cursor state
                                            current_carried = parsed_packet.carried_item
                                            previous_carried = player.cursor_item
                                            
                                            # If previous cursor had an item and current is empty, item was dropped
                                            if previous_carried and previous_carried[0] > 0 and previous_carried[1] > 0:
                                                carried_item_id, carried_count = previous_carried
                                                
                                                # Determine how many items to drop based on button
                                                if parsed_packet.button == 0:
                                                    # Left click outside = drop entire stack
                                                    drop_count = carried_count
                                                else:  # button == 1
                                                    # Right click outside = drop single item
                                                    drop_count = 1
                                                
                                                # Cap drop_count to available count
                                                drop_count = min(drop_count, carried_count)
                                                
                                                if drop_count > 0:
                                                    # Spawn item entity in the world
                                                    try:
                                                        # Calculate spawn position (at player's eye level)
                                                        # Player eye level is approximately 1.52 blocks above feet (slightly below top of head)
                                                        spawn_x = world.x + (random.random() - 0.5) * 0.3
                                                        spawn_y = world.y + 1.52
                                                        spawn_z = world.z + (random.random() - 0.5) * 0.3
                                                        
                                                        # Generate entity ID
                                                        entity_id = world.next_entity_id
                                                        world.next_entity_id += 1
                                                        
                                                        # Generate UUID
                                                        item_uuid = uuid.uuid4()
                                                        
                                                        # Calculate velocity based on player's look direction
                                                        velocity_x, velocity_y, velocity_z = player.calculate_drop_velocity()
                                                        
                                                        # Get entity type ID
                                                        item_entity_type_id = get_entity_type_id('minecraft:item')
                                                        if item_entity_type_id is None:
                                                            item_entity_type_id = 70  # Fallback
                                                        
                                                        # Spawn item entity
                                                        spawn_packet = PacketBuilder.build_spawn_entity(
                                                            entity_id=entity_id,
                                                            entity_uuid=item_uuid,
                                                            entity_type=item_entity_type_id,
                                                            x=spawn_x,
                                                            y=spawn_y,
                                                            z=spawn_z,
                                                            velocity_x=velocity_x,
                                                            velocity_y=velocity_y,
                                                            velocity_z=velocity_z,
                                                            pitch=0.0,
                                                            yaw=0.0,
                                                            head_yaw=0.0,
                                                            is_living_entity=True,  # Required for item entities (protocol quirk)
                                                            has_data_field=True
                                                        )
                                                        client_socket.sendall(spawn_packet)
                                                        
                                                        # Send Entity Metadata to set the item stack
                                                        metadata_packet = PacketBuilder.build_set_entity_metadata(
                                                            entity_id=entity_id,
                                                            metadata=[
                                                                (8, 7, (carried_item_id, drop_count))  # Index 8, type 7 (Slot), (item_id, count)
                                                            ]
                                                        )
                                                        client_socket.sendall(metadata_packet)
                                                        
                                                        # Track the item entity
                                                        item_entity = ItemEntity(
                                                            entity_id=entity_id,
                                                            uuid=item_uuid,
                                                            x=spawn_x,
                                                            y=spawn_y,
                                                            z=spawn_z,
                                                            velocity_x=velocity_x,
                                                            velocity_y=velocity_y,
                                                            velocity_z=velocity_z,
                                                            item_id=carried_item_id,
                                                            count=drop_count,
                                                            spawn_time=time.time(),
                                                            last_update_time=time.time()
                                                        )
                                                        world.item_entities[entity_id] = item_entity
                                                        
                                                        print(f"  │  ✓ Item dropped by dragging: {drop_count}x item ID {carried_item_id}")
                                                        print(f"  │  ✓ Item entity spawned (ID: {entity_id}, Pos: ({spawn_x:.1f}, {spawn_y:.1f}, {spawn_z:.1f}))")
                                                    except Exception as e:
                                                        print(f"  │  ✗ Error spawning dropped item: {e}")
                                                        import traceback
                                                        traceback.print_exc()
                                            
                                            # Update cursor item state
                                            if current_carried[0] > 0 and current_carried[1] > 0:
                                                player.cursor_item = current_carried
                                            else:
                                                player.cursor_item = None
                                        
                                        # Handle item dropping (Mode 4)
                                        if parsed_packet.mode == 4 and world and parsed_packet.window_id == 0:
                                            # Mode 4: Drop item
                                            # Button 0: Drop key (Q) - drops 1 item
                                            # Button 1: Control + Drop key (Q) - drops entire stack
                                            # Slot indicates which slot the item is being dropped from
                                            
                                            slot_number = parsed_packet.slot
                                            if slot_number in player.inventory_slots:
                                                item_id, count = player.inventory_slots[slot_number]
                                                
                                                if item_id > 0 and count > 0:
                                                    # Determine how many items to drop
                                                    if parsed_packet.button == 0:
                                                        # Drop 1 item
                                                        drop_count = 1
                                                    else:  # button == 1
                                                        # Drop entire stack
                                                        drop_count = count
                                                    
                                                    # Calculate new count
                                                    new_count = count - drop_count
                                                    
                                                    # Update slot
                                                    if new_count > 0:
                                                        player.update_slot(slot_number, item_id, new_count)
                                                    else:
                                                        # Slot is now empty
                                                        player.update_slot(slot_number, 0, 0)
                                                    
                                                    # Increment state ID
                                                    player.inventory_state_id += 1
                                                    
                                                    # Send Set Container Slot to update client inventory
                                                    container_slot_packet = PacketBuilder.build_set_container_slot(
                                                        window_id=0,  # Player inventory
                                                        state_id=player.inventory_state_id,
                                                        slot=slot_number,
                                                        item_id=item_id if new_count > 0 else 0,
                                                        count=new_count
                                                    )
                                                    client_socket.sendall(container_slot_packet)
                                                    
                                                    # Spawn item entity in the world
                                                    try:
                                                        # Calculate spawn position (at player's eye level)
                                                        # Player eye level is approximately 1.52 blocks above feet (slightly below top of head)
                                                        spawn_x = world.x + (random.random() - 0.5) * 0.3
                                                        spawn_y = world.y + 1.52
                                                        spawn_z = world.z + (random.random() - 0.5) * 0.3
                                                        
                                                        # Generate entity ID
                                                        entity_id = world.next_entity_id
                                                        world.next_entity_id += 1
                                                        
                                                        # Generate UUID
                                                        item_uuid = uuid.uuid4()
                                                        
                                                        # Calculate velocity based on player's look direction
                                                        velocity_x, velocity_y, velocity_z = player.calculate_drop_velocity()
                                                        
                                                        # Get entity type ID
                                                        item_entity_type_id = get_entity_type_id('minecraft:item')
                                                        if item_entity_type_id is None:
                                                            item_entity_type_id = 70  # Fallback
                                                        
                                                        # Spawn item entity
                                                        spawn_packet = PacketBuilder.build_spawn_entity(
                                                            entity_id=entity_id,
                                                            entity_uuid=item_uuid,
                                                            entity_type=item_entity_type_id,
                                                            x=spawn_x,
                                                            y=spawn_y,
                                                            z=spawn_z,
                                                            velocity_x=velocity_x,
                                                            velocity_y=velocity_y,
                                                            velocity_z=velocity_z,
                                                            pitch=0.0,
                                                            yaw=0.0,
                                                            head_yaw=0.0,
                                                            is_living_entity=True,  # Required for item entities (protocol quirk)
                                                            has_data_field=True
                                                        )
                                                        client_socket.sendall(spawn_packet)
                                                        
                                                        # Send Entity Metadata to set the item stack
                                                        metadata_packet = PacketBuilder.build_set_entity_metadata(
                                                            entity_id=entity_id,
                                                            metadata=[
                                                                (8, 7, (item_id, drop_count))  # Index 8, type 7 (Slot), (item_id, count)
                                                            ]
                                                        )
                                                        client_socket.sendall(metadata_packet)
                                                        
                                                        # Track the item entity
                                                        item_entity = ItemEntity(
                                                            entity_id=entity_id,
                                                            uuid=item_uuid,
                                                            x=spawn_x,
                                                            y=spawn_y,
                                                            z=spawn_z,
                                                            velocity_x=velocity_x,
                                                            velocity_y=velocity_y,
                                                            velocity_z=velocity_z,
                                                            item_id=item_id,
                                                            count=drop_count,
                                                            spawn_time=time.time(),
                                                            last_update_time=time.time()
                                                        )
                                                        world.item_entities[entity_id] = item_entity
                                                        
                                                        print(f"  │  ✓ Item dropped: {drop_count}x item ID {item_id} from slot {slot_number}")
                                                        print(f"  │  ✓ Item entity spawned (ID: {entity_id}, Pos: ({spawn_x:.1f}, {spawn_y:.1f}, {spawn_z:.1f}))")
                                                    except Exception as e:
                                                        print(f"  │  ✗ Error spawning dropped item: {e}")
                                                        import traceback
                                                        traceback.print_exc()
                                        
                                        # Update cursor item state (for all Click Container packets)
                                        if world:
                                            current_carried = parsed_packet.carried_item
                                            if current_carried[0] > 0 and current_carried[1] > 0:
                                                player.cursor_item = current_carried
                                            else:
                                                player.cursor_item = None
                                        
                                        # Update server-side inventory model
                                        if world and parsed_packet.window_id == 0:  # Player inventory
                                            # Update changed slots (for all modes, including drops)
                                            for slot_number, item_id, count in parsed_packet.changed_slots:
                                                if count > 0 and item_id > 0:
                                                    player.update_slot(slot_number, item_id, count)
                                                else:
                                                    # Empty slot
                                                    player.update_slot(slot_number, 0, 0)
                                            
                                            # Update state ID
                                            player.inventory_state_id = parsed_packet.state_id
                                            
                                            if parsed_packet.mode != 4:  # Don't double-log for drops
                                                print(f"  │  ✓ Inventory updated ({len(parsed_packet.changed_slots)} slot(s) changed)")
                                        
                                        print(f"  └─")
                                
                                elif parsed_packet_id == 0x34:  # Set Held Item (serverbound)
                                    if isinstance(parsed_packet, SetHeldItemPacket):
                                        print(f"  │  Type: Set Held Item")
                                        print(f"  │  Selected Slot: {parsed_packet.slot} (hotbar slot {parsed_packet.slot}, inventory slot {36 + parsed_packet.slot})")
                                        
                                        # Update selected hotbar slot
                                        if world:
                                            player.selected_hotbar_slot = parsed_packet.slot
                                            print(f"  │  ✓ Selected hotbar slot updated to {parsed_packet.slot}")
                                        
                                        print(f"  └─")
                                
                                elif parsed_packet_id == 0x3F:  # Use Item On
                                    if isinstance(parsed_packet, UseItemOnPacket):
                                        print(f"  │  Type: Use Item On")
                                        print(f"  │  Hand: {parsed_packet.hand} ({'main hand' if parsed_packet.hand == 0 else 'off hand'})")
                                        print(f"  │  Location: {parsed_packet.location}")
                                        print(f"  │  Face: {parsed_packet.face}")
                                        print(f"  │  Sequence: {parsed_packet.sequence}")
                                        
                                        # Handle block placement
                                        if world:
                                            # Determine which slot to use based on hand
                                            if parsed_packet.hand == 0:  # Main hand
                                                # Use currently selected hotbar slot (0-8 maps to 36-44)
                                                slot_idx = 36 + player.selected_hotbar_slot
                                            else:  # Off hand
                                                slot_idx = 45  # Offhand slot
                                            
                                            # Get item from slot
                                            if slot_idx in player.inventory_slots:
                                                item_id, count = player.inventory_slots[slot_idx]
                                                
                                                if item_id > 0 and count > 0:
                                                    # Decrement item count
                                                    new_count = count - 1
                                                    
                                                    # Update slot
                                                    if new_count > 0:
                                                        player.update_slot(slot_idx, item_id, new_count)
                                                    else:
                                                        # Slot is now empty
                                                        player.update_slot(slot_idx, 0, 0)
                                                    
                                                    # Increment state ID
                                                    player.inventory_state_id += 1
                                                    
                                                    # Send Set Container Slot to update client inventory
                                                    container_slot_packet = PacketBuilder.build_set_container_slot(
                                                            window_id=0,  # Player inventory
                                                            state_id=player.inventory_state_id,
                                                        slot=slot_idx,
                                                        item_id=item_id if new_count > 0 else 0,
                                                        count=new_count
                                                    )
                                                    client_socket.sendall(container_slot_packet)
                                                    
                                                    # Calculate block placement position (adjacent to clicked face)
                                                    x, y, z = parsed_packet.location
                                                    face = parsed_packet.face
                                                    # Face: 0=bottom, 1=top, 2=north, 3=south, 4=west, 5=east
                                                    face_offsets = {
                                                        0: (0, -1, 0),  # Bottom
                                                        1: (0, 1, 0),   # Top
                                                        2: (0, 0, -1),  # North
                                                        3: (0, 0, 1),   # South
                                                        4: (-1, 0, 0),  # West
                                                        5: (1, 0, 0)    # East
                                                    }
                                                    dx, dy, dz = face_offsets.get(face, (0, 1, 0))
                                                    place_x, place_y, place_z = x + dx, y + dy, z + dz
                                                    
                                                    # Map item_id to block_state_id
                                                    block_state_id = get_block_state_id_from_item_id(item_id)
                                                    
                                                    if block_state_id is None:
                                                        print(f"  │  ⚠ Could not map item ID {item_id} to block state ID, skipping block placement")
                                                        continue
                                                    
                                                    # Update block data in world
                                                    if world:
                                                        # Determine block ID from block state ID (simplified - in reality need to map state to block)
                                                        # For now, assume block_state_id corresponds to block ID
                                                        world.set_block(place_x, place_y, place_z, block_state_id)
                                                    
                                                    # Send Block Update to place the block
                                                    try:
                                                        block_update = PacketBuilder.build_block_update(
                                                            x=place_x,
                                                            y=place_y,
                                                            z=place_z,
                                                            block_state_id=block_state_id
                                                        )
                                                        client_socket.sendall(block_update)
                                                        print(f"  │  ✓ Block placed at ({place_x}, {place_y}, {place_z})")
                                                        print(f"  │  ✓ Inventory updated (Item ID: {item_id}, Count: {count} → {new_count})")
                                                    except Exception as e:
                                                        print(f"  │  ✗ Error placing block: {e}")
                                                else:
                                                    print(f"  │  ⚠ No item in hand to place")
                                            else:
                                                print(f"  │  ⚠ Slot {slot_idx} is empty")
                                        
                                        print(f"  └─")
                                
                                elif parsed_packet_id == 0x1F:  # Set Player Rotation
                                    if isinstance(parsed_packet, SetPlayerRotationPacket):
                                        print(f"  │  Type: Set Player Rotation")
                                        print(f"  │  Rotation: yaw={parsed_packet.yaw:.2f}, pitch={parsed_packet.pitch:.2f}")
                                        
                                        # Update player state with head rotation (this is the camera/head rotation)
                                        if world and player:
                                            player.yaw = parsed_packet.yaw
                                            player.pitch = parsed_packet.pitch
                                        
                                        print(f"  └─")
                                
                                elif parsed_packet_id == 0x1E:  # Set Player Position and Rotation
                                    if isinstance(parsed_packet, SetPlayerPositionAndRotationPacket):
                                        print(f"  │  Type: Set Player Position and Rotation")
                                        print(f"  │  Position: ({parsed_packet.x:.2f}, {parsed_packet.y:.2f}, {parsed_packet.z:.2f})")
                                        print(f"  │  Rotation: yaw={parsed_packet.yaw:.2f}, pitch={parsed_packet.pitch:.2f}")
                                        
                                        # Update player state with position and rotation
                                        if world:
                                            player.x = parsed_packet.x
                                            player.y = parsed_packet.y
                                            player.z = parsed_packet.z
                                            player.yaw = parsed_packet.yaw
                                            player.pitch = parsed_packet.pitch
                                        
                                        # Update player state and handle chunk loading (same as position only)
                                        if world and player:
                                            chunk_change = player.update_position(
                                                parsed_packet.x, parsed_packet.y, parsed_packet.z
                                            )
                                            
                                            # Check for item pickups (entities are updated by background thread)
                                            items_to_pickup = player.check_item_pickups(world.item_entities)
                                            if items_to_pickup:
                                                for item_entity in items_to_pickup:
                                                    try:
                                                        # Send Pickup Item packet (for animation)
                                                        pickup_packet = PacketBuilder.build_pickup_item(
                                                            collected_entity_id=item_entity.entity_id,
                                                            collector_entity_id=1,  # Player entity ID is usually 1
                                                            pickup_count=item_entity.count
                                                        )
                                                        client_socket.sendall(pickup_packet)
                                                        
                                                        # Send Destroy Entities packet (to remove from world)
                                                        destroy_packet = PacketBuilder.build_destroy_entities([item_entity.entity_id])
                                                        client_socket.sendall(destroy_packet)
                                                        
                                                        # Find a slot for the item
                                                        slot_idx = player.find_slot_for_item(item_entity.item_id, item_entity.count)
                                                        
                                                        if slot_idx is not None:
                                                            # Determine final item ID and count
                                                            if slot_idx in player.inventory_slots:
                                                                # Stacking with existing items
                                                                existing_item_id, existing_count = player.inventory_slots[slot_idx]
                                                                new_count = existing_count + item_entity.count
                                                                # Cap at stack size of 64
                                                                if new_count > 64:
                                                                    new_count = 64
                                                                final_item_id = existing_item_id
                                                                final_count = new_count
                                                            else:
                                                                # New slot
                                                                final_item_id = item_entity.item_id
                                                                final_count = item_entity.count
                                                            
                                                            # Update slot tracking
                                                            player.update_slot(slot_idx, final_item_id, final_count)
                                                            
                                                            # Increment state ID
                                                            player.inventory_state_id += 1
                                                            
                                                            # Send Set Container Slot packet to update client inventory
                                                            container_slot_packet = PacketBuilder.build_set_container_slot(
                                                                window_id=0,  # 0 = player inventory
                                                                state_id=player.inventory_state_id,
                                                                slot=slot_idx,
                                                                item_id=final_item_id,
                                                                count=final_count
                                                            )
                                                            client_socket.sendall(container_slot_packet)
                                                            
                                                            # Add to server-side inventory tracking
                                                            player.add_to_inventory(item_entity.item_id, item_entity.count)
                                                            
                                                            print(f"  │  ✓ Item picked up (Entity ID: {item_entity.entity_id}, Item ID: {item_entity.item_id}, Count: {item_entity.count}, Slot: {slot_idx})")
                                                        else:
                                                            print(f"  │  ⚠ Inventory full, item not picked up (Entity ID: {item_entity.entity_id})")
                                                        
                                                        # Remove from tracking
                                                        world.remove_item_entity(item_entity.entity_id)
                                                    except Exception as e:
                                                        print(f"  │  ✗ Error picking up item {item_entity.entity_id}: {e}")
                                            
                                            if chunk_change:
                                                old_chunk, new_chunk = chunk_change
                                                print(f"  │  → Player crossed chunk boundary: {old_chunk} → {new_chunk}")
                                                
                                                # Send Set Center Chunk
                                                try:
                                                    center_chunk = PacketBuilder.build_set_center_chunk(
                                                        chunk_x=new_chunk[0],
                                                        chunk_z=new_chunk[1]
                                                    )
                                                    client_socket.sendall(center_chunk)
                                                    print(f"  │  ✓ Set Center Chunk sent ({len(center_chunk)} bytes)")
                                                except Exception as e:
                                                    print(f"  │  ✗ Error sending Set Center Chunk: {e}")
                                                
                                                # Queue new chunks for async loading
                                                chunks_to_load = player.get_chunks_to_load()
                                                if chunks_to_load:
                                                    print(f"  │  → Queueing {len(chunks_to_load)} new chunk(s) for async loading...")
                                                    chunk_loader.queue_chunks(chunks_to_load, center_chunk=new_chunk)
                                                
                                                # Queue distant chunks for unloading
                                                chunks_to_unload = player.get_chunks_to_unload()
                                                if chunks_to_unload:
                                                    print(f"  │  → Queueing {len(chunks_to_unload)} distant chunk(s) for unloading...")
                                                    chunk_loader.queue_unload(chunks_to_unload)
                                        
                                        print(f"  └─")
                            
                            else:
                                print(f"  │  Type: {type(parsed_packet).__name__}")
                                print(f"  │  Data: {parsed_packet}")
                            
                            print(f"  └─")
                        else:
                            # Handle known packets that return None
                            if connection_state == ConnectionState.LOGIN and parsed_packet_id == 3:
                                print(f"  ┌─ Parsed Packet Data:")
                                print(f"  │  Type: Login Acknowledged")
                                print(f"  │  → Login complete! Transitioning to CONFIGURATION state")
                                connection_state = ConnectionState.CONFIGURATION
                                print(f"  └─")
                            elif connection_state == ConnectionState.CONFIGURATION and parsed_packet_id == 3:
                                print(f"  ┌─ Parsed Packet Data:")
                                print(f"  │  Type: Acknowledge Finish Configuration")
                                print(f"  │  → Configuration complete! Transitioning to PLAY state")
                                connection_state = ConnectionState.PLAY
                                
                                # Initialize world state
                                world = World(view_distance=10, use_terrain_generation=True)                                
                                # Start web server for visualization (if not already started)
                                if web_server_thread is None or not web_server_thread.is_alive():
                                    web_server_thread = threading.Thread(
                                        target=run_web_server,
                                        args=('127.0.0.1', 5000, world),
                                        daemon=True
                                    )
                                    web_server_thread.start()
                                    print(f"  │  ✓ Web visualization server started at http://127.0.0.1:5000")
                                
                                # Create player instance
                                if player_uuid is None:
                                    print(f"  │  ⚠ Warning: Player UUID not set, using default UUID")
                                    player_uuid = uuid.uuid4()
                                
                                player = Player(player_uuid, view_distance=10, world=world)
                                player.update_position(0.0, 65.0, 0.0)  # Spawn position
                                world.add_player(player)
                                
                                # Loot tables should already be loaded during server initialization
                                # Just verify they're available (should be instant since already cached)
                                if not hasattr(load_loot_tables, '_loot_table_cache') or not load_loot_tables._loot_table_cache:
                                    print(f"  │  ⚠ Warning: Loot tables not pre-loaded, loading now...")
                                    load_loot_tables()
                                
                                # Initialize chunk loader (background thread)
                                chunk_loader = ChunkLoader(client_socket, player, keep_alive_stop_event)
                                chunk_loader.start()
                                print(f"  │  ✓ Chunk loader thread started")
                                
                                # Send Login (play) packet
                                print(f"  │  → Sending Login (play) packet...")
                                try:
                                    login_play = PacketBuilder.build_login_play(
                                        entity_id=1,
                                        dimension_names=["minecraft:overworld"],
                                        game_mode=0,  # Survival
                                        dimension_name="minecraft:overworld"
                                    )
                                    client_socket.sendall(login_play)
                                    print(f"  │  ✓ Login (play) sent ({len(login_play)} bytes)")
                                    
                                    # Send Synchronize Player Position (spawn at 0, 65, 0 - on top of grass at y=64)
                                    print(f"  │  → Sending Synchronize Player Position...")
                                    try:
                                        player_pos = PacketBuilder.build_synchronize_player_position(
                                            x=0.0,
                                            y=65.0,  # On top of grass at y=64
                                            z=0.0,
                                            yaw=0.0,
                                            pitch=0.0,
                                            teleport_id=0
                                        )
                                        client_socket.sendall(player_pos)
                                        print(f"  │  ✓ Player Position sent ({len(player_pos)} bytes)")
                                    except Exception as pos_error:
                                        print(f"  │  ✗ Error sending Player Position: {pos_error}")
                                        import traceback
                                        traceback.print_exc()
                                    
                                    # Send Update Time
                                    print(f"  │  → Sending Update Time...")
                                    try:
                                        update_time = PacketBuilder.build_update_time(
                                            world_age=0,
                                            time_of_day=6000,  # Noon
                                            time_increasing=True
                                        )
                                        client_socket.sendall(update_time)
                                        print(f"  │  ✓ Update Time sent ({len(update_time)} bytes)")
                                    except Exception as time_error:
                                        print(f"  │  ✗ Error sending Update Time: {time_error}")
                                        import traceback
                                        traceback.print_exc()
                                    
                                    # Send Game Event (event 13: "Start waiting for level chunks")
                                    # Required for client to spawn after receiving chunks
                                    print(f"  │  → Sending Game Event (Start waiting for level chunks)...")
                                    try:
                                        game_event = PacketBuilder.build_game_event(
                                            event=13,  # Start waiting for level chunks
                                            value=0.0
                                        )
                                        client_socket.sendall(game_event)
                                        print(f"  │  ✓ Game Event sent ({len(game_event)} bytes)")
                                    except Exception as event_error:
                                        print(f"  │  ✗ Error sending Game Event: {event_error}")
                                        import traceback
                                        traceback.print_exc()
                                    
                                    # Send Set Center Chunk (spawn chunk)
                                    print(f"  │  → Sending Set Center Chunk...")
                                    try:
                                        center_chunk = PacketBuilder.build_set_center_chunk(
                                            chunk_x=0,
                                            chunk_z=0
                                        )
                                        client_socket.sendall(center_chunk)
                                        print(f"  │  ✓ Set Center Chunk sent ({len(center_chunk)} bytes)")
                                    except Exception as center_error:
                                        print(f"  │  ✗ Error sending Set Center Chunk: {center_error}")
                                        import traceback
                                        traceback.print_exc()
                                    
                                    # Send Chunk Data based on view distance
                                    # Load all chunks within view distance + buffer for neighbors
                                    # Chunk loading happens asynchronously in background thread
                                    print(f"  │  → Queueing initial chunks around spawn (view distance: {player.view_distance})...")
                                    try:
                                        # Get all chunks that should be loaded around spawn (chunk 0, 0)
                                        spawn_chunk_x, spawn_chunk_z = 0, 0
                                        chunks_to_load = player.get_chunks_in_range()
                                        
                                        # Queue chunks for async loading
                                        chunk_loader.queue_chunks(chunks_to_load, center_chunk=(spawn_chunk_x, spawn_chunk_z))
                                        print(f"  │  ✓ Queued {len(chunks_to_load)} chunks for async loading")
                                    except Exception as chunk_error:
                                        print(f"  │  ✗ Error queueing initial chunks: {chunk_error}")
                                        import traceback
                                        traceback.print_exc()
                                    
                                    print(f"  │  → Client should now be in world!")
                                    
                                    # Start keep alive thread
                                    def keep_alive_worker():
                                        """Send keep alive packets every 10 seconds."""
                                        while not keep_alive_stop_event.is_set():
                                            try:
                                                # Generate keep alive ID (timestamp in milliseconds)
                                                keep_alive_id = int(time.time() * 1000)
                                                last_keep_alive_id = keep_alive_id
                                                
                                                keep_alive_packet = PacketBuilder.build_keep_alive(keep_alive_id)
                                                client_socket.sendall(keep_alive_packet)
                                                print(f"  │  → Keep Alive sent (ID: {keep_alive_id})")
                                                
                                                # Wait 10 seconds
                                                keep_alive_stop_event.wait(10.0)
                                            except Exception as e:
                                                print(f"  │  ✗ Error sending Keep Alive: {e}")
                                                break
                                    
                                    keep_alive_thread = threading.Thread(target=keep_alive_worker, daemon=True)
                                    keep_alive_thread.start()
                                    print(f"  │  ✓ Keep Alive thread started")
                                    
                                except Exception as send_error:
                                    print(f"  │  ✗ Error sending Login (play): {send_error}")
                                    import traceback
                                    traceback.print_exc()
                                
                                print(f"  └─")
                            
                            else:
                                print(f"  └─ (Packet ID {parsed_packet_id} not recognized in {connection_state.name} state)")
                    
                    except Exception as parse_error:
                        print(f"  └─ Parse error: {parse_error}")
                        import traceback
                        traceback.print_exc()
                    
                    print()
                    
                    # Move to next packet
                    offset = total_packet_size
                    
                except Exception as e:
                    print(f"[{datetime.now().strftime('%H:%M:%S')}] Error parsing packet: {e}")
                    import traceback
                    traceback.print_exc()
                    print(f"  Buffer at offset {offset}: {format_hex(buffer[offset:offset+32])}")
                    # Skip one byte and try again
                    offset += 1
                    if offset >= len(buffer):
                        break
            
            # Keep remaining data in buffer
            buffer = buffer[offset:]
            
    except Exception as e:
        print(f"[{datetime.now().strftime('%H:%M:%S')}] Error handling client: {e}")
        import traceback
        traceback.print_exc()
    finally:
        # Stop keep alive thread
        if keep_alive_thread:
            keep_alive_stop_event.set()
            keep_alive_thread.join(timeout=1.0)
        client_socket.close()
        print(f"[{datetime.now().strftime('%H:%M:%S')}] Connection closed\n")

def initialize_server_data():
    """
    Initialize server data by pre-loading caches from extracted_data/.
    This should be called once before the server starts accepting connections.
    """
    print(f"{'='*60}")
    print(f"Initializing Server Data")
    print(f"{'='*60}")
    
    script_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    extracted_data_dir = os.path.join(script_dir, 'extracted_data')
    
    # Check if extracted_data exists
    if not os.path.exists(extracted_data_dir):
        print(f"⚠ Warning: extracted_data/ directory not found at {extracted_data_dir}")
        print(f"  Run src/extract_server_data.py to generate extracted data")
        print(f"  Some features may not work correctly.")
        return False
    
    print(f"✓ Using extracted data from {extracted_data_dir}")
    
    # Step 1: Pre-load loot tables
    print(f"→ Pre-loading loot tables...")
    try:
        load_loot_tables()
        loot_count = len(load_loot_tables._loot_table_cache) if hasattr(load_loot_tables, '_loot_table_cache') else 0
        print(f"✓ Loaded {loot_count} loot table mappings")
    except Exception as e:
        print(f"⚠ Warning: Could not pre-load loot tables: {e}")
    
    # Step 2: Pre-load item registry (for item IDs)
    print(f"→ Pre-loading item registry...")
    try:
        # Trigger item ID cache loading
        get_item_id_from_name('minecraft:dirt')  # This will load the cache
        item_count = len(get_item_id_from_name._item_id_cache) if hasattr(get_item_id_from_name, '_item_id_cache') else 0
        print(f"✓ Loaded {item_count} item IDs")
    except Exception as e:
        print(f"⚠ Warning: Could not pre-load item registry: {e}")
    
    # Step 3: Pre-load entity type registry
    print(f"→ Pre-loading entity type registry...")
    try:
        # Trigger entity type cache loading
        get_entity_type_id('minecraft:item')  # This will load the cache
        entity_count = len(get_entity_type_id._entity_type_cache) if hasattr(get_entity_type_id, '_entity_type_cache') else 0
        print(f"✓ Loaded {entity_count} entity type IDs")
    except Exception as e:
        print(f"⚠ Warning: Could not pre-load entity type registry: {e}")
    
    print(f"{'='*60}\n")
    return True


def main():
    """Main server function."""
    # Initialize server data before starting
    initialize_server_data()
    
    host = '0.0.0.0'  # Listen on all interfaces
    port = 25565
    
    server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    server_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    
    try:
        server_socket.bind((host, port))
        server_socket.listen(5)
        print(f"{'='*60}")
        print(f"Minecraft Packet Debug Server")
        print(f"Listening on {host}:{port}")
        print(f"Waiting for connections...")
        print(f"{'='*60}\n")
        
        while True:
            client_socket, client_address = server_socket.accept()
            
            # Handle each client in a separate thread
            client_thread = threading.Thread(
                target=handle_client,
                args=(client_socket, client_address)
            )
            client_thread.daemon = True
            client_thread.start()
            
    except KeyboardInterrupt:
        print("\n\nShutting down server...")
    except Exception as e:
        print(f"Server error: {e}")
    finally:
        server_socket.close()
        print("Server closed")

if __name__ == "__main__":
    main()

