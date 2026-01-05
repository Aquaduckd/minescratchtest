#!/usr/bin/env python3
"""
Generate reference chunk data from Python server for comparison with C# implementation.
"""
import sys
import os
import json

# Add PythonServer to path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'PythonServer'))

from PythonServer.minecraft_protocol import PacketBuilder
from PythonServer.block_manager import BlockManager

def generate_chunk_reference(chunk_x=0, chunk_z=0):
    """Generate chunk data packet and save it for comparison."""
    
    # Create block manager (flat world)
    # BlockManager() creates a terrain generator by default
    # We need to disable it to get flat world
    block_manager = BlockManager()
    block_manager.terrain_generator = None  # Force flat world
    
    # Load the chunk first (required for Python BlockManager)
    block_manager.load_chunk(chunk_x, chunk_z, ground_y=64, flat_world=True, use_terrain=False)
    
    # Generate chunk data
    chunk_data = PacketBuilder.build_chunk_data(chunk_x, chunk_z, block_manager)
    
    # Convert to hex for easy comparison
    chunk_hex = chunk_data.hex()
    
    # Parse the packet to get structured data
    # Skip length prefix (first VarInt)
    from PythonServer.minecraft_protocol import ProtocolReader
    reader = ProtocolReader(chunk_data)
    packet_length = reader.read_varint()
    
    # Read packet ID
    packet_id = reader.read_varint()
    assert packet_id == 0x2C, f"Expected packet ID 0x2C, got 0x{packet_id:X}"
    
    # Read chunk coordinates
    chunk_x_read = reader.read_int()
    chunk_z_read = reader.read_int()
    assert chunk_x_read == chunk_x, f"Chunk X mismatch: {chunk_x_read} != {chunk_x}"
    assert chunk_z_read == chunk_z, f"Chunk Z mismatch: {chunk_z_read} != {chunk_z}"
    
    # Read heightmaps
    num_heightmaps = reader.read_varint()
    heightmap_type = reader.read_varint()
    heightmap_data_length = reader.read_varint()
    
    # Read heightmap data
    heightmap_longs = []
    for i in range(heightmap_data_length):
        heightmap_longs.append(reader.read_long())
    
    # Read data size
    data_size = reader.read_varint()
    
    # Read chunk sections data
    chunk_sections_data = reader.read_bytes(data_size)
    
    # Parse chunk sections
    sections_reader = ProtocolReader(chunk_sections_data)
    sections = []
    
    for section_idx in range(24):
        block_count = sections_reader.read_short()
        bits_per_entry = sections_reader.read_byte()
        
        section_info = {
            "section_index": section_idx,
            "block_count": block_count,
            "bits_per_entry": bits_per_entry
        }
        
        if bits_per_entry == 0:
            # Single-value palette
            palette_value = sections_reader.read_varint()
            section_info["palette_type"] = "single_value"
            section_info["palette"] = [palette_value]
            section_info["data_array_length"] = 0
        else:
            # Indirect palette
            palette_length = sections_reader.read_varint()
            palette = []
            for i in range(palette_length):
                palette.append(sections_reader.read_varint())
            
            # Note: As of protocol 1.21.5+, the data array length is NOT written
            # It's calculated from bits_per_entry and number of entries (4096 for blocks)
            # So we calculate it instead of reading it
            entries_per_long = 64 // bits_per_entry
            num_entries = 4096  # Always 4096 for block sections (16x16x16)
            data_array_length = (num_entries + entries_per_long - 1) // entries_per_long
            
            data_array_longs = []
            for i in range(data_array_length):
                data_array_longs.append(sections_reader.read_long())
            
            section_info["palette_type"] = "indirect"
            section_info["palette"] = palette
            section_info["data_array_length"] = data_array_length
            section_info["data_array_longs"] = data_array_longs
        
        # Biomes (single-value)
        biome_bits = sections_reader.read_byte()
        biome_value = sections_reader.read_varint()
        section_info["biome_bits_per_entry"] = biome_bits
        section_info["biome_value"] = biome_value
        
        sections.append(section_info)
    
    # Read block entities
    num_block_entities = reader.read_varint()
    
    # Read light data (simplified - just read remaining bytes for now)
    # Light data format is complex, we'll skip detailed parsing for now
    light_data_remaining = reader.remaining()
    light_data_bytes = reader.read_bytes(light_data_remaining) if light_data_remaining > 0 else b""
    
    # Simplified light data structure
    sky_light_mask = []
    block_light_mask = []
    empty_sky_light_mask = []
    empty_block_light_mask = []
    sky_light_arrays = []
    block_light_arrays = []
    
    reference_data = {
        "chunk_x": chunk_x,
        "chunk_z": chunk_z,
        "packet_length": packet_length,
        "packet_id": packet_id,
        "heightmaps": {
            "count": num_heightmaps,
            "type": heightmap_type,
            "data_length": heightmap_data_length,
            "longs": heightmap_longs
        },
        "chunk_sections": sections,
        "block_entities_count": num_block_entities,
        "light_data": {
            "raw_bytes_length": len(light_data_bytes),
            "note": "Light data parsing simplified for reference comparison"
        },
        "raw_hex": chunk_hex,
        "raw_length": len(chunk_data)
    }
    
    return reference_data

if __name__ == "__main__":
    import argparse
    
    parser = argparse.ArgumentParser(description="Generate reference chunk data from Python server")
    parser.add_argument("--chunk-x", type=int, default=0, help="Chunk X coordinate")
    parser.add_argument("--chunk-z", type=int, default=0, help="Chunk Z coordinate")
    parser.add_argument("--output", type=str, default="chunk_reference.json", help="Output JSON file")
    
    args = parser.parse_args()
    
    print(f"Generating reference chunk data for chunk ({args.chunk_x}, {args.chunk_z})...")
    reference_data = generate_chunk_reference(args.chunk_x, args.chunk_z)
    
    with open(args.output, 'w') as f:
        json.dump(reference_data, f, indent=2)
    
    print(f"Reference data saved to {args.output}")
    print(f"Packet length: {reference_data['packet_length']} bytes")
    print(f"Number of sections: {len(reference_data['chunk_sections'])}")
    print(f"Heightmap data length: {reference_data['heightmaps']['data_length']} longs")

