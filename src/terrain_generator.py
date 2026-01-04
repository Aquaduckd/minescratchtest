#!/usr/bin/env python3
"""
Terrain Generator - Perlin Noise-based Terrain Generation

This module provides terrain generation using Perlin noise for creating
natural-looking landscapes with hills, valleys, and varied terrain.
"""

from typing import Dict, List, Tuple, Optional
import math

try:
    import noise
    NOISE_AVAILABLE = True
except ImportError:
    NOISE_AVAILABLE = False
    print("Warning: 'noise' library not available. Install with: pip install noise")


class TerrainGenerator:
    """
    Generates terrain using Perlin noise.
    
    This class handles:
    - Height map generation per chunk
    - Noise parameter configuration
    - Deterministic generation (seed-based)
    """
    
    def __init__(self, seed: int = 0, 
                 scale: float = 0.03,
                 amplitude: int = 16,
                 base_height: int = 64,
                 octaves: int = 1,
                 persistence: float = 0.5,
                 lacunarity: float = 2.0,
                 mountain_scale: float = 0.01,
                 mountain_amplitude: int = 300,
                 mountain_threshold: float = 0.5):
        """
        Initialize the terrain generator.
        
        Args:
            seed: Random seed for deterministic generation
            scale: Noise scale (lower = larger features, higher = smaller features)
            amplitude: Base height variation range (how much terrain varies)
            base_height: Base Y level for terrain
            octaves: Number of noise layers for detail
            persistence: How much each octave contributes (0-1)
            lacunarity: Frequency multiplier between octaves
            mountain_scale: Scale for mountain placement noise (lower = larger mountain regions)
            mountain_amplitude: Additional amplitude for mountains (added to base amplitude)
            mountain_threshold: Threshold (0-1) above which mountains appear (higher = rarer mountains)
        """
        if not NOISE_AVAILABLE:
            raise ImportError("'noise' library is required. Install with: pip install noise")
        
        self.seed = seed
        self.scale = scale
        self.amplitude = amplitude
        self.base_height = base_height
        self.octaves = octaves
        self.persistence = persistence
        self.lacunarity = lacunarity
        self.mountain_scale = mountain_scale
        self.mountain_amplitude = mountain_amplitude
        self.mountain_threshold = mountain_threshold
        
        # Cache for height maps: {(chunk_x, chunk_z): [[height values]]}
        # Height map is 16x16 (one per block in chunk)
        self.height_map_cache: Dict[Tuple[int, int], List[List[int]]] = {}
    
    def generate_height_map(self, chunk_x: int, chunk_z: int) -> List[List[int]]:
        """
        Generate a height map for a chunk.
        
        Returns a 16x16 grid of height values (Y coordinates) for the chunk.
        Height maps are cached to avoid regeneration.
        
        Args:
            chunk_x: Chunk X coordinate
            chunk_z: Chunk Z coordinate
            
        Returns:
            16x16 list of height values (Y coordinates)
        """
        # Check cache first
        cache_key = (chunk_x, chunk_z)
        if cache_key in self.height_map_cache:
            return self.height_map_cache[cache_key]
        
        # Generate height map
        height_map = []
        
        for z in range(16):
            row = []
            for x in range(16):
                # Convert local chunk coordinates to world coordinates
                world_x = chunk_x * 16 + x
                world_z = chunk_z * 16 + z
                
                # Generate base terrain noise value
                noise_value = self._get_noise(world_x, world_z)
                
                # Generate mountain placement noise (separate, larger-scale noise)
                # This determines where mountains appear
                mountain_noise = self._get_mountain_noise(world_x, world_z)
                
                # Calculate effective amplitude based on mountain noise
                # Most areas use base amplitude, but areas above threshold get extra amplitude
                if mountain_noise > self.mountain_threshold:
                    # In mountain areas: use higher amplitude
                    # Interpolate between base and max amplitude based on how far above threshold
                    mountain_factor = (mountain_noise - self.mountain_threshold) / (1.0 - self.mountain_threshold)
                    effective_amplitude = self.amplitude + int(self.mountain_amplitude * mountain_factor)
                else:
                    # In normal areas: use base amplitude
                    effective_amplitude = self.amplitude
                
                # Convert noise value (-1 to 1) to height using effective amplitude
                height = int(self.base_height + noise_value * effective_amplitude)
                
                # Clamp height to reasonable bounds (e.g., 0-255)
                height = max(0, min(255, height))
                
                row.append(height)
            height_map.append(row)
        
        # Cache the result
        self.height_map_cache[cache_key] = height_map
        
        return height_map
    
    def _get_noise(self, x: float, z: float) -> float:
        """
        Get Perlin noise value at world coordinates.
        
        Uses multiple octaves for more natural-looking terrain.
        
        Args:
            x: World X coordinate
            z: World Z coordinate
            
        Returns:
            Noise value in range [-1, 1]
        """
        # Use seed as offset to ensure different worlds
        # Use smaller multipliers to avoid making noise always return 0
        seed_offset_x = self.seed * 100.0
        seed_offset_z = self.seed * 200.0
        
        total = 0.0
        frequency = self.scale
        amplitude = 1.0
        max_value = 0.0
        
        for _ in range(self.octaves):
            # Generate noise value using pnoise2 (2D Perlin noise)
            # Add seed offset to coordinates for world variation
            noise_value = noise.pnoise2(
                (x * frequency) + seed_offset_x,
                (z * frequency) + seed_offset_z,
                octaves=1
            )
            
            total += noise_value * amplitude
            max_value += amplitude
            
            amplitude *= self.persistence
            frequency *= self.lacunarity
        
        # Normalize to [-1, 1] range
        if max_value > 0:
            total /= max_value
        
        return total
    
    def _get_mountain_noise(self, x: float, z: float) -> float:
        """
        Get mountain placement noise value at world coordinates.
        
        This is a separate, larger-scale noise used to determine where
        mountains appear. Returns value in range [0, 1] (normalized from [-1, 1]).
        
        Args:
            x: World X coordinate
            z: World Z coordinate
            
        Returns:
            Noise value in range [0, 1]
        """
        # Use different seed offset to ensure mountains are independent of base terrain
        seed_offset_x = self.seed * 300.0
        seed_offset_z = self.seed * 400.0
        
        # Use larger scale (lower frequency) for mountain placement
        # This creates large regions where mountains can appear
        noise_value = noise.pnoise2(
            (x * self.mountain_scale) + seed_offset_x,
            (z * self.mountain_scale) + seed_offset_z,
            octaves=2  # Fewer octaves for smoother mountain regions
        )
        
        # Normalize from [-1, 1] to [0, 1]
        return (noise_value + 1.0) / 2.0
    
    def clear_cache(self) -> None:
        """Clear the height map cache."""
        self.height_map_cache.clear()
    
    def get_height_at(self, world_x: int, world_z: int) -> int:
        """
        Get the terrain height at specific world coordinates.
        
        Args:
            world_x: World X coordinate
            world_z: World Z coordinate
            
        Returns:
            Height (Y coordinate) at this position
        """
        # Calculate chunk coordinates
        chunk_x = world_x // 16
        chunk_z = world_z // 16
        
        # Handle negative coordinates correctly
        if world_x < 0:
            chunk_x = (world_x + 1) // 16 - 1
        if world_z < 0:
            chunk_z = (world_z + 1) // 16 - 1
        
        # Get height map for chunk
        height_map = self.generate_height_map(chunk_x, chunk_z)
        
        # Get local coordinates within chunk
        local_x = world_x % 16
        local_z = world_z % 16
        
        # Handle negative coordinates
        if world_x < 0:
            local_x = 15 - ((-world_x - 1) % 16)
        if world_z < 0:
            local_z = 15 - ((-world_z - 1) % 16)
        
        return height_map[local_z][local_x]

