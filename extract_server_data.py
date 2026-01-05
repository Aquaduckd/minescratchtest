#!/usr/bin/env python3
"""
Script to extract all data from Minecraft server.jar and write to JSON files.
This is used to bootstrap a C# implementation by providing all necessary data
in a structured format.

All extracted files are written to extracted_data/ to keep all JSON data
in one new location.

Extracts:
1. Loot tables (block -> item mappings) - from server.jar
2. Biome entries - from server.jar
3. Damage type entries - from server.jar
4. Configuration registries (variant registries, dimension types, etc.) - from server.jar
5. Generates reports using Minecraft data generator (registries.json, blocks.json, items.json, etc.)
6. Copies all generated reports to extracted_data/

Note: Requires Java to be installed to generate reports. If reports already exist in generated/reports/,
they will be reused instead of regenerating.
"""

import os
import json
import zipfile
from pathlib import Path
from typing import Dict, List, Optional, Any

def get_script_dir():
    """Get the directory containing this script."""
    return os.path.dirname(os.path.abspath(__file__))

def get_project_root():
    """Get the project root directory."""
    script_dir = get_script_dir()
    # If script is in PythonServer/, go up one level. Otherwise, script is already in project root.
    if os.path.basename(script_dir) == 'PythonServer':
        return os.path.dirname(script_dir)
    return script_dir

def extract_inner_jar(jar_path: str, inner_jar_path: str) -> bool:
    """
    Extract the inner server JAR from the outer JAR.
    Tries multiple possible paths to find the inner JAR.
    """
    if os.path.exists(inner_jar_path):
        print(f"✓ Inner JAR already exists at {inner_jar_path}")
        return True
    
    if not os.path.exists(jar_path):
        print(f"Error: Server JAR not found at {jar_path}")
        return False
    
    print(f"Extracting inner JAR from {jar_path}...")
    
    # Try different possible paths for the inner JAR
    # Modern Minecraft versions (1.18+) use META-INF/versions/{version}/server-{version}.jar
    possible_paths = [
        'META-INF/versions/1.21.10/server-1.21.10.jar',  # Specific version
        'META-INF/versions/1.21/server-1.21.jar',  # Minor version
    ]
    
    # Also try to auto-detect the version from the jar filename or contents
    try:
        with zipfile.ZipFile(jar_path, 'r') as jar:
            # List all files to find the inner jar path
            all_files = jar.namelist()
            
            # Look for server-*.jar in META-INF/versions/
            inner_jar_candidates = [
                f for f in all_files 
                if f.startswith('META-INF/versions/') 
                and f.endswith('/server-') and '.jar' in f
            ]
            
            # More specific: look for files matching the pattern
            for file in all_files:
                if 'META-INF/versions/' in file and file.endswith('.jar') and 'server-' in file:
                    possible_paths.insert(0, file)  # Add found path to the front
            
            # Try each possible path
            for inner_jar_path_in_jar in possible_paths:
                try:
                    if inner_jar_path_in_jar in all_files:
                        print(f"  → Found inner JAR at: {inner_jar_path_in_jar}")
                        inner_jar_data = jar.read(inner_jar_path_in_jar)
                        
                        # Ensure output directory exists
                        os.makedirs(os.path.dirname(inner_jar_path), exist_ok=True)
                        
                        with open(inner_jar_path, 'wb') as f:
                            f.write(inner_jar_data)
                        print(f"✓ Inner JAR extracted to {inner_jar_path}")
                        return True
                except KeyError:
                    continue  # Try next path
                except Exception as e:
                    print(f"  ⚠ Warning: Could not extract from {inner_jar_path_in_jar}: {e}")
                    continue
            
            # If we get here, no path worked
            print(f"✗ Error: Could not find inner JAR in {jar_path}")
            print(f"  Tried paths: {possible_paths[:3]}...")  # Show first few
            if inner_jar_candidates:
                print(f"  Found candidates: {inner_jar_candidates[:3]}...")
            return False
            
    except zipfile.BadZipFile:
        print(f"✗ Error: {jar_path} is not a valid ZIP file")
        return False
    except Exception as e:
        print(f"✗ Error extracting inner JAR: {e}")
        return False

