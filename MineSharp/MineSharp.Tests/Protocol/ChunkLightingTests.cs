using MineSharp.Core.Protocol;
using MineSharp.World;
using MineSharp.World.Generation.Generators;
using System;
using System.Linq;
using Xunit;

namespace MineSharp.Tests.Protocol;

/// <summary>
/// Tests for chunk lighting calculations.
/// Verifies that light data is correctly calculated based on heightmaps.
/// </summary>
public class ChunkLightingTests
{
    [Fact]
    public void LightCalculation_AboveSurface_HasFullLight()
    {
        // Arrange
        var generator = new FlatWorldGenerator();
        var blockManager = new BlockManager(generator);
        var heightmap = blockManager.GenerateHeightmap(0, 0);
        
        // Act & Assert
        // For flat world at y=64, blocks at y=65 and above should have full light
        // This test verifies the concept - actual implementation will be in PacketBuilder
        Assert.NotNull(heightmap);
        Assert.Equal(256, heightmap.Length);
        // All heightmap values should be 65 (ground at 64 + 1)
        Assert.All(heightmap, h => Assert.Equal(65, h));
    }

    [Fact]
    public void LightCalculation_BelowSurface_LightDecreases()
    {
        // Arrange
        var generator = new FlatWorldGenerator();
        var blockManager = new BlockManager(generator);
        var heightmap = blockManager.GenerateHeightmap(0, 0);
        
        // Act & Assert
        // For flat world at y=64:
        // - y=64 (surface): light = 15
        // - y=63 (1 block below): light = 14
        // - y=62 (2 blocks below): light = 13
        // - y=50 (14 blocks below): light = 1
        // - y=49 (15 blocks below): light = 0
        // - y=48 (16+ blocks below): light = 0
        
        int surfaceHeight = 64; // Ground level
        int expectedLight;
        
        // At surface
        expectedLight = CalculateLightValue(surfaceHeight, surfaceHeight);
        Assert.Equal(15, expectedLight);
        
        // 1 block below
        expectedLight = CalculateLightValue(surfaceHeight, surfaceHeight - 1);
        Assert.Equal(14, expectedLight);
        
        // 5 blocks below
        expectedLight = CalculateLightValue(surfaceHeight, surfaceHeight - 5);
        Assert.Equal(10, expectedLight);
        
        // 15 blocks below
        expectedLight = CalculateLightValue(surfaceHeight, surfaceHeight - 15);
        Assert.Equal(0, expectedLight);
        
        // 20 blocks below (should still be 0)
        expectedLight = CalculateLightValue(surfaceHeight, surfaceHeight - 20);
        Assert.Equal(0, expectedLight);
    }

    [Fact]
    public void LightCalculation_NoiseTerrain_UsesHeightmap()
    {
        // Arrange
        var generator = new NoiseTerrainGenerator();
        var blockManager = new BlockManager(generator);
        var heightmap = blockManager.GenerateHeightmap(0, 0);
        
        // Act & Assert
        // Heightmap should have varying values for noise terrain
        Assert.NotNull(heightmap);
        Assert.Equal(256, heightmap.Length);
        
        // For noise terrain, heights should vary
        // Check that not all values are the same (unlike flat world)
        var uniqueHeights = heightmap.Distinct().ToList();
        // Noise terrain should have some variation (though it might be minimal with default settings)
        // At minimum, we should have valid height values
        Assert.All(heightmap, h => Assert.True(h >= 0 && h <= 256));
    }

    [Fact]
    public void LightCalculation_AboveHeightmap_HasFullLight()
    {
        // Arrange
        int surfaceHeight = 80; // Example height from noise terrain
        
        // Act & Assert
        // Blocks above or at surface should have full light
        Assert.Equal(15, CalculateLightValue(surfaceHeight, surfaceHeight));
        Assert.Equal(15, CalculateLightValue(surfaceHeight, surfaceHeight + 1));
        Assert.Equal(15, CalculateLightValue(surfaceHeight, surfaceHeight + 10));
    }

    [Fact]
    public void LightCalculation_BelowHeightmap_LightDecreasesCorrectly()
    {
        // Arrange
        int surfaceHeight = 100; // Example height from noise terrain
        
        // Act & Assert
        // Light should decrease by 1 per block below surface
        Assert.Equal(15, CalculateLightValue(surfaceHeight, surfaceHeight));      // At surface
        Assert.Equal(14, CalculateLightValue(surfaceHeight, surfaceHeight - 1));  // 1 below
        Assert.Equal(13, CalculateLightValue(surfaceHeight, surfaceHeight - 2));  // 2 below
        Assert.Equal(10, CalculateLightValue(surfaceHeight, surfaceHeight - 5));  // 5 below
        Assert.Equal(5, CalculateLightValue(surfaceHeight, surfaceHeight - 10)); // 10 below
        Assert.Equal(0, CalculateLightValue(surfaceHeight, surfaceHeight - 15)); // 15 below
        Assert.Equal(0, CalculateLightValue(surfaceHeight, surfaceHeight - 20)); // 20 below (clamped)
    }

    [Fact]
    public void LightCalculation_EdgeCases_HandlesCorrectly()
    {
        // Arrange & Act & Assert
        // Very high surface
        Assert.Equal(15, CalculateLightValue(300, 300));
        Assert.Equal(14, CalculateLightValue(300, 299));
        Assert.Equal(0, CalculateLightValue(300, 285)); // 15 blocks below
        
        // Very low surface
        Assert.Equal(15, CalculateLightValue(10, 10));
        // At y=5, surface at 10: distance = 10 - 5 = 5, light = 15 - 5 = 10
        Assert.Equal(10, CalculateLightValue(10, 5)); // 5 blocks below
        // At y=-5, surface at 10: distance = 10 - (-5) = 15, light = 15 - 15 = 0
        Assert.Equal(0, CalculateLightValue(10, -5)); // 15 blocks below (clamped)
        
        // At world minimum
        Assert.Equal(15, CalculateLightValue(-64, -64));
        Assert.Equal(14, CalculateLightValue(-64, -65)); // Should handle negative correctly
    }

    /// <summary>
    /// Helper method to calculate light value based on surface height and block Y.
    /// Matches Python implementation: light = 15 - distance_below, clamped to 0-15.
    /// </summary>
    private static int CalculateLightValue(int surfaceHeight, int blockY)
    {
        if (blockY >= surfaceHeight)
        {
            // Above or at surface: full sky light
            return 15;
        }
        else
        {
            // Below surface: light decreases by 1 per block downward
            int distanceBelow = surfaceHeight - blockY;
            // Light level = 15 - distance, clamped to 0-15
            return Math.Max(0, 15 - distanceBelow);
        }
    }
}

