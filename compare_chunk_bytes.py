#!/usr/bin/env python3
"""
Compare chunk data packet bytes between Python and C# implementations.
Generates hex dumps for comparison.
"""

import sys
import os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'PythonServer'))

from PythonServer.block_manager import BlockManager
from PythonServer.minecraft_protocol import PacketBuilder

def generate_python_chunk_packet(chunk_x, chunk_z):
    """Generate chunk data packet from Python server."""
    block_manager = BlockManager()
    block_manager.terrain_generator = None
    block_manager.load_chunk(chunk_x, chunk_z, ground_y=64, flat_world=True, use_terrain=False)
    
    # Build chunk data packet
    chunk_packet = PacketBuilder.build_chunk_data(
        chunk_x=chunk_x,
        chunk_z=chunk_z,
        block_manager=block_manager
    )
    
    return chunk_packet

def analyze_packet_structure(packet_bytes, label):
    """Analyze and print packet structure."""
    print(f"\n{'='*60}")
    print(f"{label}")
    print(f"{'='*60}")
    print(f"Total packet length: {len(packet_bytes)} bytes")
    print(f"\nHex dump (first 256 bytes):")
    
    # Print hex dump
    for i in range(0, min(256, len(packet_bytes)), 16):
        hex_part = ' '.join(f'{b:02x}' for b in packet_bytes[i:i+16])
        ascii_part = ''.join(chr(b) if 32 <= b < 127 else '.' for b in packet_bytes[i:i+16])
        print(f"{i:04x}: {hex_part:<48} {ascii_part}")
    
    if len(packet_bytes) > 256:
        print(f"... ({len(packet_bytes) - 256} more bytes)")
    
    # Try to parse structure
    print(f"\nPacket structure analysis:")
    try:
        from PythonServer.minecraft_protocol import ProtocolReader
        
        reader = ProtocolReader(packet_bytes)
        
        # Read packet length
        packet_length = reader.read_varint()
        print(f"  Packet length (VarInt): {packet_length}")
        
        # Read packet ID
        packet_id = reader.read_varint()
        print(f"  Packet ID (VarInt): 0x{packet_id:02X}")
        
        if packet_id == 0x2C:  # Chunk Data
            # Read chunk coordinates
            chunk_x = reader.read_int()
            chunk_z = reader.read_int()
            print(f"  Chunk X: {chunk_x}")
            print(f"  Chunk Z: {chunk_z}")
            
            # Read heightmaps
            num_heightmaps = reader.read_varint()
            print(f"  Number of heightmaps: {num_heightmaps}")
            
            if num_heightmaps > 0:
                heightmap_type = reader.read_varint()
                print(f"    Heightmap type: {heightmap_type}")
                heightmap_length = reader.read_varint()
                print(f"    Heightmap data length (longs): {heightmap_length}")
                heightmap_bytes = reader.read_bytes(heightmap_length * 8)
                print(f"    Heightmap data: {len(heightmap_bytes)} bytes")
            
            # Read chunk data length
            chunk_data_length = reader.read_varint()
            print(f"  Chunk data length: {chunk_data_length} bytes")
            
            # Read chunk sections
            chunk_data_start = reader.offset
            print(f"  Chunk data starts at offset: {chunk_data_start}")
            
            # Analyze first section
            if chunk_data_length > 0:
                section_reader = ProtocolReader(packet_bytes[chunk_data_start:chunk_data_start + min(100, chunk_data_length)])
                
                block_count = section_reader.read_short()
                print(f"    Section 0 - Block count: {block_count}")
                
                bits_per_entry = section_reader.read_byte()
                print(f"    Section 0 - Bits per entry: {bits_per_entry}")
                
                if bits_per_entry == 0:
                    single_value = section_reader.read_varint()
                    print(f"    Section 0 - Single value palette: {single_value}")
                else:
                    palette_length = section_reader.read_varint()
                    print(f"    Section 0 - Palette length: {palette_length}")
                    palette = [section_reader.read_varint() for _ in range(min(palette_length, 10))]
                    print(f"    Section 0 - Palette (first {len(palette)}): {palette}")
                    
                    if palette_length > 10:
                        print(f"    Section 0 - ... ({palette_length - 10} more palette entries)")
                    
                    # Read data array length
                    data_array_length = section_reader.read_varint()
                    print(f"    Section 0 - Data array length (longs): {data_array_length}")
                    
                    # Read first few longs
                    num_longs_to_read = min(3, data_array_length)
                    data_longs = []
                    for i in range(num_longs_to_read):
                        if section_reader.remaining() >= 8:
                            long_val = section_reader.read_long()
                            data_longs.append(long_val)
                            print(f"      Long {i}: 0x{long_val:016X}")
                    
                    if data_array_length > num_longs_to_read:
                        print(f"      ... ({data_array_length - num_longs_to_read} more longs)")
                
                # Biome palette
                biome_bits = section_reader.read_byte()
                print(f"    Section 0 - Biome bits per entry: {biome_bits}")
                if biome_bits == 0:
                    biome_value = section_reader.read_varint()
                    print(f"    Section 0 - Biome single value: {biome_value}")
            
            # Check remaining bytes
            remaining = len(packet_bytes) - reader.offset
            print(f"\n  Remaining bytes after chunk data: {remaining}")
            
    except Exception as e:
        print(f"  Error parsing structure: {e}")
        import traceback
        traceback.print_exc()

if __name__ == "__main__":
    # Test chunk that's crashing: (-2, -1)
    chunk_x = -2
    chunk_z = -1
    
    print("Generating Python chunk packet...")
    python_packet = generate_python_chunk_packet(chunk_x, chunk_z)
    
    analyze_packet_structure(python_packet, "Python Chunk Packet")
    
    # Save to file for C# comparison
    output_file = f"chunk_python_{chunk_x}_{chunk_z}.bin"
    with open(output_file, 'wb') as f:
        f.write(python_packet)
    print(f"\nPython packet saved to: {output_file}")
    print(f"Use this to compare with C# output")

