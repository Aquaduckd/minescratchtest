#!/usr/bin/env python3
"""
Block Manager - Single Source of Truth for Block Data

This module provides a centralized system for managing all block data in the world.
It serves as the single source of truth for both:
- Protocol packet generation (client communication)
- Server-side game logic (collision detection, physics, etc.)

All block operations should go through this manager to ensure consistency.
"""

from typing import Dict, List, Tuple, Optional
import math

try:
    from .terrain_generator import TerrainGenerator
    TERRAIN_AVAILABLE = True
except ImportError:
    TERRAIN_AVAILABLE = False
    TerrainGenerator = None


class BlockManager:
    """
    Centralized block data manager.
    
    This class stores all block data and provides methods for:
    - Reading blocks at specific coordinates
    - Modifying blocks (breaking, placing)
    - Generating initial chunk data
    - Converting to protocol format
    - Tracking block changes for optimization
    """
    
    def __init__(self):
        """
        Initialize the block manager.
        
        Storage format:
        - Key: (chunk_x, chunk_z, section_y) tuple
        - Value: List of 4096 block state IDs (16x16x16 section)
        - Index calculation: y * 256 + z * 16 + x
        """
        # Block data storage: {(chunk_x, chunk_z, section_y): [4096 block IDs]}
        self.block_data: Dict[Tuple[int, int, int], List[int]] = {}
        
        # Track blocks that have been modified (for cache invalidation)
        # Set of (x, y, z) world coordinates
        self.updated_blocks: set = set()
        
        # Block state ID constants
        self.BLOCK_AIR = 0
        self.BLOCK_DIRT = 2105  # Brown wool (for testing)
        self.BLOCK_GRASS_BLOCK = 2098  # Lime wool (for testing)
        self.BLOCK_STONE = 1
        self.BLOCK_WHITE_WOOL = 2093  # White wool (snow)
        self.BLOCK_YELLOW_WOOL = 2097  # Yellow wool (sand)
        self.BLOCK_WATER = 86  # Water (full water block, level=0)
        
        # Terrain generator (optional, for terrain generation)
        self.terrain_generator: Optional[TerrainGenerator] = None
        if TERRAIN_AVAILABLE:
            # Initialize with default parameters
            self.terrain_generator = TerrainGenerator()
    
    def get_block(self, x: int, y: int, z: int) -> int:
        """
        Get the block state ID at the given world coordinates.
        
        Args:
            x: World X coordinate
            y: World Y coordinate
            z: World Z coordinate
            
        Returns:
            Block state ID (0 for air, 10 for dirt, 9 for grass, etc.)
            Returns 0 (air) if chunk section is not loaded or out of bounds.
        """
        chunk_x, chunk_z, section_y, local_x, local_y, local_z = self._world_to_local_coords(x, y, z)
        
        # Check if chunk section is loaded
        key = (chunk_x, chunk_z, section_y)
        if key not in self.block_data:
            # Chunk not loaded, return air
            return self.BLOCK_AIR
        
        # Get block from section data
        block_data = self.block_data[key]
        idx = self._calculate_block_index(local_x, local_y, local_z)
        if 0 <= idx < len(block_data):
            return block_data[idx]
        return self.BLOCK_AIR
    
    def set_block(self, x: int, y: int, z: int, block_state_id: int) -> bool:
        """
        Set a block at the given world coordinates.
        
        Args:
            x: World X coordinate
            y: World Y coordinate
            z: World Z coordinate
            block_state_id: Block state ID to set
            
        Returns:
            True if block was successfully set, False otherwise
        """
        chunk_x, chunk_z, section_y, local_x, local_y, local_z = self._world_to_local_coords(x, y, z)
        
        # Ensure chunk section is loaded
        key = (chunk_x, chunk_z, section_y)
        if key not in self.block_data:
            # Load the chunk section first (lazy loading)
            block_data = self.generate_initial_chunk_section(chunk_x, chunk_z, section_y, ground_y=64)
            self.block_data[key] = block_data
        
        # Update the block
        block_data = self.block_data[key]
        idx = self._calculate_block_index(local_x, local_y, local_z)
        if 0 <= idx < len(block_data):
            block_data[idx] = block_state_id
            # Mark this block as updated for collision detection optimization
            self.updated_blocks.add((x, y, z))
            return True
        return False
    
    def is_block_solid(self, x: int, y: int, z: int) -> bool:
        """
        Check if a block at the given coordinates is solid.
        
        Args:
            x: World X coordinate
            y: World Y coordinate
            z: World Z coordinate
            
        Returns:
            True if block is solid (not air), False otherwise
        """
        block_id = self.get_block(x, y, z)
        return block_id != self.BLOCK_AIR
    
    def load_chunk(self, chunk_x: int, chunk_z: int, ground_y: int = 64, flat_world: bool = True,
                   use_terrain: bool = False) -> None:
        """
        Load a chunk by generating and storing all block sections.
        
        This should be called when a chunk needs to be loaded for the first time.
        It will generate all 24 sections (y=-64 to 320) and store them.
        
        Args:
            chunk_x: Chunk X coordinate
            chunk_z: Chunk Z coordinate
            ground_y: Y coordinate of ground level (default 64)
            flat_world: If True, generate flat world (dirt at y=63, grass at y=64)
            use_terrain: If True, use terrain generation instead of flat world
        """
        # Overworld has 24 sections (y=-64 to 320)
        for section_idx in range(24):
            block_data = self.generate_initial_chunk_section(chunk_x, chunk_z, section_idx, ground_y, flat_world, use_terrain)
            key = (chunk_x, chunk_z, section_idx)
            self.block_data[key] = block_data
    
    def is_chunk_loaded(self, chunk_x: int, chunk_z: int) -> bool:
        """
        Check if a chunk is loaded (has any sections stored).
        
        Args:
            chunk_x: Chunk X coordinate
            chunk_z: Chunk Z coordinate
            
        Returns:
            True if chunk has at least one section loaded, False otherwise
        """
        # Check if any section exists for this chunk
        for section_y in range(24):
            key = (chunk_x, chunk_z, section_y)
            if key in self.block_data:
                return True
        return False
    
    def get_chunk_section(self, chunk_x: int, chunk_z: int, section_y: int) -> Optional[List[int]]:
        """
        Get the block data for a specific chunk section.
        
        Args:
            chunk_x: Chunk X coordinate
            chunk_z: Chunk Z coordinate
            section_y: Section Y index (0-23, where section_y = (y + 64) // 16)
            
        Returns:
            List of 4096 block state IDs, or None if section is not loaded
        """
        key = (chunk_x, chunk_z, section_y)
        return self.block_data.get(key)
    
    def get_chunk_section_for_protocol(self, chunk_x: int, chunk_z: int, section_y: int) -> Tuple[int, List[int], List[int]]:
        """
        Get chunk section data formatted for protocol packet generation.
        
        This method returns the data needed to build a chunk data packet:
        - Block count (number of non-air blocks)
        - Palette (list of unique block state IDs in this section)
        - Palette indices (list mapping each block to its palette index)
        
        Args:
            chunk_x: Chunk X coordinate
            chunk_z: Chunk Z coordinate
            section_y: Section Y index (0-23)
            
        Returns:
            Tuple of (block_count, palette, palette_indices)
            - block_count: Number of non-air blocks in section
            - palette: List of unique block state IDs
            - palette_indices: List of palette indices (one per block, 4096 total)
            
        Returns (0, [0], [0]*4096) if section is not loaded (all air).
        """
        block_data = self.get_chunk_section(chunk_x, chunk_z, section_y)
        if block_data is None:
            # Section not loaded, return all air
            return (0, [self.BLOCK_AIR], [0] * 4096)
        
        # Count non-air blocks
        block_count = sum(1 for block_id in block_data if block_id != self.BLOCK_AIR)
        
        # Create palette (unique block state IDs)
        unique_blocks = set(block_data)
        palette = sorted(unique_blocks)  # Sort for consistency
        
        # Map block_data to palette indices
        palette_indices = [palette.index(block_id) for block_id in block_data]
        
        return (block_count, palette, palette_indices)
    
    def generate_initial_chunk_section(self, chunk_x: int, chunk_z: int, section_y: int, 
                                      ground_y: int = 64, flat_world: bool = True,
                                      use_terrain: bool = False) -> List[int]:
        """
        Generate initial block data for a chunk section (before any modifications).
        
        This is used when loading a chunk for the first time. It generates
        the default blocks based on world generation rules.
        
        Args:
            chunk_x: Chunk X coordinate
            chunk_z: Chunk Z coordinate
            section_y: Section Y index (0-23)
            ground_y: Y coordinate of ground level (default 64)
            flat_world: If True, generate flat world blocks
            use_terrain: If True, use terrain generation instead of flat world
            
        Returns:
            List of 4096 block state IDs for the section
        """
        section_y_min, section_y_max = self._get_section_y_range(section_y)
        block_data = [self.BLOCK_AIR] * 4096  # 16x16x16 = 4096 blocks
        
        # Terrain generation mode
        if use_terrain and self.terrain_generator is not None:
            # Get height map for this chunk
            height_map = self.terrain_generator.generate_height_map(chunk_x, chunk_z)
            
            # Fill blocks based on height map
            for y in range(16):
                world_y = section_y_min + y
                for z in range(16):
                    for x in range(16):
                        # Get surface height at this position
                        surface_height = height_map[z][x]
                        
                        if world_y > surface_height:
                            # Above surface - air or water
                            if world_y < 64:
                                # Below sea level - fill with water
                                block_data[self._calculate_block_index(x, y, z)] = self.BLOCK_WATER
                            else:
                                # Above sea level - air
                                block_data[self._calculate_block_index(x, y, z)] = self.BLOCK_AIR
                        elif world_y == surface_height:
                            # Surface - determine block based on height and slope
                            surface_block = self._get_surface_block(
                                height_map, x, z, surface_height, chunk_x, chunk_z
                            )
                            block_data[self._calculate_block_index(x, y, z)] = surface_block
                        elif world_y >= surface_height - 3:
                            # Top 3 blocks below surface - dirt
                            block_data[self._calculate_block_index(x, y, z)] = self.BLOCK_DIRT
                        else:
                            # Below dirt layer - stone
                            block_data[self._calculate_block_index(x, y, z)] = self.BLOCK_STONE
        
        # Flat world mode (original behavior)
        elif flat_world and section_y_min <= ground_y <= section_y_max:
            # This section contains ground
            # Generate blocks: dirt at y=63, grass at y=64
            for y in range(16):
                world_y = section_y_min + y
                if world_y == ground_y - 1:  # Dirt layer (y=63)
                    for z in range(16):
                        for x in range(16):
                            idx = self._calculate_block_index(x, y, z)
                            block_data[idx] = self.BLOCK_DIRT
                elif world_y == ground_y:  # Grass layer (y=64)
                    for z in range(16):
                        for x in range(16):
                            idx = self._calculate_block_index(x, y, z)
                            block_data[idx] = self.BLOCK_GRASS_BLOCK
        elif flat_world and section_y_min < ground_y - 1:
            # Section below ground - fill with dirt
            block_data = [self.BLOCK_DIRT] * 4096
        
        return block_data
    
    def _get_surface_block(self, height_map: List[List[int]], x: int, z: int, 
                           surface_height: int, chunk_x: int, chunk_z: int) -> int:
        """
        Determine the surface block type based on height and slope.
        
        Rules:
        - Mountain peaks (high altitude): White wool (snow)
        - Sea level and below: Yellow wool (sand)
        - Gentle slopes: Lime wool (grass)
        - Steep slopes: Brown wool (dirt)
        
        Args:
            height_map: 16x16 height map for the chunk
            x: Local X coordinate (0-15)
            z: Local Z coordinate (0-15)
            surface_height: Height at this position
            chunk_x: Chunk X coordinate
            chunk_z: Chunk Z coordinate
            
        Returns:
            Block state ID for the surface block
        """
        # Sea level threshold (y=64)
        sea_level = 64
        snow_threshold = 90  # Mountains above this height get snow
        steep_slope_threshold = 4  # Height difference for steep slope (increased to allow grass on steeper slopes)
        
        # Check height first
        if surface_height >= snow_threshold:
            # Mountain peaks - white wool (snow)
            return self.BLOCK_WHITE_WOOL
        elif surface_height <= sea_level:
            # Sea level and below - yellow wool (sand)
            return self.BLOCK_YELLOW_WOOL
        
        # For areas between sea level and snow: check slope
        # Calculate slope directly from noise function to avoid chunk boundary artifacts
        world_x = chunk_x * 16 + x + 0.5  # Use center of block for precision
        world_z = chunk_z * 16 + z + 0.5
        max_slope = self.terrain_generator.get_slope_at(world_x, world_z)
        
        # Determine block based on slope
        if max_slope >= steep_slope_threshold:
            # Steep slope - brown wool (dirt)
            return self.BLOCK_DIRT
        else:
            # Gentle slope - lime wool (grass)
            return self.BLOCK_GRASS_BLOCK
    
    def clear_updated_blocks(self) -> None:
        """
        Clear the set of updated blocks.
        
        This should be called after processing block updates (e.g., after
        collision detection has checked for changes).
        """
        self.updated_blocks.clear()
    
    def get_updated_blocks(self) -> set:
        """
        Get the set of blocks that have been modified since last clear.
        
        Returns:
            Set of (x, y, z) tuples representing modified block coordinates
        """
        return self.updated_blocks.copy()  # Return a copy to prevent external modification
    
    def _world_to_chunk_coords(self, x: int, z: int) -> Tuple[int, int]:
        """
        Convert world coordinates to chunk coordinates.
        
        Args:
            x: World X coordinate
            z: World Z coordinate
            
        Returns:
            Tuple of (chunk_x, chunk_z)
        """
        chunk_x = x // 16
        chunk_z = z // 16
        return (chunk_x, chunk_z)
    
    def _world_to_section_y(self, y: int) -> int:
        """
        Convert world Y coordinate to section Y index.
        
        Minecraft sections start at y=-64, so:
        section_y = (y + 64) // 16
        
        Args:
            y: World Y coordinate
            
        Returns:
            Section Y index (0-23)
        """
        return (y + 64) // 16
    
    def _world_to_local_coords(self, x: int, y: int, z: int) -> Tuple[int, int, int, int, int, int]:
        """
        Convert world coordinates to chunk and local coordinates.
        
        Args:
            x: World X coordinate
            y: World Y coordinate
            z: World Z coordinate
            
        Returns:
            Tuple of (chunk_x, chunk_z, section_y, local_x, local_y, local_z)
        """
        chunk_x, chunk_z = self._world_to_chunk_coords(x, z)
        section_y = self._world_to_section_y(y)
        
        # Get local coordinates within chunk
        local_x = x % 16
        if local_x < 0:
            local_x += 16
        local_z = z % 16
        if local_z < 0:
            local_z += 16
        
        # Local Y within section
        section_y_min = -64 + (section_y * 16)
        local_y = y - section_y_min
        
        return (chunk_x, chunk_z, section_y, local_x, local_y, local_z)
    
    def _get_section_y_range(self, section_y: int) -> Tuple[int, int]:
        """
        Get the world Y coordinate range for a section.
        
        Args:
            section_y: Section Y index (0-23)
            
        Returns:
            Tuple of (min_y, max_y) inclusive
        """
        section_y_min = -64 + (section_y * 16)
        section_y_max = section_y_min + 15
        return (section_y_min, section_y_max)
    
    def _calculate_block_index(self, local_x: int, local_y: int, local_z: int) -> int:
        """
        Calculate the array index for a block within a section.
        
        Format: y * 256 + z * 16 + x
        
        Args:
            local_x: Local X coordinate within section (0-15)
            local_y: Local Y coordinate within section (0-15)
            local_z: Local Z coordinate within section (0-15)
            
        Returns:
            Array index (0-4095)
        """
        return local_y * 256 + local_z * 16 + local_x