def extract_loot_tables(inner_jar_path: str, output_dir: str) -> Dict[str, Any]:
    """
    Extract all block loot tables from the inner JAR.
    Returns a dictionary mapping block names to their loot table data.
    """
    loot_tables = {}
    
    if not os.path.exists(inner_jar_path):
        print(f"Error: Inner JAR not found at {inner_jar_path}")
        return loot_tables
    
    try:
        with zipfile.ZipFile(inner_jar_path, 'r') as inner_jar:
            all_files = inner_jar.namelist()
            block_loot_tables = [f for f in all_files 
                               if f.startswith('data/minecraft/loot_table/blocks/') 
                               and f.endswith('.json')]
            
            print(f"Found {len(block_loot_tables)} block loot tables")
            
            for loot_file in block_loot_tables:
                try:
                    loot_data = inner_jar.read(loot_file)
                    loot_json = json.loads(loot_data.decode('utf-8'))
                    
                    # Extract block name from filename
                    block_name = loot_file.split('/')[-1].replace('.json', '')
                    block_id = f"minecraft:{block_name}"
                    
                    # Store the full loot table JSON
                    loot_tables[block_id] = loot_json
                    
                except Exception as e:
                    print(f"  ⚠ Warning: Could not parse loot table {loot_file}: {e}")
                    continue
        
        # Write to JSON file
        output_file = os.path.join(output_dir, 'loot_tables.json')
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(loot_tables, f, indent=2, ensure_ascii=False)
        print(f"✓ Wrote {len(loot_tables)} loot tables to {output_file}")
        
        # Also create a simplified mapping file (block -> default item)
        simplified_mapping = {}
        for block_id, loot_json in loot_tables.items():
            item_name = extract_default_item_from_loot_table(loot_json)
            if item_name:
                simplified_mapping[block_id] = item_name
        
        simplified_file = os.path.join(output_dir, 'loot_table_mappings.json')
        with open(simplified_file, 'w', encoding='utf-8') as f:
            json.dump(simplified_mapping, f, indent=2, ensure_ascii=False)
        print(f"✓ Wrote {len(simplified_mapping)} simplified mappings to {simplified_file}")
        
    except Exception as e:
        print(f"✗ Error extracting loot tables: {e}")
    
    return loot_tables

def extract_default_item_from_loot_table(loot_json: Dict) -> Optional[str]:
    """
    Extract the default item drop from a loot table.
    Returns the item name, or None if not found.
    """
    if 'pools' not in loot_json or len(loot_json['pools']) == 0:
        return None
    
    pool = loot_json['pools'][0]  # Use first pool
    
    if 'entries' not in pool or len(pool['entries']) == 0:
        return None
    
    entry = pool['entries'][0]
    
    # Handle simple item entry
    if entry.get('type') == 'minecraft:item':
        return entry.get('name')
    
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
                        return child.get('name')
            
            # If no non-silk-touch found, use first item
            if len(entry['children']) > 0:
                first_child = entry['children'][0]
                if first_child.get('type') == 'minecraft:item':
                    return first_child.get('name')
    
    return None

def extract_biome_entries(inner_jar_path: str, output_dir: str) -> List[str]:
    """
    Extract all biome entries from the inner JAR.
    Returns a list of biome resource IDs.
    """
    biomes = []
    
    if not os.path.exists(inner_jar_path):
        print(f"Error: Inner JAR not found at {inner_jar_path}")
        return biomes
    
    try:
        with zipfile.ZipFile(inner_jar_path, 'r') as inner_jar:
            all_files = inner_jar.namelist()
            biome_files = [f for f in all_files 
                          if f.startswith('data/minecraft/worldgen/biome/') 
                          and f.endswith('.json')
                          and '/tags/' not in f]
            
            print(f"Found {len(biome_files)} biome files")
            
            for biome_file in biome_files:
                try:
                    filename = os.path.basename(biome_file)
                    entry_name = filename.replace('.json', '')
                    biome_id = f"minecraft:{entry_name}"
                    biomes.append(biome_id)
                except Exception as e:
                    print(f"  ⚠ Warning: Could not process biome {biome_file}: {e}")
                    continue
        
        # Write to JSON file
        output_file = os.path.join(output_dir, 'biomes.json')
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(sorted(biomes), f, indent=2, ensure_ascii=False)
        print(f"✓ Wrote {len(biomes)} biomes to {output_file}")
        
    except Exception as e:
        print(f"✗ Error extracting biomes: {e}")
    
    return biomes

