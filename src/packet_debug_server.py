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
import zipfile
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
                chunk_data = PacketBuilder.build_chunk_data(
                    chunk_x=chunk_x,
                    chunk_z=chunk_z,
                    flat_world=True,
                    ground_y=64
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
    
    def __init__(self, player_uuid: uuid.UUID, view_distance: int = 10):
        """
        Initialize a player.
        
        Args:
            player_uuid: Unique identifier for the player
            view_distance: Server view distance for this player
        """
        self.uuid = player_uuid
        
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
    
    def __init__(self, view_distance: int = 10):
        """
        Initialize world state.
        
        Args:
            view_distance: Server view distance
        """
        # Player management
        self.players: Dict[uuid.UUID, Player] = {}  # Dictionary of player UUID -> Player instance
        
        # Entity management
        self.next_entity_id = 1000  # Start entity IDs at 1000 (player is usually 1)
        self.item_entities: Dict[int, ItemEntity] = {}  # Track all item entities: entity_id -> ItemEntity
    
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
        Handles large time deltas by updating in multiple steps.
        
        Args:
            delta_time: Time step in seconds (default 0.05 = 1 tick at 20 TPS)
        """
        GRAVITY = -0.04  # Minecraft gravity per tick (blocks per tick^2)
        DRAG = 0.98  # Air resistance factor per tick
        MAX_STEP = 0.05  # Maximum time step per iteration (1 tick)
        
        current_time = time.time()
        
        for entity_id, item_entity in list(self.item_entities.items()):
            # Calculate time since last update
            if item_entity.last_update_time == 0.0:
                item_entity.last_update_time = current_time
                continue
            
            total_delta = current_time - item_entity.last_update_time
            
            # Update in multiple steps if delta is large (catch up on missed updates)
            remaining_delta = total_delta
            while remaining_delta > 0:
                # Use smaller step size for accuracy
                step_delta = min(remaining_delta, MAX_STEP)
                
                # Update velocity (apply gravity and drag)
                item_entity.velocity_y += GRAVITY * step_delta / 0.05  # Scale to tick-based gravity
                item_entity.velocity_x *= DRAG ** (step_delta / 0.05)
                item_entity.velocity_y *= DRAG ** (step_delta / 0.05)
                item_entity.velocity_z *= DRAG ** (step_delta / 0.05)
                
                # Update position based on velocity
                item_entity.x += item_entity.velocity_x * step_delta
                item_entity.y += item_entity.velocity_y * step_delta
                item_entity.z += item_entity.velocity_z * step_delta
                
                # Simple ground collision (prevent falling below y=64 for our flat world)
                if item_entity.y < 64.0:
                    item_entity.y = 64.0
                    item_entity.velocity_y = 0.0
                    # Bounce slightly on ground (damping)
                    item_entity.velocity_x *= 0.7
                    item_entity.velocity_z *= 0.7
                
                remaining_delta -= step_delta
            
            item_entity.last_update_time = current_time
    
    def remove_item_entity(self, entity_id: int):
        """Remove an item entity from tracking."""
        self.item_entities.pop(entity_id, None)


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
    
    registries_file = os.path.join(os.path.dirname(os.path.dirname(__file__)), 'generated', 'reports', 'registries.json')
    
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
    Load block loot tables from the server JAR.
    Returns a dictionary mapping block names to item names.
    Thread-safe: if loading is in progress, returns empty dict to avoid blocking.
    """
    import zipfile
    import json
    
    script_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    jar_path = os.path.join(script_dir, 'data', 'server-1-21-10.jar')
    inner_jar_path = os.path.join(script_dir, 'data', 'temp_inner_server.jar')
    
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
        
        if not os.path.exists(jar_path):
            print(f"  │  ⚠ Warning: Server JAR not found at {jar_path}, cannot load loot tables")
        else:
            # Extract inner JAR if needed
            if not os.path.exists(inner_jar_path):
                print(f"  │  → Extracting inner JAR for loot tables...")
                try:
                    with zipfile.ZipFile(jar_path, 'r') as jar:
                        inner_jar_data = jar.read('META-INF/versions/1.21.10/server-1.21.10.jar')
                        with open(inner_jar_path, 'wb') as f:
                            f.write(inner_jar_data)
                except Exception as e:
                    print(f"  │  ⚠ Warning: Could not extract inner JAR: {e}")
            
            # Extract all block loot tables
            if os.path.exists(inner_jar_path):
                try:
                    with zipfile.ZipFile(inner_jar_path, 'r') as inner_jar:
                        all_files = inner_jar.namelist()
                        block_loot_tables = [f for f in all_files 
                                           if f.startswith('data/minecraft/loot_table/blocks/') 
                                           and f.endswith('.json')]
                        
                        print(f"  │  → Loading {len(block_loot_tables)} block loot tables...")
                        
                        for loot_file in block_loot_tables:
                            try:
                                loot_data = inner_jar.read(loot_file)
                                loot_json = json.loads(loot_data.decode('utf-8'))
                                
                                # Extract block name from filename
                                block_name = loot_file.split('/')[-1].replace('.json', '')
                                block_id = f"minecraft:{block_name}"
                                
                                # Parse loot table to find default item drop
                                # For now, we'll use a simple approach:
                                # 1. Look for simple item entries (no alternatives)
                                # 2. For alternatives, use the first non-silk-touch option
                                item_name = None
                                
                                if 'pools' in loot_json and len(loot_json['pools']) > 0:
                                    pool = loot_json['pools'][0]  # Use first pool
                                    
                                    if 'entries' in pool and len(pool['entries']) > 0:
                                        entry = pool['entries'][0]
                                        
                                        # Handle simple item entry
                                        if entry.get('type') == 'minecraft:item':
                                            item_name = entry.get('name')
                                        
                                        # Handle alternatives (e.g., silk touch vs normal)
                                        elif entry.get('type') == 'minecraft:alternatives':
                                            if 'children' in entry:
                                                # Find first item that's not silk touch
                                                for child in entry['children']:
                                                    if child.get('type') == 'minecraft:item':
                                                        # Check if it has silk touch condition
                                                        has_silk_touch = False
                                                        if 'conditions' in child:
                                                            for condition in child['conditions']:
                                                                if condition.get('condition') == 'minecraft:match_tool':
                                                                    has_silk_touch = True
                                                                    break
                                                        
                                                        if not has_silk_touch:
                                                            item_name = child.get('name')
                                                            break
                                                
                                                # If no non-silk-touch found, use first item
                                                if item_name is None and len(entry['children']) > 0:
                                                    first_child = entry['children'][0]
                                                    if first_child.get('type') == 'minecraft:item':
                                                        item_name = first_child.get('name')
                                
                                if item_name:
                                    load_loot_tables._loot_table_cache[block_id] = item_name
                            
                            except Exception as e:
                                # Skip invalid loot tables
                                continue
                        
                        print(f"  │  ✓ Loaded {len(load_loot_tables._loot_table_cache)} loot table mappings")
                
                except Exception as e:
                    print(f"  │  ⚠ Warning: Could not load loot tables: {e}")
    finally:
        load_loot_tables._is_loading = False
        load_loot_tables._loading_lock.release()
    
    return load_loot_tables._loot_table_cache


def get_item_for_block(block_name: str) -> str:
    """
    Get the item name that should drop from a block using loot tables.
    
    Args:
        block_name: Block identifier (e.g., 'minecraft:grass_block', 'minecraft:stone')
    
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
    
    registries_file = os.path.join(os.path.dirname(os.path.dirname(__file__)), 'generated', 'reports', 'registries.json')
    
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
    
    registries_file = os.path.join(os.path.dirname(os.path.dirname(__file__)), 'generated', 'reports', 'registries.json')
    
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
    
    # Map block state ID to block name (simplified - we'd need block registry for this)
    # For now, use a simple heuristic:
    # - Block state ID 1 is likely stone
    # - Block state ID ~10 is likely grass_block
    # In a real implementation, we'd look up the block registry
    
    # Simple mapping based on common block state IDs
    if block_state_id == 1:
        item_name = 'minecraft:stone'
    elif block_state_id == 10:  # Approximate grass_block block state ID
        item_name = 'minecraft:grass_block'
    else:
        # Default: try to infer from block state ID
        # This is a fallback - ideally we'd have a proper block state -> block name -> item name mapping
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
                            
                            elif isinstance(parsed_packet, list) and connection_state == ConnectionState.CONFIGURATION and parsed_packet_id == 0x07:
                                # Serverbound Known Packs (parsed as list)
                                print(f"  │  Type: Serverbound Known Packs")
                                packs = parsed_packet
                                print(f"  │  Client knows {len(packs)} pack(s):")
                                for namespace, pack_id, version in packs:
                                    print(f"  │    - {namespace}:{pack_id} (version {version})")
                                known_packs_received = True
                                
                                # Load registry data from JSON file
                                registry_data_file = os.path.join(os.path.dirname(os.path.dirname(__file__)), 'data', 'registry_data.json')
                                registry_json_data = {}
                                if os.path.exists(registry_data_file):
                                    try:
                                        with open(registry_data_file, 'r') as f:
                                            registry_json_data = json.load(f)
                                    except Exception as e:
                                        print(f"  │  ⚠ Warning: Could not load registry_data.json: {e}")
                                
                                # Extract entries from server JAR (must be defined before get_registry_entries)
                                def get_jar_entries(registry_path, registry_name):
                                    """Extract entries from server JAR for a given registry path.
                                    Note: Inner JAR should already be extracted during server initialization.
                                    This function will extract it as a fallback if needed.
                                    """
                                    script_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
                                    jar_path = os.path.join(script_dir, 'data', 'server-1-21-10.jar')
                                    inner_jar_path = os.path.join(script_dir, 'data', 'temp_inner_server.jar')
                                    
                                    if not os.path.exists(jar_path):
                                        return []
                                    
                                    # Inner JAR should already be extracted during initialization
                                    # Fallback: extract now if not done during init
                                    if not os.path.exists(inner_jar_path):
                                        try:
                                            with zipfile.ZipFile(jar_path, 'r') as jar:
                                                inner_jar_data = jar.read('META-INF/versions/1.21.10/server-1.21.10.jar')
                                                with open(inner_jar_path, 'wb') as f:
                                                    f.write(inner_jar_data)
                                        except Exception:
                                            return []
                                    
                                    try:
                                        with zipfile.ZipFile(inner_jar_path, 'r') as inner_jar:
                                            all_files = inner_jar.namelist()
                                            registry_files = [f for f in all_files 
                                                            if registry_path in f 
                                                            and f.endswith('.json') 
                                                            and '/tags/' not in f]
                                            
                                            entries = []
                                            for f in registry_files:
                                                filename = os.path.basename(f)
                                                entry_name = filename.replace('.json', '')
                                                entries.append(f"minecraft:{entry_name}")
                                            
                                            return sorted(list(set(entries)))
                                    except Exception:
                                        return []
                                
                                def get_biome_entries():
                                    """Extract all biome entries from server JAR."""
                                    return get_jar_entries('worldgen/biome/', 'biome')
                                
                                def get_damage_type_entries():
                                    """Extract all damage_type entries from server JAR."""
                                    return get_jar_entries('damage_type/', 'damage_type')
                                
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
                                            
                                            # Update item entity positions (simulate physics)
                                            world.update_item_entities()
                                            
                                            # Check for item pickups
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
                                            # For now, we'll infer the block from Y coordinate
                                            # In a real implementation, we'd track what block was actually there
                                            # Our flat world: grass_block at y=64, stone at y=63
                                            if world:
                                                # Determine block name from position (heuristic for flat world)
                                                block_name = None
                                                if y == 64:
                                                    block_name = 'minecraft:grass_block'
                                                elif y == 63:
                                                    block_name = 'minecraft:stone'
                                                else:
                                                    # Default to stone for other positions
                                                    block_name = 'minecraft:stone'
                                                
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
                                                    
                                                    # For now, place a simple block (we'd need to map item_id to block_state_id)
                                                    # Using a placeholder block state ID - in a real implementation,
                                                    # we'd look up the block state ID from the item ID
                                                    # For now, just place stone (block state ID ~1) as a placeholder
                                                    block_state_id = 1  # Placeholder - should map item_id to block_state_id
                                                    
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
                                            
                                            # Update item entity positions (simulate physics)
                                            world.update_item_entities()
                                            
                                            # Check for item pickups
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
                                world = World(view_distance=10)
                                
                                # Create player instance
                                if player_uuid is None:
                                    print(f"  │  ⚠ Warning: Player UUID not set, using default UUID")
                                    player_uuid = uuid.uuid4()
                                
                                player = Player(player_uuid, view_distance=10)
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
    Initialize server data by extracting JAR files and pre-loading caches.
    This should be called once before the server starts accepting connections.
    """
    print(f"{'='*60}")
    print(f"Initializing Server Data")
    print(f"{'='*60}")
    
    script_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    jar_path = os.path.join(script_dir, 'data', 'server-1-21-10.jar')
    inner_jar_path = os.path.join(script_dir, 'data', 'temp_inner_server.jar')
    
    # Step 1: Extract inner JAR if needed
    if not os.path.exists(jar_path):
        print(f"⚠ Warning: Server JAR not found at {jar_path}")
        print(f"  Some features may not work correctly.")
        return False
    
    if not os.path.exists(inner_jar_path):
        print(f"→ Extracting inner JAR from server JAR...")
        try:
            with zipfile.ZipFile(jar_path, 'r') as jar:
                inner_jar_data = jar.read('META-INF/versions/1.21.10/server-1.21.10.jar')
                with open(inner_jar_path, 'wb') as f:
                    f.write(inner_jar_data)
            print(f"✓ Inner JAR extracted to {inner_jar_path}")
        except Exception as e:
            print(f"✗ Error extracting inner JAR: {e}")
            return False
    else:
        print(f"✓ Inner JAR already exists at {inner_jar_path}")
    
    # Step 2: Pre-load loot tables
    print(f"→ Pre-loading loot tables...")
    try:
        load_loot_tables()
        loot_count = len(load_loot_tables._loot_table_cache) if hasattr(load_loot_tables, '_loot_table_cache') else 0
        print(f"✓ Loaded {loot_count} loot table mappings")
    except Exception as e:
        print(f"⚠ Warning: Could not pre-load loot tables: {e}")
    
    # Step 3: Pre-load item registry (for item IDs)
    print(f"→ Pre-loading item registry...")
    try:
        # Trigger item ID cache loading
        get_item_id_from_name('minecraft:stone')  # This will load the cache
        item_count = len(get_item_id_from_name._item_id_cache) if hasattr(get_item_id_from_name, '_item_id_cache') else 0
        print(f"✓ Loaded {item_count} item IDs")
    except Exception as e:
        print(f"⚠ Warning: Could not pre-load item registry: {e}")
    
    # Step 4: Pre-load entity type registry
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

