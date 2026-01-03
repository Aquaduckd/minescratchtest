#!/usr/bin/env python3
"""
Minecraft Protocol Parser
Handles parsing and encoding of Minecraft Java Edition protocol packets.
"""

import struct
import uuid
import zlib
from enum import IntEnum
from typing import Optional, Tuple, List, Dict, Any
from dataclasses import dataclass


class ConnectionState(IntEnum):
    """Connection states in the Minecraft protocol."""
    HANDSHAKING = 0
    STATUS = 1
    LOGIN = 2
    CONFIGURATION = 3
    PLAY = 4


class PacketDirection(IntEnum):
    """Packet direction."""
    CLIENTBOUND = 0  # Server -> Client
    SERVERBOUND = 1  # Client -> Server


@dataclass
class HandshakePacket:
    """Handshake packet structure."""
    protocol_version: int
    server_address: str
    server_port: int
    intent: int  # 1=Status, 2=Login, 3=Transfer


@dataclass
class LoginStartPacket:
    """Login Start packet structure."""
    username: str
    player_uuid: uuid.UUID


@dataclass
class GameProfile:
    """Game Profile structure for Login Success."""
    uuid: uuid.UUID
    username: str
    properties: List[Dict[str, Any]]  # Can be empty for offline mode


@dataclass
class ClientInformationPacket:
    """Client Information packet structure (Configuration state)."""
    locale: str
    view_distance: int
    chat_mode: int
    chat_colors: bool
    displayed_skin_parts: int
    main_hand: int
    enable_text_filtering: bool
    allow_server_listings: bool


@dataclass
class SetPlayerPositionPacket:
    """Set Player Position packet structure (0x1D)."""
    x: float
    y: float
    z: float


@dataclass
class SetPlayerPositionAndRotationPacket:
    """Set Player Position and Rotation packet structure (0x1E)."""
    x: float
    y: float
    z: float
    yaw: float
    pitch: float


@dataclass
class KeepAlivePacket:
    """Keep Alive packet structure (0x1B serverbound, 0x2B clientbound)."""
    keep_alive_id: int


@dataclass
class PlayerActionPacket:
    """Player Action packet structure (0x28)."""
    status: int  # 0=Started digging, 1=Cancelled, 2=Finished digging
    location: Tuple[int, int, int]  # (x, y, z) block position
    face: int  # Face being hit (0-5)
    sequence: int  # Block change sequence number


class ProtocolReader:
    """Reads Minecraft protocol data types from bytes."""
    
    def __init__(self, data: bytes, offset: int = 0):
        self.data = data
        self.offset = offset
    
    def read_varint(self) -> int:
        """Read a VarInt from the current position."""
        result = 0
        shift = 0
        
        while True:
            if self.offset >= len(self.data):
                raise ValueError("Not enough data for VarInt")
            
            byte = self.data[self.offset]
            self.offset += 1
            
            result |= (byte & 0x7F) << shift
            
            if (byte & 0x80) == 0:
                break
            
            shift += 7
            if shift >= 32:
                raise ValueError("VarInt too long")
        
        # Convert to signed integer (two's complement)
        if result & 0x80000000:
            result -= 0x100000000
        
        return result
    
    def read_string(self, max_length: int = 32767) -> str:
        """Read a length-prefixed UTF-8 string."""
        length = self.read_varint()
        if length < 0 or length > max_length * 3:  # UTF-8 can be up to 3 bytes per char
            raise ValueError(f"Invalid string length: {length}")
        
        if self.offset + length > len(self.data):
            raise ValueError("Not enough data for string")
        
        string_bytes = self.data[self.offset:self.offset + length]
        self.offset += length
        
        return string_bytes.decode('utf-8')
    
    def read_uuid(self) -> uuid.UUID:
        """Read a UUID (16 bytes, big-endian)."""
        if self.offset + 16 > len(self.data):
            raise ValueError("Not enough data for UUID")
        
        uuid_bytes = self.data[self.offset:self.offset + 16]
        self.offset += 16
        
        # UUID is stored as big-endian
        return uuid.UUID(bytes=uuid_bytes)
    
    def read_unsigned_short(self) -> int:
        """Read an unsigned short (2 bytes, big-endian)."""
        if self.offset + 2 > len(self.data):
            raise ValueError("Not enough data for unsigned short")
        
        value = struct.unpack('>H', self.data[self.offset:self.offset + 2])[0]
        self.offset += 2
        return value
    
    def read_byte(self) -> int:
        """Read a single byte."""
        if self.offset >= len(self.data):
            raise ValueError("Not enough data for byte")
        
        value = self.data[self.offset]
        self.offset += 1
        return value
    
    def read_bool(self) -> bool:
        """Read a boolean (1 byte, 0x00 = false, 0x01 = true)."""
        return self.read_byte() != 0
    
    def read_bytes(self, length: int) -> bytes:
        """Read a fixed number of bytes."""
        if self.offset + length > len(self.data):
            raise ValueError(f"Not enough data: need {length} bytes")
        
        result = self.data[self.offset:self.offset + length]
        self.offset += length
        return result
    
    def read_int(self) -> int:
        """Read a signed 32-bit integer (4 bytes, big-endian)."""
        if self.offset + 4 > len(self.data):
            raise ValueError("Not enough data for int")
        
        value = struct.unpack('>i', self.data[self.offset:self.offset + 4])[0]
        self.offset += 4
        return value
    
    def read_long(self) -> int:
        """Read a signed 64-bit integer (8 bytes, big-endian)."""
        if self.offset + 8 > len(self.data):
            raise ValueError("Not enough data for long")
        
        value = struct.unpack('>q', self.data[self.offset:self.offset + 8])[0]
        self.offset += 8
        return value
    
    def read_float(self) -> float:
        """Read a 32-bit float (4 bytes, big-endian)."""
        if self.offset + 4 > len(self.data):
            raise ValueError("Not enough data for float")
        
        value = struct.unpack('>f', self.data[self.offset:self.offset + 4])[0]
        self.offset += 4
        return value
    
    def read_double(self) -> float:
        """Read a 64-bit double (8 bytes, big-endian)."""
        if self.offset + 8 > len(self.data):
            raise ValueError("Not enough data for double")
        
        value = struct.unpack('>d', self.data[self.offset:self.offset + 8])[0]
        self.offset += 8
        return value
    
    def read_position(self) -> Tuple[int, int, int]:
        """Read a Position (64-bit: x=26 bits, z=26 bits, y=12 bits)."""
        if self.offset + 8 > len(self.data):
            raise ValueError("Not enough data for position")
        
        value = struct.unpack('>Q', self.data[self.offset:self.offset + 8])[0]
        self.offset += 8
        
        # Extract x (26 MSBs), z (26 middle), y (12 LSBs)
        x = (value >> 38) & 0x3FFFFFF
        z = (value >> 12) & 0x3FFFFFF
        y = value & 0xFFF
        
        # Convert from unsigned to signed
        if x & 0x2000000:  # Check sign bit
            x -= 0x4000000
        if z & 0x2000000:
            z -= 0x4000000
        if y & 0x800:  # Check sign bit for 12-bit
            y -= 0x1000
        
        return (x, y, z)
    
    def read_identifier(self) -> str:
        """Read an Identifier (same as String)."""
        return self.read_string(32767)
    
    def remaining(self) -> int:
        """Get remaining bytes."""
        return len(self.data) - self.offset