def extract_damage_type_entries(inner_jar_path: str, output_dir: str) -> List[str]:
    """
    Extract all damage type entries from the inner JAR.
    Returns a list of damage type resource IDs.
    """
    damage_types = []
    
    if not os.path.exists(inner_jar_path):
        print(f"Error: Inner JAR not found at {inner_jar_path}")
        return damage_types
    
    try:
        with zipfile.ZipFile(inner_jar_path, 'r') as inner_jar:
            all_files = inner_jar.namelist()
            damage_type_files = [f for f in all_files 
                               if f.startswith('data/minecraft/damage_type/') 
                               and f.endswith('.json')
                               and '/tags/' not in f]
            
            print(f"Found {len(damage_type_files)} damage type files")
            
            for damage_type_file in damage_type_files:
                try:
                    filename = os.path.basename(damage_type_file)
                    entry_name = filename.replace('.json', '')
                    damage_type_id = f"minecraft:{entry_name}"
                    damage_types.append(damage_type_id)
                except Exception as e:
                    print(f"  ⚠ Warning: Could not process damage type {damage_type_file}: {e}")
                    continue
        
        # Write to JSON file
        output_file = os.path.join(output_dir, 'damage_types.json')
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(sorted(damage_types), f, indent=2, ensure_ascii=False)
        print(f"✓ Wrote {len(damage_types)} damage types to {output_file}")
        
    except Exception as e:
        print(f"✗ Error extracting damage types: {e}")
    
    return damage_types

def extract_configuration_registries(inner_jar_path: str, output_dir: str, project_root: str):
    """
    Extract registries needed for configuration phase.
    These registries are in a different format than the protocol ID registries.
    Returns a dict with registry entries in the format needed for configuration.
    """
    import shutil
    
    registry_data = {}
    
    # First, try to copy from data/registry_data.json if it exists
    existing_registry_data_file = os.path.join(project_root, 'data', 'registry_data.json')
    if os.path.exists(existing_registry_data_file):
        try:
            with open(existing_registry_data_file, 'r') as f:
                existing_data = json.load(f)
                # Copy registries that are needed for configuration
                required_registries = [
                    "minecraft:dimension_type",
                    "minecraft:cat_variant",
                    "minecraft:chicken_variant",
                    "minecraft:cow_variant",
                    "minecraft:frog_variant",
                    "minecraft:painting_variant",
                    "minecraft:pig_variant",
                    "minecraft:wolf_variant",
                    "minecraft:wolf_sound_variant",
                    "minecraft:worldgen/biome",
                    "minecraft:damage_type",
                ]
                for reg in required_registries:
                    if reg in existing_data:
                        registry_data[reg] = existing_data[reg]
        except Exception as e:
            print(f"  ⚠ Warning: Could not load existing registry_data.json: {e}")
    
    # Extract missing registries from server.jar
    if not os.path.exists(inner_jar_path):
        print(f"  ⚠ Warning: Inner JAR not found, cannot extract missing registries")
        return registry_data
    
    # Extract variant registries from server.jar
    variant_registries = [
        ("cat_variant", "minecraft:cat_variant"),
        ("chicken_variant", "minecraft:chicken_variant"),
        ("cow_variant", "minecraft:cow_variant"),
        ("frog_variant", "minecraft:frog_variant"),
        ("painting_variant", "minecraft:painting_variant"),
        ("pig_variant", "minecraft:pig_variant"),
        ("wolf_variant", "minecraft:wolf_variant"),
        ("wolf_sound_variant", "minecraft:wolf_sound_variant"),
    ]
    
    try:
        with zipfile.ZipFile(inner_jar_path, 'r') as inner_jar:
            all_files = inner_jar.namelist()
            
            for jar_path, registry_id in variant_registries:
                if registry_id not in registry_data:
                    registry_files = [f for f in all_files 
                                   if f.startswith(f'data/minecraft/{jar_path}/') 
                                   and f.endswith('.json')
                                   and '/tags/' not in f]
                    
                    if registry_files:
                        entries = {}
                        for f in registry_files:
                            try:
                                entry_data = inner_jar.read(f)
                                entry_json = json.loads(entry_data.decode('utf-8'))
                                filename = os.path.basename(f).replace('.json', '')
                                entry_name = f"minecraft:{filename}"
                                entries[entry_name] = entry_json
                            except Exception:
                                continue
                        
                        if entries:
                            registry_data[registry_id] = entries
                            print(f"  ✓ Extracted {registry_id}: {len(entries)} entries")
    except Exception as e:
        print(f"  ⚠ Warning: Could not extract variant registries: {e}")
    
    # Write registry_data.json in the format needed for configuration
    registry_data_file = os.path.join(output_dir, 'registry_data.json')
    with open(registry_data_file, 'w', encoding='utf-8') as f:
        json.dump(registry_data, f, indent=2, ensure_ascii=False)
    print(f"✓ Wrote registry_data.json with {len(registry_data)} registries")
    
    return registry_data

