#!/usr/bin/env python3
"""
Enhanced TCP server for debugging Minecraft protocol packets.
Listens on port 25565 and prints both raw hex and parsed packet data.
"""

import socket
import threading
import json
import os
import zipfile
from datetime import datetime
from minecraft_protocol import (
    PacketParser, ConnectionState,
    HandshakePacket, LoginStartPacket,
    ClientInformationPacket,
    SetPlayerPositionPacket,
    SetPlayerPositionAndRotationPacket,
    KeepAlivePacket,
    PlayerActionPacket,
    PacketBuilder, GameProfile
)
import uuid
import time
import threading

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


class PlayerState:
    """Tracks player state including position and loaded chunks."""
    
    def __init__(self, view_distance: int = 10):
        """
        Initialize player state.
        
        Args:
            view_distance: Server view distance
        """
        self.x = 0.0
        self.y = 64.0
        self.z = 0.0
        self.chunk_x = 0
        self.chunk_z = 0
        self.loaded_chunks = set()  # Set of (chunk_x, chunk_z) tuples
        self.chunk_manager = ChunkManager(view_distance=view_distance)
        self.last_center_chunk = (0, 0)
        self.view_distance = view_distance
        self.next_entity_id = 1000  # Start entity IDs at 1000 (player is usually 1)
    
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


def get_item_id_for_block(block_state_id: int) -> int:
    """
    Map block state ID to item ID.
    This is a simplified mapping - in reality, block states and items have separate registries.
    For now, we'll use approximate mappings:
    - Grass block (block state ~10) -> grass_block item (~10)
    - Stone (block state ~1) -> stone item (~1)
    - Default: return block_state_id as item_id
    """
    # Simplified mapping - in a real implementation, you'd look up the item registry
    # For now, we'll assume block state IDs roughly correspond to item IDs
    # Common blocks in our world:
    # - Air = 0 -> no drop
    # - Stone = ~1 -> stone item (~1)
    # - Grass block = ~10 -> grass_block item (~10)
    
    if block_state_id == 0:  # Air
        return None  # No item drop
    
    # For now, return the block state ID as the item ID
    # In a real implementation, you'd need to look up the item registry
    return block_state_id