class ProtocolWriter:
    """Writes Minecraft protocol data types to bytes."""
    
    def __init__(self):
        self.data = bytearray()
    
    def write_varlong(self, value: int) -> 'ProtocolWriter':
        """Write a VarLong (variable-length long, up to 10 bytes)."""
        # Similar to VarInt but for 64-bit values
        while True:
            byte = value & 0x7F
            value >>= 7
            if value == 0 and (byte & 0x80) == 0:
                self.data.append(byte)
                break
            self.data.append(byte | 0x80)
        return self
    
    def write_varint(self, value: int) -> 'ProtocolWriter':
        """Write a VarInt."""
        # Convert to unsigned for encoding
        if value < 0:
            value = value + 0x100000000
        
        while True:
            byte = value & 0x7F
            value >>= 7
            
            if value != 0:
                byte |= 0x80
            
            self.data.append(byte)
            
            if value == 0:
                break
        
        return self
    
    def write_string(self, value: str, max_length: int = 32767) -> 'ProtocolWriter':
        """Write a length-prefixed UTF-8 string."""
        encoded = value.encode('utf-8')
        length = len(encoded)
        
        if length > max_length * 3:
            raise ValueError(f"String too long: {length} bytes")
        
        self.write_varint(length)
        self.data.extend(encoded)
        return self
    
    def write_byte(self, value: int) -> 'ProtocolWriter':
        """Write a single byte (signed, -128 to 127)."""
        if value < -128 or value > 127:
            raise ValueError(f"Byte value out of range: {value}")
        # Convert signed to unsigned byte for network transmission
        # -1 becomes 0xFF, -128 becomes 0x80, etc.
        self.data.append(value & 0xFF)
        return self
    
    def write_bool(self, value: bool) -> 'ProtocolWriter':
        """Write a boolean (1 byte, 0x00 = false, 0x01 = true)."""
        self.data.append(0x01 if value else 0x00)
        return self
    
    def write_uuid(self, value: uuid.UUID) -> 'ProtocolWriter':
        """Write a UUID (16 bytes, big-endian)."""
        self.data.extend(value.bytes)
        return self
    
    def write_unsigned_short(self, value: int) -> 'ProtocolWriter':
        """Write an unsigned short (2 bytes, big-endian)."""
        self.data.extend(struct.pack('>H', value))
        return self
    
    def write_short(self, value: int) -> 'ProtocolWriter':
        """Write a signed short (2 bytes, big-endian)."""
        self.data.extend(struct.pack('>h', value))
        return self

    def write_bytes(self, data: bytes) -> 'ProtocolWriter':
        """Write raw bytes."""
        self.data.extend(data)
        return self
    
    def write_int(self, value: int) -> 'ProtocolWriter':
        """Write a signed 32-bit integer (4 bytes, big-endian)."""
        self.data.extend(struct.pack('>i', value))
        return self
    
    def write_long(self, value: int) -> 'ProtocolWriter':
        """Write a signed 64-bit integer (8 bytes, big-endian)."""
        self.data.extend(struct.pack('>q', value))
        return self
    
    def write_bitset(self, bits: List[bool]) -> 'ProtocolWriter':
        """
        Write a BitSet (prefixed by length in longs, then array of longs).
        
        Args:
            bits: List of boolean values representing bits (True = set, False = unset)
        """
        # Calculate number of longs needed
        num_bits = len(bits)
        num_longs = (num_bits + 63) // 64  # Ceiling division
        
        # Write length (number of longs)
        self.write_varint(num_longs)
        
        # Pack bits into longs
        for long_idx in range(num_longs):
            long_value = 0
            for bit_idx in range(64):
                bit_pos = long_idx * 64 + bit_idx
                if bit_pos < num_bits and bits[bit_pos]:
                    # Set bit at position (bit_idx) in the long
                    long_value |= (1 << bit_idx)
            self.write_long(long_value)
        
        return self
    
    def write_float(self, value: float) -> 'ProtocolWriter':
        """Write a 32-bit float (4 bytes, big-endian)."""
        self.data.extend(struct.pack('>f', value))
        return self
    
    def write_double(self, value: float) -> 'ProtocolWriter':
        """Write a 64-bit double (8 bytes, big-endian)."""
        self.data.extend(struct.pack('>d', value))
        return self
    
    def write_position(self, x: int, y: int, z: int) -> 'ProtocolWriter':
        """Write a Position (64-bit: x=26 bits, z=26 bits, y=12 bits)."""
        # Convert signed to unsigned
        x_unsigned = x & 0x3FFFFFF
        z_unsigned = z & 0x3FFFFFF
        y_unsigned = y & 0xFFF
        
        # Pack into 64-bit value
        value = (x_unsigned << 38) | (z_unsigned << 12) | y_unsigned
        self.data.extend(struct.pack('>Q', value))
        return self
    
    def write_identifier(self, value: str) -> 'ProtocolWriter':
        """Write an Identifier (same as String)."""
        return self.write_string(value, 32767)
    
    def write_lpvec3(self, x: float, y: float, z: float) -> 'ProtocolWriter':
        """
        Write an LpVec3 (low-precision velocity vector).
        Each component is a VarInt representing velocity in units of 1/8000 blocks per tick.
        """
        # Convert float velocity to fixed-point integer (1/8000 precision)
        x_int = int(x * 8000)
        y_int = int(y * 8000)
        z_int = int(z * 8000)
        
        self.write_varint(x_int)
        self.write_varint(y_int)
        self.write_varint(z_int)
        return self
    
    def write_angle(self, angle: float) -> 'ProtocolWriter':
        """
        Write an Angle (1 byte, representing 1/256 of a full turn).
        Angle in degrees is converted to byte: (angle / 360.0) * 256
        """
        angle_byte = int((angle % 360.0) / 360.0 * 256) & 0xFF
        self.data.append(angle_byte)
        return self
    
    def to_bytes(self) -> bytes:
        """Get the written data as bytes."""
        return bytes(self.data)
    
    def length(self) -> int:
        """Get the current length."""
        return len(self.data)