def generate_reports(jar_path: str, project_root: str) -> bool:
    """
    Generate reports using Minecraft's data generator.
    Runs: java -DbundlerMainClass="net.minecraft.data.Main" -jar server.jar --reports
    
    Returns True if successful, False otherwise.
    """
    import subprocess
    
    if not os.path.exists(jar_path):
        print(f"  ⚠ Warning: Server JAR not found at {jar_path}, cannot generate reports")
        return False
    
    generated_dir = os.path.join(project_root, 'generated')
    reports_dir = os.path.join(generated_dir, 'reports')
    
    # Check if reports already exist
    required_files = ['registries.json', 'blocks.json', 'items.json', 'commands.json', 'datapack.json', 'packets.json']
    all_exist = all(os.path.exists(os.path.join(reports_dir, f)) for f in required_files)
    
    if all_exist:
        print(f"  ✓ Reports already exist in {reports_dir}")
        return True
    
    print(f"  → Generating reports using Minecraft data generator...")
    print(f"    This may take a minute...")
    
    try:
        # Run the data generator
        # For Minecraft 1.18+, use: java -DbundlerMainClass="net.minecraft.data.Main" -jar server.jar --reports
        cmd = [
            'java',
            f'-DbundlerMainClass=net.minecraft.data.Main',
            '-jar', jar_path,
            '--reports',
            '--output', generated_dir
        ]
        
        result = subprocess.run(
            cmd,
            capture_output=True,
            text=True,
            timeout=300  # 5 minute timeout
        )
        
        if result.returncode == 0:
            # Verify that reports were generated
            all_exist = all(os.path.exists(os.path.join(reports_dir, f)) for f in required_files)
            if all_exist:
                print(f"  ✓ Reports generated successfully in {reports_dir}")
                return True
            else:
                print(f"  ⚠ Warning: Data generator completed but some reports are missing")
                return False
        else:
            print(f"  ✗ Error: Data generator failed with return code {result.returncode}")
            if result.stderr:
                print(f"    Error output: {result.stderr[:500]}")
            return False
            
    except subprocess.TimeoutExpired:
        print(f"  ✗ Error: Data generator timed out after 5 minutes")
        return False
    except FileNotFoundError:
        print(f"  ✗ Error: Java not found. Please install Java to generate reports.")
        return False
    except Exception as e:
        print(f"  ✗ Error running data generator: {e}")
        return False

def copy_generated_reports(output_dir: str, project_root: str):
    """Copy all JSON files from generated/reports/ to the output directory."""
    import shutil
    
    generated_reports_dir = os.path.join(project_root, 'generated', 'reports')
    files_to_copy = [
        'registries.json',
        'blocks.json',
        'items.json',
        'commands.json',
        'datapack.json',
        'packets.json'
    ]
    
    copied_count = 0
    for filename in files_to_copy:
        source_file = os.path.join(generated_reports_dir, filename)
        dest_file = os.path.join(output_dir, filename)
        
        if os.path.exists(source_file):
            try:
                shutil.copy2(source_file, dest_file)
                copied_count += 1
                file_size = os.path.getsize(dest_file) / 1024  # KB
                print(f"✓ Copied {filename} ({file_size:.1f} KB)")
            except Exception as e:
                print(f"  ⚠ Warning: Could not copy {filename}: {e}")
        else:
            print(f"  ⚠ Warning: {filename} not found in generated/reports/")
    
    return copied_count