def handle_client(client_socket, client_address):
    """Handle a single client connection."""
    print(f"\n{'='*60}")
    print(f"[{datetime.now().strftime('%H:%M:%S')}] New connection from {client_address}")
    print(f"{'='*60}\n")
    
    # Track connection state
    connection_state = ConnectionState.HANDSHAKING
    known_packs_sent = False
    known_packs_received = False
    
    # Initialize player state for chunk management
    player_state = None  # Will be initialized when entering PLAY state
    
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
                                registry_data_file = os.path.join(os.path.dirname(__file__), 'data', 'registry_data.json')
                                registry_json_data = {}
                                if os.path.exists(registry_data_file):
                                    try:
                                        with open(registry_data_file, 'r') as f:
                                            registry_json_data = json.load(f)
                                    except Exception as e:
                                        print(f"  │  ⚠ Warning: Could not load registry_data.json: {e}")
                                
                                # Extract entries from server JAR (must be defined before get_registry_entries)
                                def get_jar_entries(registry_path, registry_name):
                                    """Extract entries from server JAR for a given registry path."""
                                    jar_path = os.path.join(os.path.dirname(__file__), 'data', 'server-1-21-10.jar')
                                    inner_jar_path = os.path.join(os.path.dirname(__file__), 'data', 'temp_inner_server.jar')
                                    
                                    if not os.path.exists(jar_path):
                                        return []
                                    
                                    # Extract inner JAR if needed
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
                                        if player_state:
                                            chunk_change = player_state.update_position(
                                                parsed_packet.x, parsed_packet.y, parsed_packet.z
                                            )
                                            
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
                                                
                                                # Load new chunks
                                                # Strategy: Load chunks in order of distance from center, ensuring neighbors
                                                chunks_to_load = player_state.get_chunks_to_load()
                                                if chunks_to_load:
                                                    print(f"  │  → Loading {len(chunks_to_load)} new chunk(s)...")
                                                    
                                                    # Sort chunks by distance from center (closer first)
                                                    center_x, center_z = new_chunk
                                                    chunks_to_load.sort(key=lambda c: abs(c[0] - center_x) + abs(c[1] - center_z))
                                                    
                                                    chunks_sent = 0
                                                    for chunk_x, chunk_z in chunks_to_load:
                                                        try:
                                                            chunk_data = PacketBuilder.build_chunk_data(
                                                                chunk_x=chunk_x,
                                                                chunk_z=chunk_z,
                                                                flat_world=True,
                                                                ground_y=64
                                                            )
                                                            client_socket.sendall(chunk_data)
                                                            player_state.mark_chunk_loaded(chunk_x, chunk_z)
                                                            chunks_sent += 1
                                                            print(f"  │  ✓ Chunk ({chunk_x}, {chunk_z}) loaded ({len(chunk_data)} bytes)")
                                                        except Exception as e:
                                                            print(f"  │  ✗ Error loading chunk ({chunk_x}, {chunk_z}): {e}")
                                                    
                                                    # After loading, send Set Center Chunk again to ensure client updates view
                                                    try:
                                                        center_chunk = PacketBuilder.build_set_center_chunk(
                                                            chunk_x=new_chunk[0],
                                                            chunk_z=new_chunk[1]
                                                        )
                                                        client_socket.sendall(center_chunk)
                                                        print(f"  │  ✓ Set Center Chunk resent after loading chunks")
                                                    except Exception as e:
                                                        print(f"  │  ✗ Error resending Set Center Chunk: {e}")
                                                    
                                                    print(f"  │  ✓ Loaded {chunks_sent} chunk(s)")
                                                
                                                # Unload distant chunks (optional, for memory management)
                                                chunks_to_unload = player_state.get_chunks_to_unload()
                                                if chunks_to_unload:
                                                    print(f"  │  → Unloading {len(chunks_to_unload)} distant chunk(s)...")
                                                    for chunk_x, chunk_z in chunks_to_unload:
                                                        player_state.mark_chunk_unloaded(chunk_x, chunk_z)
                                                    print(f"  │  ✓ Unloaded {len(chunks_to_unload)} chunk(s)")
                                        
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
                                            
                                            # Spawn item drop
                                            # For now, we'll assume we're breaking grass or stone
                                            # In a real implementation, we'd track what block was there
                                            # Common blocks: grass_block (~10), stone (~1)
                                            # We'll default to grass_block item for blocks at y=64, stone for y=63
                                            if player_state:
                                                # Determine item to drop based on Y coordinate
                                                # This is a simplified approach - in reality, we'd track block types
                                                if y == 64:
                                                    item_id = 10  # grass_block item (approximate)
                                                elif y == 63:
                                                    item_id = 1  # stone item (approximate)
                                                else:
                                                    item_id = 1  # Default to stone
                                                
                                                try:
                                                    # Generate entity ID
                                                    entity_id = player_state.next_entity_id
                                                    player_state.next_entity_id += 1
                                                    
                                                    # Generate UUID for the item entity
                                                    import uuid as uuid_module
                                                    item_uuid = uuid_module.uuid4()
                                                    
                                                    # Calculate spawn position (center of block + small offset)
                                                    spawn_x = x + 0.5
                                                    spawn_y = y + 0.5
                                                    spawn_z = z + 0.5
                                                    
                                                    # Calculate random velocity for natural drop
                                                    import random
                                                    velocity_x = (random.random() - 0.5) * 0.1
                                                    velocity_y = 0.1  # Slight upward velocity
                                                    velocity_z = (random.random() - 0.5) * 0.1
                                                    
                                                    # Spawn item entity (entity type 2 = item)
                                                    # Data field is 0 for item entities (item stack set via Entity Metadata)
                                                    spawn_packet = PacketBuilder.build_spawn_entity(
                                                        entity_id=entity_id,
                                                        entity_uuid=item_uuid,
                                                        entity_type=2,  # Item entity type
                                                        x=spawn_x,
                                                        y=spawn_y,
                                                        z=spawn_z,
                                                        velocity_x=velocity_x,
                                                        velocity_y=velocity_y,
                                                        velocity_z=velocity_z,
                                                        pitch=0.0,
                                                        yaw=0.0,
                                                        head_yaw=0.0
                                                    )
                                                    client_socket.sendall(spawn_packet)
                                                    print(f"  │  ✓ Item entity spawned (ID: {entity_id})")
                                                    
                                                    # Send Entity Metadata to set the item stack
                                                    # For item entities, index 8 is the item stack (Slot type 7)
                                                    metadata_packet = PacketBuilder.build_set_entity_metadata(
                                                        entity_id=entity_id,
                                                        metadata=[
                                                            (8, 7, (item_id, 1))  # Index 8, type 7 (Slot), (item_id, count)
                                                        ]
                                                    )
                                                    client_socket.sendall(metadata_packet)
                                                    print(f"  │  ✓ Item metadata sent (Item ID: {item_id}, Count: 1)")
                                                except Exception as e:
                                                    print(f"  │  ✗ Error spawning item: {e}")
                                                    import traceback
                                                    traceback.print_exc()
                                        
                                        print(f"  └─")
                                
                                elif parsed_packet_id == 0x1E:  # Set Player Position and Rotation
                                    if isinstance(parsed_packet, SetPlayerPositionAndRotationPacket):
                                        print(f"  │  Type: Set Player Position and Rotation")
                                        print(f"  │  Position: ({parsed_packet.x:.2f}, {parsed_packet.y:.2f}, {parsed_packet.z:.2f})")
                                        print(f"  │  Rotation: yaw={parsed_packet.yaw:.2f}, pitch={parsed_packet.pitch:.2f}")
                                        
                                        # Update player state and handle chunk loading (same as position only)
                                        if player_state:
                                            chunk_change = player_state.update_position(
                                                parsed_packet.x, parsed_packet.y, parsed_packet.z
                                            )
                                            
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
                                                
                                                # Load new chunks
                                                # Strategy: Load chunks in order of distance from center, ensuring neighbors
                                                chunks_to_load = player_state.get_chunks_to_load()
                                                if chunks_to_load:
                                                    print(f"  │  → Loading {len(chunks_to_load)} new chunk(s)...")
                                                    
                                                    # Sort chunks by distance from center (closer first)
                                                    center_x, center_z = new_chunk
                                                    chunks_to_load.sort(key=lambda c: abs(c[0] - center_x) + abs(c[1] - center_z))
                                                    
                                                    chunks_sent = 0
                                                    for chunk_x, chunk_z in chunks_to_load:
                                                        try:
                                                            chunk_data = PacketBuilder.build_chunk_data(
                                                                chunk_x=chunk_x,
                                                                chunk_z=chunk_z,
                                                                flat_world=True,
                                                                ground_y=64
                                                            )
                                                            client_socket.sendall(chunk_data)
                                                            player_state.mark_chunk_loaded(chunk_x, chunk_z)
                                                            chunks_sent += 1
                                                            print(f"  │  ✓ Chunk ({chunk_x}, {chunk_z}) loaded ({len(chunk_data)} bytes)")
                                                        except Exception as e:
                                                            print(f"  │  ✗ Error loading chunk ({chunk_x}, {chunk_z}): {e}")
                                                    
                                                    # After loading, send Set Center Chunk again to ensure client updates view
                                                    try:
                                                        center_chunk = PacketBuilder.build_set_center_chunk(
                                                            chunk_x=new_chunk[0],
                                                            chunk_z=new_chunk[1]
                                                        )
                                                        client_socket.sendall(center_chunk)
                                                        print(f"  │  ✓ Set Center Chunk resent after loading chunks")
                                                    except Exception as e:
                                                        print(f"  │  ✗ Error resending Set Center Chunk: {e}")
                                                    
                                                    print(f"  │  ✓ Loaded {chunks_sent} chunk(s)")
                                                
                                                # Unload distant chunks (optional, for memory management)
                                                chunks_to_unload = player_state.get_chunks_to_unload()
                                                if chunks_to_unload:
                                                    print(f"  │  → Unloading {len(chunks_to_unload)} distant chunk(s)...")
                                                    for chunk_x, chunk_z in chunks_to_unload:
                                                        player_state.mark_chunk_unloaded(chunk_x, chunk_z)
                                                    print(f"  │  ✓ Unloaded {len(chunks_to_unload)} chunk(s)")
                                        
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
                                
                                # Initialize player state for chunk management
                                player_state = PlayerState(view_distance=10)
                                player_state.update_position(0.0, 65.0, 0.0)  # Spawn position
                                
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
                                    print(f"  │  → Loading initial chunks around spawn (view distance: {player_state.view_distance})...")
                                    try:
                                        # Get all chunks that should be loaded around spawn (chunk 0, 0)
                                        spawn_chunk_x, spawn_chunk_z = 0, 0
                                        chunks_to_load = player_state.get_chunks_in_range()
                                        
                                        # Sort chunks by distance from center (closer first for better rendering)
                                        chunks_to_load.sort(key=lambda c: abs(c[0] - spawn_chunk_x) + abs(c[1] - spawn_chunk_z))
                                        
                                        chunks_sent = 0
                                        total_bytes = 0
                                        for chunk_x, chunk_z in chunks_to_load:
                                            try:
                                                chunk_data = PacketBuilder.build_chunk_data(
                                                    chunk_x=chunk_x,
                                                    chunk_z=chunk_z,
                                                    flat_world=True,
                                                    ground_y=64
                                                )
                                                client_socket.sendall(chunk_data)
                                                player_state.mark_chunk_loaded(chunk_x, chunk_z)
                                                chunks_sent += 1
                                                total_bytes += len(chunk_data)
                                                if chunks_sent <= 10 or chunks_sent % 10 == 0:  # Log first 10 and every 10th
                                                    print(f"  │  ✓ Chunk ({chunk_x}, {chunk_z}) loaded ({len(chunk_data)} bytes)")
                                            except Exception as e:
                                                print(f"  │  ✗ Error loading chunk ({chunk_x}, {chunk_z}): {e}")
                                        
                                        print(f"  │  ✓ All {chunks_sent} chunks loaded ({total_bytes} total bytes)")
                                    except Exception as chunk_error:
                                        print(f"  │  ✗ Error loading initial chunks: {chunk_error}")
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

def main():
    """Main server function."""
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