class PacketParser:
    """Parses Minecraft protocol packets."""
    
    @staticmethod
    def parse_packet(data: bytes, state: ConnectionState) -> Tuple[int, Any]:
        """
        Parse a packet from raw bytes.
        Returns: (packet_id, parsed_packet_object)
        """
        reader = ProtocolReader(data)
        
        # Read packet length and ID
        packet_length = reader.read_varint()
        packet_id = reader.read_varint()
        
        # Parse based on state and packet ID
        if state == ConnectionState.HANDSHAKING:
            if packet_id == 0:  # Handshake
                return packet_id, PacketParser._parse_handshake(reader)
        
        elif state == ConnectionState.LOGIN:
            if packet_id == 0:  # Login Start
                return packet_id, PacketParser._parse_login_start(reader)
            elif packet_id == 3:  # Login Acknowledged
                return packet_id, None  # Empty packet
        
        elif state == ConnectionState.CONFIGURATION:
            if packet_id == 0:  # Client Information
                return packet_id, PacketParser._parse_client_information(reader)
            elif packet_id == 3:  # Acknowledge Finish Configuration
                return packet_id, None  # Empty packet
            elif packet_id == 0x07:  # Serverbound Known Packs
                return packet_id, PacketParser._parse_known_packs(reader)
        
        elif state == ConnectionState.PLAY:
            if packet_id == 0x1D:  # Set Player Position
                return packet_id, PacketParser._parse_set_player_position(reader)
            elif packet_id == 0x1E:  # Set Player Position and Rotation
                return packet_id, PacketParser._parse_set_player_position_and_rotation(reader)
            elif packet_id == 0x1B:  # Serverbound Keep Alive
                return packet_id, PacketParser._parse_keep_alive(reader)
            elif packet_id == 0x28:  # Player Action
                return packet_id, PacketParser._parse_player_action(reader)
        
        # Unknown packet
        return packet_id, None
    
    @staticmethod
    def _parse_handshake(reader: ProtocolReader) -> HandshakePacket:
        """Parse a Handshake packet."""
        protocol_version = reader.read_varint()
        server_address = reader.read_string(255)
        server_port = reader.read_unsigned_short()
        intent = reader.read_varint()
        
        return HandshakePacket(
            protocol_version=protocol_version,
            server_address=server_address,
            server_port=server_port,
            intent=intent
        )
    
    @staticmethod
    def _parse_login_start(reader: ProtocolReader) -> LoginStartPacket:
        """Parse a Login Start packet."""
        username = reader.read_string(16)
        player_uuid = reader.read_uuid()
        
        return LoginStartPacket(
            username=username,
            player_uuid=player_uuid
        )
    
    @staticmethod
    def _parse_client_information(reader: ProtocolReader) -> ClientInformationPacket:
        """Parse a Client Information packet (Configuration state)."""
        locale = reader.read_string(16)
        view_distance = reader.read_byte()  # Byte (signed)
        chat_mode = reader.read_varint()
        chat_colors = reader.read_bool()  # Boolean
        displayed_skin_parts = reader.read_byte()  # Unsigned Byte (but we read as byte)
        main_hand = reader.read_varint()
        enable_text_filtering = reader.read_bool()  # Boolean
        allow_server_listings = reader.read_bool()  # Boolean
        
        return ClientInformationPacket(
            locale=locale,
            view_distance=view_distance,
            chat_mode=chat_mode,
            chat_colors=chat_colors,
            displayed_skin_parts=displayed_skin_parts,
            main_hand=main_hand,
            enable_text_filtering=enable_text_filtering,
            allow_server_listings=allow_server_listings
        )
    
    @staticmethod
    def _parse_known_packs(reader: ProtocolReader) -> List[Tuple[str, str, str]]:
        """Parse a Serverbound Known Packs packet."""
        packs = []
        num_packs = reader.read_varint()
        for _ in range(num_packs):
            namespace = reader.read_string(32767)
            pack_id = reader.read_string(32767)
            version = reader.read_string(32767)
            packs.append((namespace, pack_id, version))
        return packs
    
    @staticmethod
    def _parse_set_player_position(reader: ProtocolReader) -> SetPlayerPositionPacket:
        """Parse a Set Player Position packet (0x1D)."""
        x = reader.read_double()
        y = reader.read_double()
        z = reader.read_double()
        
        return SetPlayerPositionPacket(x=x, y=y, z=z)
    
    @staticmethod
    def _parse_set_player_position_and_rotation(reader: ProtocolReader) -> SetPlayerPositionAndRotationPacket:
        """Parse a Set Player Position and Rotation packet (0x1E)."""
        x = reader.read_double()
        y = reader.read_double()
        z = reader.read_double()
        yaw = reader.read_float()
        pitch = reader.read_float()
        
        return SetPlayerPositionAndRotationPacket(x=x, y=y, z=z, yaw=yaw, pitch=pitch)
    
    @staticmethod
    def _parse_keep_alive(reader: ProtocolReader) -> KeepAlivePacket:
        """Parse a Serverbound Keep Alive packet (0x1B)."""
        keep_alive_id = reader.read_long()
        return KeepAlivePacket(keep_alive_id=keep_alive_id)
    
    @staticmethod
    def _parse_player_action(reader: ProtocolReader) -> PlayerActionPacket:
        """Parse a Player Action packet (0x28)."""
        status = reader.read_varint()
        location = reader.read_position()
        face = reader.read_byte()
        sequence = reader.read_varint()
        return PlayerActionPacket(
            status=status,
            location=location,
            face=face,
            sequence=sequence
        )