def cleanup_temp_files(inner_jar_path: str, project_root: str):
    """
    Remove temporary files and directories created during extraction.
    
    Args:
        inner_jar_path: Path to the temporary inner JAR file
        project_root: Project root directory
    """
    import shutil
    
    removed_count = 0
    
    # Remove temporary inner JAR
    if os.path.exists(inner_jar_path):
        try:
            os.remove(inner_jar_path)
            print(f"✓ Removed temporary inner JAR: {inner_jar_path}")
            removed_count += 1
        except Exception as e:
            print(f"  ⚠ Warning: Could not remove {inner_jar_path}: {e}")
    
    # Remove generated/ directory
    generated_dir = os.path.join(project_root, 'generated')
    if os.path.exists(generated_dir):
        try:
            shutil.rmtree(generated_dir)
            print(f"✓ Removed generated/ directory")
            removed_count += 1
        except Exception as e:
            print(f"  ⚠ Warning: Could not remove {generated_dir}: {e}")
    
    # Remove libraries/ directory
    libraries_dir = os.path.join(project_root, 'libraries')
    if os.path.exists(libraries_dir):
        try:
            shutil.rmtree(libraries_dir)
            print(f"✓ Removed libraries/ directory")
            removed_count += 1
        except Exception as e:
            print(f"  ⚠ Warning: Could not remove {libraries_dir}: {e}")
    
    # Remove data/ directory (server jar is now in project root)
    data_dir = os.path.join(project_root, 'data')
    if os.path.exists(data_dir):
        try:
            shutil.rmtree(data_dir)
            print(f"✓ Removed data/ directory")
            removed_count += 1
        except Exception as e:
            print(f"  ⚠ Warning: Could not remove {data_dir}: {e}")
    
    return removed_count

def create_extraction_summary(output_dir: str):
    """Create a summary document of all extracted data."""
    summary = {
        "extraction_date": None,  # Will be filled by script
        "minecraft_version": "1.21.10",
        "protocol_version": 773,
        "extracted_data": {
            "loot_tables": {
                "description": "Block loot tables extracted from data/minecraft/loot_table/blocks/",
                "files": [
                    "loot_tables.json - Full loot table JSON data for each block",
                    "loot_table_mappings.json - Simplified block -> item mappings"
                ],
                "source": "server.jar -> inner JAR -> data/minecraft/loot_table/blocks/*.json"
            },
            "biomes": {
                "description": "Biome entries extracted from data/minecraft/worldgen/biome/",
                "files": [
                    "biomes.json - List of all biome resource IDs"
                ],
                "source": "server.jar -> inner JAR -> data/minecraft/worldgen/biome/*.json"
            },
            "damage_types": {
                "description": "Damage type entries extracted from data/minecraft/damage_type/",
                "files": [
                    "damage_types.json - List of all damage type resource IDs"
                ],
                "source": "server.jar -> inner JAR -> data/minecraft/damage_type/*.json"
            },
            "registries": {
                "description": "Registry data (copied from generated/reports/)",
                "files": [
                    "registries.json - Full registry data with protocol IDs"
                ],
                "source": "Minecraft data generator reports"
            },
            "blocks": {
                "description": "Block data (copied from generated/reports/)",
                "files": [
                    "blocks.json - Full block data with state IDs"
                ],
                "source": "Minecraft data generator reports"
            },
            "items": {
                "description": "Item data (copied from generated/reports/)",
                "files": [
                    "items.json - Full item data with protocol IDs"
                ],
                "source": "Minecraft data generator reports"
            },
            "commands": {
                "description": "Command data (copied from generated/reports/)",
                "files": [
                    "commands.json - Command definitions"
                ],
                "source": "Minecraft data generator reports"
            },
            "datapack": {
                "description": "Datapack data (copied from generated/reports/)",
                "files": [
                    "datapack.json - Datapack definitions"
                ],
                "source": "Minecraft data generator reports"
            },
            "packets": {
                "description": "Packet data (copied from generated/reports/)",
                "files": [
                    "packets.json - Packet definitions"
                ],
                "source": "Minecraft data generator reports"
            }
        },
        "usage_in_server": {
            "loot_tables": "Used in load_loot_tables() to determine item drops from broken blocks",
            "biomes": "Used in get_registry_entries() for minecraft:worldgen/biome registry",
            "damage_types": "Used in get_registry_entries() for minecraft:damage_type registry",
            "registries": "Used throughout server for protocol ID lookups (items, entities, etc.)",
            "blocks": "Used for block state ID to block name mappings"
        }
    }
    
    from datetime import datetime
    summary["extraction_date"] = datetime.now().isoformat()
    
    summary_file = os.path.join(output_dir, 'extraction_summary.json')
    with open(summary_file, 'w', encoding='utf-8') as f:
        json.dump(summary, f, indent=2, ensure_ascii=False)
    print(f"✓ Wrote extraction summary to {summary_file}")