class PacketBuilder:
    """Builds Minecraft protocol packets."""
    
    @staticmethod
    def build_login_success(profile: GameProfile) -> bytes:
        """Build a Login Success packet."""
        writer = ProtocolWriter()
        
        # Write packet ID first (we'll prepend length later)
        packet_writer = ProtocolWriter()
        packet_writer.write_varint(0x02)  # Login Success packet ID
        
        # Write Game Profile
        packet_writer.write_uuid(profile.uuid)
        packet_writer.write_string(profile.username, 16)
        
        # Write properties array (empty for offline mode)
        packet_writer.write_varint(0)  # Empty array
        
        # Build final packet with length prefix
        packet_data = packet_writer.to_bytes()
        final_writer = ProtocolWriter()
        final_writer.write_varint(len(packet_data))
        final_writer.write_bytes(packet_data)
        
        return final_writer.to_bytes()
    
    @staticmethod
    def build_disconnect(reason: str) -> bytes:
        """Build a Disconnect packet (login state)."""
        # For now, just a simple JSON string
        # In full implementation, this should be a proper JSON Text Component
        writer = ProtocolWriter()
        
        packet_writer = ProtocolWriter()
        packet_writer.write_varint(0x00)  # Disconnect packet ID
        packet_writer.write_string(f'{{"text":"{reason}"}}', 32767)
        
        packet_data = packet_writer.to_bytes()
        final_writer = ProtocolWriter()
        final_writer.write_varint(len(packet_data))
        final_writer.write_bytes(packet_data)
        
        return final_writer.to_bytes()
    
    @staticmethod
    def build_finish_configuration() -> bytes:
        """Build a Finish Configuration packet (Configuration state)."""
        # Finish Configuration is packet ID 0x03 with no fields
        packet_writer = ProtocolWriter()
        packet_writer.write_varint(0x03)  # Finish Configuration packet ID
        
        packet_data = packet_writer.to_bytes()
        final_writer = ProtocolWriter()
        final_writer.write_varint(len(packet_data))
        final_writer.write_bytes(packet_data)
        
        return final_writer.to_bytes()
    
    @staticmethod
    def build_login_play(
        entity_id: int = 1,
        is_hardcore: bool = False,
        dimension_names: List[str] = None,
        max_players: int = 20,
        view_distance: int = 10,
        simulation_distance: int = 10,
        reduced_debug_info: bool = False,
        enable_respawn_screen: bool = True,
        do_limited_crafting: bool = False,
        dimension_type: int = 0,  # 0 = overworld in dimension_type registry
        dimension_name: str = "minecraft:overworld",
        hashed_seed: int = 0,
        game_mode: int = 0,  # 0=Survival
        previous_game_mode: int = -1,  # -1=Undefined
        is_debug: bool = False,
        is_flat: bool = False,
        has_death_location: bool = False,
        portal_cooldown: int = 0,
        sea_level: int = 63,
        enforces_secure_chat: bool = False
    ) -> bytes:
        """
        Build a Login (play) packet (PLAY state).
        This is the first packet sent after entering PLAY state.
        """
        packet_writer = ProtocolWriter()
        packet_writer.write_varint(0x30)  # Login (play) packet ID
        
        # Entity ID
        packet_writer.write_int(entity_id)
        
        # Is hardcore
        packet_writer.write_bool(is_hardcore)
        
        # Dimension Names (array of identifiers)
        if dimension_names is None:
            dimension_names = ["minecraft:overworld"]
        packet_writer.write_varint(len(dimension_names))
        for dim_name in dimension_names:
            packet_writer.write_identifier(dim_name)
        
        # Max Players
        packet_writer.write_varint(max_players)
        
        # View Distance
        packet_writer.write_varint(view_distance)
        
        # Simulation Distance
        packet_writer.write_varint(simulation_distance)
        
        # Reduced Debug Info
        packet_writer.write_bool(reduced_debug_info)
        
        # Enable respawn screen
        packet_writer.write_bool(enable_respawn_screen)
        
        # Do limited crafting
        packet_writer.write_bool(do_limited_crafting)
        
        # Dimension Type
        packet_writer.write_varint(dimension_type)
        
        # Dimension Name
        packet_writer.write_identifier(dimension_name)
        
        # Hashed seed
        packet_writer.write_long(hashed_seed)
        
        # Game mode
        packet_writer.write_byte(game_mode)
        
        # Previous Game mode
        packet_writer.write_byte(previous_game_mode)
        
        # Is Debug
        packet_writer.write_bool(is_debug)
        
        # Is Flat
        packet_writer.write_bool(is_flat)
        
        # Has death location
        packet_writer.write_bool(has_death_location)
        
        # Death dimension name and location (only if has_death_location is true)
        if has_death_location:
            # For minimal implementation, we'll skip this
            # Would need: packet_writer.write_identifier(death_dimension_name)
            # Would need: packet_writer.write_position(death_x, death_y, death_z)
            pass
        
        # Portal cooldown
        packet_writer.write_varint(portal_cooldown)
        
        # Sea level
        packet_writer.write_varint(sea_level)
        
        # Enforces Secure Chat
        packet_writer.write_bool(enforces_secure_chat)
        
        # Build final packet with length prefix
        packet_data = packet_writer.to_bytes()
        final_writer = ProtocolWriter()
        final_writer.write_varint(len(packet_data))
        final_writer.write_bytes(packet_data)
        
        return final_writer.to_bytes()
    
    @staticmethod
    def build_registry_data(
        registry_id: str,
        entries: List[Tuple[str, Optional[bytes]]] = None
    ) -> bytes:
        """
        Build a Registry Data packet (Configuration state).
        
        Args:
            registry_id: Registry identifier (e.g., "minecraft:dimension_type")
            entries: List of (entry_id, nbt_data) tuples. nbt_data can be None to omit NBT.
                    Entry order determines numeric IDs (first entry = ID 0, second = ID 1, etc.)
        """
        if entries is None:
            entries = []
        
        packet_writer = ProtocolWriter()
        packet_writer.write_varint(0x07)  # Registry Data packet ID
        
        # Registry ID
        packet_writer.write_identifier(registry_id)
        
        # Entries array
        packet_writer.write_varint(len(entries))
        for entry_id, nbt_data in entries:
            # Entry ID (Identifier)
            packet_writer.write_identifier(entry_id)
            
            # Data (Prefixed Optional NBT)
            # Prefixed Optional: boolean (present?) + data if present
            if nbt_data is not None:
                packet_writer.write_bool(True)
                # For now, we'll just write the raw bytes
                # In full implementation, this would be proper NBT encoding
                packet_writer.write_bytes(nbt_data)
            else:
                packet_writer.write_bool(False)  # Omit NBT data
        
        # Build final packet with length prefix
        packet_data = packet_writer.to_bytes()
        final_writer = ProtocolWriter()
        final_writer.write_varint(len(packet_data))
        final_writer.write_bytes(packet_data)
        
        return final_writer.to_bytes()
    
    @staticmethod
    def build_known_packs(packs: List[Tuple[str, str, str]] = None) -> bytes:
        """
        Build a Clientbound Known Packs packet (Configuration state).
        
        Args:
            packs: List of (namespace, id, version) tuples. 
                  Default: [("minecraft", "core", "1.21.10")]
        """
        if packs is None:
            packs = [("minecraft", "core", "1.21.10")]
        
        packet_writer = ProtocolWriter()
        packet_writer.write_varint(0x0E)  # Clientbound Known Packs packet ID
        
        # Known Packs array
        packet_writer.write_varint(len(packs))
        for namespace, pack_id, version in packs:
            packet_writer.write_string(namespace, 32767)
            packet_writer.write_string(pack_id, 32767)
            packet_writer.write_string(version, 32767)
        
        # Build final packet with length prefix
        packet_data = packet_writer.to_bytes()
        final_writer = ProtocolWriter()
        final_writer.write_varint(len(packet_data))
        final_writer.write_bytes(packet_data)
        
        return final_writer.to_bytes()
    
    @staticmethod
    def build_synchronize_player_position(
        x: float = 0.0,
        y: float = 64.0,
        z: float = 0.0,
        yaw: float = 0.0,
        pitch: float = 0.0,
        flags: int = 0,  # 0 = all absolute
        teleport_id: int = 0
    ) -> bytes:
        """
        Build a Synchronize Player Position packet (PLAY state).
        Sets the player's spawn position.
        """
        packet_writer = ProtocolWriter()
        packet_writer.write_varint(0x46)  # Synchronize Player Position packet ID
        
        # Teleport ID
        packet_writer.write_varint(teleport_id)
        
        # Position (Double)
        packet_writer.write_double(float(x))
        packet_writer.write_double(float(y))
        packet_writer.write_double(float(z))
        
        # Velocity (Double) - usually 0 for spawn
        packet_writer.write_double(0.0)
        packet_writer.write_double(0.0)
        packet_writer.write_double(0.0)
        
        # Yaw and Pitch (Float)
        packet_writer.write_float(float(yaw))
        packet_writer.write_float(float(pitch))
        
        # Flags (Int) - 0 = all absolute
        packet_writer.write_int(flags)
        
        # Build final packet with length prefix
        packet_data = packet_writer.to_bytes()
        final_writer = ProtocolWriter()
        final_writer.write_varint(len(packet_data))
        final_writer.write_bytes(packet_data)
        
        return final_writer.to_bytes()
    
    @staticmethod
    def build_update_time(
        world_age: int = 0,
        time_of_day: int = 6000,  # Noon
        time_increasing: bool = True
    ) -> bytes:
        """
        Build an Update Time packet (PLAY state).
        Sets the world time.
        """
        packet_writer = ProtocolWriter()
        packet_writer.write_varint(0x6F)  # Update Time packet ID
        
        # World Age (Long)
        packet_writer.write_long(world_age)
        
        # Time of Day (Long)
        packet_writer.write_long(time_of_day)
        
        # Time of Day Increasing (Boolean)
        packet_writer.write_bool(time_increasing)
        
        # Build final packet with length prefix
        packet_data = packet_writer.to_bytes()
        final_writer = ProtocolWriter()
        final_writer.write_varint(len(packet_data))
        final_writer.write_bytes(packet_data)
        
        return final_writer.to_bytes()
    
    @staticmethod
    def build_game_event(
        event: int = 13,  # 13 = "Start waiting for level chunks"
        value: float = 0.0
    ) -> bytes:
        """
        Build a Game Event packet (PLAY state).
        Event 13 is required for the client to spawn after receiving chunks.
        """
        packet_writer = ProtocolWriter()
        packet_writer.write_varint(0x26)  # Game Event packet ID
        
        # Event (Unsigned Byte)
        packet_writer.write_byte(event)
        
        # Value (Float)
        packet_writer.write_float(value)
        
        # Build final packet with length prefix
        packet_data = packet_writer.to_bytes()
        final_writer = ProtocolWriter()
        final_writer.write_varint(len(packet_data))
        final_writer.write_bytes(packet_data)
        
        return final_writer.to_bytes()
    
    @staticmethod
    def build_set_center_chunk(chunk_x: int, chunk_z: int) -> bytes:
        """
        Build a Set Center Chunk packet (PLAY state, packet ID 0x5C).
        Sets the center position of the client's chunk loading area.
        
        Args:
            chunk_x: Chunk X coordinate (VarInt)
            chunk_z: Chunk Z coordinate (VarInt)
        """
        packet_writer = ProtocolWriter()
        packet_writer.write_varint(0x5C)  # Set Center Chunk packet ID
        
        # Chunk X (VarInt)
        packet_writer.write_varint(chunk_x)
        
        # Chunk Z (VarInt)
        packet_writer.write_varint(chunk_z)
        
        # Build final packet with length prefix
        packet_data = packet_writer.to_bytes()
        final_writer = ProtocolWriter()
        final_writer.write_varint(len(packet_data))
        final_writer.write_bytes(packet_data)
        
        return final_writer.to_bytes()
    
    @staticmethod
    def build_keep_alive(keep_alive_id: int) -> bytes:
        """
        Build a Keep Alive packet (PLAY state, packet ID 0x2B).
        Server sends this to client, client responds with same ID.
        
        Args:
            keep_alive_id: Unique ID for this keep alive (typically timestamp in milliseconds)
        """
        packet_writer = ProtocolWriter()
        packet_writer.write_varint(0x2B)  # Keep Alive packet ID (clientbound)
        
        # Keep Alive ID (Long)
        packet_writer.write_long(keep_alive_id)
        
        # Build final packet with length prefix
        packet_data = packet_writer.to_bytes()
        final_writer = ProtocolWriter()
        final_writer.write_varint(len(packet_data))
        final_writer.write_bytes(packet_data)
        
        return final_writer.to_bytes()
    
    @staticmethod
    def build_block_update(x: int, y: int, z: int, block_state_id: int) -> bytes:
        """
        Build a Block Update packet (PLAY state, packet ID 0x08).
        Notifies the client that a block has changed.
        
        Args:
            x: Block X coordinate
            y: Block Y coordinate
            z: Block Z coordinate
            block_state_id: New block state ID (0 for air)
        """
        packet_writer = ProtocolWriter()
        packet_writer.write_varint(0x08)  # Block Update packet ID
        
        # Location (Position)
        packet_writer.write_position(x, y, z)
        
        # Block ID (VarInt)
        packet_writer.write_varint(block_state_id)
        
        # Build final packet with length prefix
        packet_data = packet_writer.to_bytes()
        final_writer = ProtocolWriter()
        final_writer.write_varint(len(packet_data))
        final_writer.write_bytes(packet_data)
        
        return final_writer.to_bytes()
    
    @staticmethod
    def _write_slot(writer: ProtocolWriter, item_id: int, count: int = 1):
        """
        Write a Slot (item stack) format using the new data component format (1.21+).
        Format: Item Count (VarInt) + Item ID (Optional VarInt) + Components (Optional)
        For simple items with no components:
        - Item Count (VarInt) = count
        - Item ID (VarInt) = item_id
        - Components are omitted (Optional fields are not written when 0/empty)
        """
        writer.write_varint(count)  # Item Count
        if count > 0:
            writer.write_varint(item_id)  # Item ID
            # Number of components to add (Optional VarInt) - omitted when 0
            # Number of components to remove (Optional VarInt) - omitted when 0
            # For simple items, we don't write any components
    
    @staticmethod
    def build_spawn_entity(
        entity_id: int,
        entity_uuid: uuid.UUID,
        entity_type: int,  # ID in minecraft:entity_type registry
        x: float,
        y: float,
        z: float,
        velocity_x: float = 0.0,
        velocity_y: float = 0.0,
        velocity_z: float = 0.0,
        pitch: float = 0.0,
        yaw: float = 0.0,
        head_yaw: float = 0.0
    ) -> bytes:
        """
        Build a Spawn Entity packet (PLAY state, packet ID 0x01).
        
        Args:
            entity_id: Unique entity ID
            entity_uuid: Entity UUID
            entity_type: ID in minecraft:entity_type registry (e.g., 2 for item)
            x, y, z: Position (Double)
            velocity_x, velocity_y, velocity_z: Velocity (LpVec3)
            pitch, yaw, head_yaw: Rotation angles in degrees (Angle)
            
        Note:
            For item entities, the Data field is 0. The item stack should be set
            via Entity Metadata (index 8, Slot type) after spawning.
        """
        packet_writer = ProtocolWriter()
        packet_writer.write_varint(0x01)  # Spawn Entity packet ID
        
        # Entity ID
        packet_writer.write_varint(entity_id)
        
        # Entity UUID
        packet_writer.write_uuid(entity_uuid)
        
        # Type
        packet_writer.write_varint(entity_type)
        
        # Position
        packet_writer.write_double(x)
        packet_writer.write_double(y)
        packet_writer.write_double(z)
        
        # Velocity (LpVec3)
        packet_writer.write_lpvec3(velocity_x, velocity_y, velocity_z)
        
        # Pitch, Yaw, Head Yaw (Angle)
        packet_writer.write_angle(pitch)
        packet_writer.write_angle(yaw)
        packet_writer.write_angle(head_yaw)
        
        # Data - meaning depends on entity type
        # For item entities (type 2), Data is 0 (item stack is set via Entity Metadata)
        # According to Object data documentation, item entities are not listed,
        # meaning they don't use the Data field - it should be 0
        packet_writer.write_varint(0)
        
        # Build final packet with length prefix
        packet_data = packet_writer.to_bytes()
        final_writer = ProtocolWriter()
        final_writer.write_varint(len(packet_data))
        final_writer.write_bytes(packet_data)
        
        return final_writer.to_bytes()
    
    @staticmethod
    def build_set_entity_metadata(
        entity_id: int,
        metadata: List[Tuple[int, int, any]]  # List of (index, type, value) tuples
    ) -> bytes:
        """
        Build a Set Entity Metadata packet (PLAY state, packet ID 0x61).
        
        Args:
            entity_id: Entity ID
            metadata: List of (index, type, value) tuples
                     Type 0: Byte, 1: VarInt, 2: VarLong, 3: Float, 4: String, 5: Chat, 6: Optional Chat,
                     7: Slot, 8: Boolean, 9: Rotation, 10: Position, 11: Optional Position,
                     12: Direction, 13: Optional UUID, 14: Block State, 15: Optional Block State,
                     16: NBT, 17: Particle, 18: Villager Data, 19: Optional VarInt, 20: Pose,
                     21: Cat Variant, 22: Wolf Variant, 23: Frog Variant, 24: Optional Global Position,
                     25: Painting Variant, 26: Sniffer State, 27: Vector3, 28: Quaternion
        """
        packet_writer = ProtocolWriter()
        packet_writer.write_varint(0x61)  # Set Entity Metadata packet ID
        
        # Entity ID
        packet_writer.write_varint(entity_id)
        
        # Metadata entries
        for index, meta_type, value in metadata:
            # Write index (VarInt)
            packet_writer.write_varint(index)
            # Write type (VarInt)
            packet_writer.write_varint(meta_type)
            # Write value based on type
            if meta_type == 0:  # Byte
                packet_writer.write_byte(value)
            elif meta_type == 1:  # VarInt
                packet_writer.write_varint(value)
            elif meta_type == 2:  # VarLong
                packet_writer.write_varlong(value)
            elif meta_type == 3:  # Float
                packet_writer.write_float(value)
            elif meta_type == 7:  # Slot
                PacketBuilder._write_slot(packet_writer, value[0], value[1])  # (item_id, count)
            elif meta_type == 8:  # Boolean
                packet_writer.write_bool(value)
            else:
                # For other types, we'll need to implement them as needed
                raise ValueError(f"Unsupported metadata type: {meta_type}")
        
        # End marker (0xFF)
        packet_writer.write_byte(0xFF)
        
        # Build final packet with length prefix
        packet_data = packet_writer.to_bytes()
        final_writer = ProtocolWriter()
        final_writer.write_varint(len(packet_data))
        final_writer.write_bytes(packet_data)
        
        return final_writer.to_bytes()
    
    @staticmethod
    def _write_paletted_container_indirect(
        writer: 'ProtocolWriter',
        bits_per_entry: int,
        palette: List[int],
        data_array: List[int]
    ):
        """
        Write a PalettedContainer using Indirect palette format.
        
        Args:
            writer: ProtocolWriter to write to
            bits_per_entry: Bits per entry (4-8 for blocks, 1-3 for biomes)
            palette: List of global palette IDs
            data_array: List of palette indices (4096 for blocks, 64 for biomes)
        """
        # Bits per entry
        writer.write_byte(bits_per_entry)
        
        # Palette length
        writer.write_varint(len(palette))
        
        # Palette array (VarInt IDs)
        for palette_id in palette:
            writer.write_varint(palette_id)
        
        # Data array: Pack entries into longs
        # Number of entries per long
        entries_per_long = 64 // bits_per_entry
        
        # Number of longs needed
        num_entries = len(data_array)
        num_longs = (num_entries + entries_per_long - 1) // entries_per_long
        
        # Pack data into longs
        # Entries are packed with first entry at least significant bits
        # Long is written in big-endian, so LSB of first entry ends up in last byte
        for long_idx in range(num_longs):
            long_value = 0
            for entry_idx in range(entries_per_long):
                data_idx = long_idx * entries_per_long + entry_idx
                if data_idx < num_entries:
                    # Pack entry into long
                    # First entry goes at bit 0 (LSB), second at bit bits_per_entry, etc.
                    entry_value = data_array[data_idx]
                    # Mask to ensure we only use the bits we need
                    entry_value &= (1 << bits_per_entry) - 1
                    # Shift to correct bit position (first entry at bit 0)
                    bit_offset = entry_idx * bits_per_entry
                    long_value |= (entry_value << bit_offset)
            
            # Write long (big-endian) - struct.pack('>Q') handles byte order
            writer.write_long(long_value)
    
    @staticmethod
    def build_chunk_data(
        chunk_x: int = 0,
        chunk_z: int = 0,
        flat_world: bool = True,
        ground_y: int = 64
    ) -> bytes:
        """
        Build a Chunk Data and Update Light packet (PLAY state).
        
        Args:
            chunk_x: Chunk X coordinate
            chunk_z: Chunk Z coordinate
            flat_world: If True, generate flat world (stone at y=63, grass at y=64)
            ground_y: Y coordinate of ground level (default 64)
        """
        # Block state IDs (may need adjustment based on version)
        # These are approximate values for 1.21.10
        BLOCK_AIR = 0
        BLOCK_STONE = 1
        BLOCK_GRASS_BLOCK = 9  # May need adjustment
        
        packet_writer = ProtocolWriter()
        packet_writer.write_varint(0x2C)  # Chunk Data and Update Light packet ID
        
        # Chunk coordinates
        packet_writer.write_int(chunk_x)
        packet_writer.write_int(chunk_z)
        
        # Heightmaps: Generate MOTION_BLOCKING heightmap
        # For flat world at y=64, all columns have height 64
        # Heightmap format: Prefixed Array of Heightmap
        # Each heightmap: Type (VarInt) + Data (Prefixed Array of Long)
        # Bits per entry = ceil(log2(world_height + 1))
        # Overworld: -64 to 320 = 384 blocks, so bits_per_entry = ceil(log2(385)) = 9
        if flat_world:
            # Send MOTION_BLOCKING heightmap (Type = 4)
            packet_writer.write_varint(1)  # Number of heightmaps
            
            # Heightmap Type: MOTION_BLOCKING = 4
            packet_writer.write_varint(4)
            
            # Heightmap Data: 256 entries (16x16 columns), all set to ground_y
            # Packed into longs using 9 bits per entry
            # Ordering: x increases fastest, then z (same as block data ordering)
            heightmap_bits_per_entry = 9
            heightmap_entries = []
            for z in range(16):
                for x in range(16):
                    heightmap_entries.append(ground_y)  # All columns at ground level
            
            # Pack into longs (same format as Data Array)
            entries_per_long = 64 // heightmap_bits_per_entry  # 7 entries per long
            num_longs = (256 + entries_per_long - 1) // entries_per_long
            
            # Write length (number of longs)
            packet_writer.write_varint(num_longs)
            
            # Pack heightmap data
            for long_idx in range(num_longs):
                long_value = 0
                for entry_idx in range(entries_per_long):
                    data_idx = long_idx * entries_per_long + entry_idx
                    if data_idx < 256:
                        entry_value = heightmap_entries[data_idx]
                        bit_offset = entry_idx * heightmap_bits_per_entry
                        entry_value &= (1 << heightmap_bits_per_entry) - 1
                        long_value |= (entry_value << bit_offset)
                packet_writer.write_long(long_value)
        else:
            # No heightmaps
            packet_writer.write_varint(0)
        
        # Generate chunk sections
        chunk_data_writer = ProtocolWriter()
        
        # Overworld has 24 sections (y=-64 to 320)
        # Each section is 16 blocks tall
        # Sections are indexed 0-23, covering:
        # Section 0: y=-64 to -49
        # Section 1: y=-48 to -33
        # ...
        # Section 7: y=48 to 63
        # Section 8: y=64 to 79
        # ...
        # Section 23: y=304 to 319
        
        for section_idx in range(24):
            section_y_min = -64 + (section_idx * 16)
            section_y_max = section_y_min + 15
            
            # Generate block data for this section
            if flat_world and section_y_min <= ground_y <= section_y_max:
                # This section contains ground
                # Generate blocks: stone at y=63, grass at y=64
                block_data = [BLOCK_AIR] * 4096  # 16x16x16 = 4096 blocks
                block_count = 0
                
                # Fill section with blocks
                for y in range(16):
                    world_y = section_y_min + y
                    if world_y == ground_y - 1:  # Stone layer (y=63)
                        # Fill entire layer with stone
                        for z in range(16):
                            for x in range(16):
                                idx = y * 256 + z * 16 + x
                                block_data[idx] = BLOCK_STONE
                                block_count += 1
                    elif world_y == ground_y:  # Grass layer (y=64)
                        # Fill entire layer with grass
                        for z in range(16):
                            for x in range(16):
                                idx = y * 256 + z * 16 + x
                                block_data[idx] = BLOCK_GRASS_BLOCK
                                block_count += 1
                
                # Create palette (air, stone, grass)
                palette = [BLOCK_AIR, BLOCK_STONE, BLOCK_GRASS_BLOCK]
                
                # Map block_data to palette indices
                palette_indices = []
                for block_id in block_data:
                    palette_indices.append(palette.index(block_id))
                
                # Write block count
                chunk_data_writer.write_short(block_count)
                
                # Write block states PalettedContainer (Indirect, 4 bits per entry)
                PacketBuilder._write_paletted_container_indirect(
                    chunk_data_writer,
                    bits_per_entry=4,  # 4 bits = up to 16 palette entries
                    palette=palette,
                    data_array=palette_indices
                )
            elif flat_world and section_y_min < ground_y - 1:
                # Section below ground - fill with stone
                block_data = [BLOCK_STONE] * 4096
                block_count = 4096
                
                # Single-value palette (all stone)
                chunk_data_writer.write_short(block_count)
                chunk_data_writer.write_byte(0)  # 0 bits per entry
                chunk_data_writer.write_varint(BLOCK_STONE)
            else:
                # Section is all air
                chunk_data_writer.write_short(0)  # Block count = 0
                chunk_data_writer.write_byte(0)  # 0 bits per entry (single value)
                chunk_data_writer.write_varint(BLOCK_AIR)
            
            # Biomes: Single-value palette (plains = 0)
            chunk_data_writer.write_byte(0)  # 0 bits per entry
            chunk_data_writer.write_varint(0)  # Biome ID 0 = plains
        
        # Get the chunk data (uncompressed)
        chunk_data_raw = chunk_data_writer.to_bytes()
        
        # Write Data as Prefixed Array of Byte (VarInt length + bytes)
        packet_writer.write_varint(len(chunk_data_raw))
        packet_writer.write_bytes(chunk_data_raw)
        
        # Block Entities: Empty
        packet_writer.write_varint(0)
        
        # Light Data
        # BitSet format: VarInt (number of longs) + Array of Long
        # For overworld: 24 sections + 2 extra = 26 bits total
        num_sections = 24
        num_light_bits = num_sections + 2  # 26 bits
        
        # Calculate which sections need sky light (sections at/above ground)
        sky_light_mask_bits = [False] * num_light_bits
        sky_light_sections = []  # Track which sections we're sending data for
        
        if flat_world:
            # Sections that need sky light: from ground section upward
            ground_section = (ground_y + 64) // 16  # Section containing ground_y
            for section_idx in range(ground_section, num_sections):
                # Bit index: section_idx + 1 (because bit 0 is for section below world)
                bit_idx = section_idx + 1
                if bit_idx < num_light_bits:
                    sky_light_mask_bits[bit_idx] = True
                    sky_light_sections.append(section_idx)
        
        # Write Sky Light Mask (BitSet)
        packet_writer.write_bitset(sky_light_mask_bits)
        
        # Block Light Mask: Empty (no block light sources)
        packet_writer.write_bitset([False] * num_light_bits)
        
        # Empty Sky Light Mask: Sections that have all-zero sky light
        # For our flat world, sections below ground have no sky light
        empty_sky_light_mask_bits = [False] * num_light_bits
        if flat_world:
            ground_section = (ground_y + 64) // 16
            for section_idx in range(ground_section):
                bit_idx = section_idx + 1
                if bit_idx < num_light_bits:
                    empty_sky_light_mask_bits[bit_idx] = True
        packet_writer.write_bitset(empty_sky_light_mask_bits)
        
        # Empty Block Light Mask: All sections (no block light)
        packet_writer.write_bitset([True] * num_light_bits)
        
        # Sky Light Arrays: One array per bit set in sky light mask
        # Each array is 2048 bytes = 4096 light values (4 bits each, 0-15)
        # Light values: 15 = full brightness, 0 = no light
        packet_writer.write_varint(len(sky_light_sections))
        
        for section_idx in sky_light_sections:
            section_y_min = -64 + (section_idx * 16)
            section_y_max = section_y_min + 15
            
            # Generate light array (4096 values = 16x16x16 blocks)
            light_array = bytearray(2048)  # 2048 bytes = 4096 nibbles
            
            for y in range(16):
                world_y = section_y_min + y
                for z in range(16):
                    for x in range(16):
                        # Calculate index in 4096-element array
                        block_idx = y * 256 + z * 16 + x
                        
                        # Calculate byte and nibble index
                        byte_idx = block_idx // 2
                        is_high_nibble = (block_idx % 2) == 0
                        
                        # Determine light value
                        if flat_world:
                            if world_y >= ground_y:
                                # Above or at ground: full sky light
                                light_value = 15
                            else:
                                # Below ground: no sky light
                                light_value = 0
                        else:
                            light_value = 15  # Default: full light
                        
                        # Pack into byte (high nibble first, then low nibble)
                        if is_high_nibble:
                            light_array[byte_idx] = (light_value << 4) & 0xF0
                        else:
                            light_array[byte_idx] |= light_value & 0x0F
            
            # Write array length (2048) and data
            packet_writer.write_varint(2048)
            packet_writer.write_bytes(bytes(light_array))
        
        # Block Light Arrays: Empty (no block light)
        packet_writer.write_varint(0)
        
        # Build final packet with length prefix
        packet_data = packet_writer.to_bytes()
        final_writer = ProtocolWriter()
        final_writer.write_varint(len(packet_data))
        final_writer.write_bytes(packet_data)
        
        return final_writer.to_bytes()


# Example usage and testing
if __name__ == "__main__":
    # Test parsing the handshake packet from output.txt
    # The hex from output.txt shows the packet AFTER length prefix was read
    # So we need to reconstruct: [Length VarInt] [Packet ID: 0x00] [Payload]
    # Payload from output: 85 06 09 6c 6f 63 61 6c 68 6f 73 74 63 dd 02 (15 bytes)
    # So full packet = 1 byte (packet ID) + 15 bytes (payload) = 16 bytes total
    # Length VarInt for 16 = 0x10 (single byte)
    payload_hex = "85 06 09 6c 6f 63 61 6c 68 6f 73 74 63 dd 02"
    payload_bytes = bytes.fromhex(payload_hex.replace(" ", ""))
    # Reconstruct full packet with length prefix
    writer = ProtocolWriter()
    writer.write_varint(1 + len(payload_bytes))  # Length: packet ID (1) + payload
    writer.write_varint(0)  # Packet ID: 0
    writer.write_bytes(payload_bytes)
    handshake_bytes = writer.to_bytes()
    
    print("Testing Handshake Packet Parsing:")
    print(f"Raw bytes: {handshake_bytes.hex()}")
    print(f"Length: {len(handshake_bytes)} bytes")
    
    try:
        packet_id, handshake = PacketParser.parse_packet(
            handshake_bytes, 
            ConnectionState.HANDSHAKING
        )
        if handshake:
            print(f"Packet ID: {packet_id}")
            print(f"Protocol Version: {handshake.protocol_version}")
            print(f"Server Address: {handshake.server_address}")
            print(f"Server Port: {handshake.server_port}")
            print(f"Intent: {handshake.intent} ({'Status' if handshake.intent == 1 else 'Login' if handshake.intent == 2 else 'Transfer'})")
        else:
            print(f"Packet ID: {packet_id} (not parsed)")
    except Exception as e:
        import traceback
        print(f"Error: {e}")
        traceback.print_exc()
    
    print("\n" + "="*60 + "\n")
    
    # Test parsing login start packet
    # The hex from output.txt shows the packet AFTER length prefix was read
    # Payload from output: 0a 43 6c 65 6d 65 6e 50 69 6e 65 67 0f b6 ce 0b 55 44 8f a9 a9 ed 3f 1d a9 35 08 (27 bytes)
    # So full packet = 1 byte (packet ID) + 27 bytes (payload) = 28 bytes total
    payload_hex = "0a 43 6c 65 6d 65 6e 50 69 6e 65 67 0f b6 ce 0b 55 44 8f a9 a9 ed 3f 1d a9 35 08"
    payload_bytes = bytes.fromhex(payload_hex.replace(" ", ""))
    # Reconstruct full packet with length prefix
    writer = ProtocolWriter()
    writer.write_varint(1 + len(payload_bytes))  # Length: packet ID (1) + payload
    writer.write_varint(0)  # Packet ID: 0
    writer.write_bytes(payload_bytes)
    login_bytes = writer.to_bytes()
    
    print("Testing Login Start Packet Parsing:")
    print(f"Raw bytes: {login_bytes.hex()}")
    print(f"Length: {len(login_bytes)} bytes")
    
    try:
        packet_id, login = PacketParser.parse_packet(
            login_bytes,
            ConnectionState.LOGIN
        )
        if login:
            print(f"Packet ID: {packet_id}")
            print(f"Username: {login.username}")
            print(f"UUID: {login.player_uuid}")
        else:
            print(f"Packet ID: {packet_id} (not parsed)")
    except Exception as e:
        import traceback
        print(f"Error: {e}")
        traceback.print_exc()
    
    print("\n" + "="*60 + "\n")
    
    # Test building login success packet
    print("Testing Login Success Packet Building:")
    # Use the UUID we parsed from login start, or create a test one
    if 'login' in locals() and login:
        test_uuid = login.player_uuid
        test_username = login.username
    else:
        # Fallback test values
        test_uuid = uuid.uuid4()
        test_username = "ClemenPine"
    
    profile = GameProfile(
        uuid=test_uuid,
        username=test_username,
        properties=[]
    )
    
    login_success = PacketBuilder.build_login_success(profile)
    print(f"Login Success packet: {login_success.hex()}")
    print(f"Length: {len(login_success)} bytes")