def main():
    """Main extraction function."""
    print("=" * 70)
    print("Minecraft Server Data Extraction")
    print("=" * 70)
    print()
    
    project_root = get_project_root()
    script_dir = get_script_dir()
    
    jar_path = os.path.join(project_root, 'server-1-21-10.jar')
    inner_jar_path = os.path.join(project_root, 'data', 'temp_inner_server.jar')
    output_dir = os.path.join(project_root, 'extracted_data')
    
    # Create output directory
    os.makedirs(output_dir, exist_ok=True)
    
    # Step 1: Extract inner JAR
    if not extract_inner_jar(jar_path, inner_jar_path):
        print("Cannot proceed without inner JAR")
        return
    
    print()
    
    # Step 2: Extract loot tables
    print("Extracting loot tables...")
    loot_tables = extract_loot_tables(inner_jar_path, output_dir)
    print()
    
    # Step 3: Extract biomes
    print("Extracting biomes...")
    biomes = extract_biome_entries(inner_jar_path, output_dir)
    print()
    
    # Step 4: Extract damage types
    print("Extracting damage types...")
    damage_types = extract_damage_type_entries(inner_jar_path, output_dir)
    print()
    
    # Step 5: Extract configuration registries
    print("Extracting configuration registries...")
    config_registries = extract_configuration_registries(inner_jar_path, output_dir, project_root)
    print()
    
    # Step 6: Generate reports (if needed)
    print("Checking for generated reports...")
    reports_generated = generate_reports(jar_path, project_root)
    if not reports_generated:
        print("  ⚠ Warning: Could not generate reports. Will try to copy existing ones if available.")
    print()
    
    # Step 7: Copy files from generated/reports/
    print("Copying files from generated/reports/...")
    copied_count = copy_generated_reports(output_dir, project_root)
    if copied_count == 0:
        print("  ⚠ Warning: No reports were copied. Make sure reports are generated or available.")
    else:
        print(f"✓ Copied {copied_count} files from generated/reports/")
    print()
    
    # Step 8: Create summary
    print("Creating extraction summary...")
    create_extraction_summary(output_dir)
    print()
    
    # Step 9: Cleanup temporary files and directories
    print("Cleaning up temporary files and directories...")
    cleanup_temp_files(inner_jar_path, project_root)
    print()
    
    print("=" * 70)
    print("Extraction Complete!")
    print("=" * 70)
    print(f"Output directory: {output_dir}")
    print()
    print("All JSON files are now in extracted_data/:")
    print(f"  - loot_tables.json ({len(loot_tables)} entries)")
    print(f"  - loot_table_mappings.json (simplified mappings)")
    print(f"  - biomes.json ({len(biomes)} entries)")
    print(f"  - damage_types.json ({len(damage_types)} entries)")
    print(f"  - registry_data.json ({len(config_registries)} registries for configuration)")
    print(f"  - registries.json (copied - protocol IDs for play phase)")
    print(f"  - blocks.json (copied)")
    print(f"  - items.json (copied)")
    print(f"  - commands.json (copied)")
    print(f"  - datapack.json (copied)")
    print(f"  - packets.json (copied)")
    print(f"  - extraction_summary.json (documentation)")
    print()
    print("Temporary files have been cleaned up.")
    print("=" * 70)

if __name__ == '__main__':
    main()

