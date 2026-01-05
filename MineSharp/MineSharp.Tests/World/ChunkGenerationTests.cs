using MineSharp.World;
using Xunit;

namespace MineSharp.Tests.World;

public class ChunkGenerationTests
{
    [Fact]
    public void GenerateFlatWorldChunk_CreatesGrassAtY64()
    {
        // Arrange
        var blockManager = new BlockManager(useTerrainGeneration: false);
        
        // Act
        var chunk = blockManager.GetOrCreateChunk(0, 0);
        
        // Assert
        // Check that grass blocks exist at y=64
        for (int x = 0; x < 16; x++)
        {
            for (int z = 0; z < 16; z++)
            {
                var block = chunk.GetBlock(x, 64, z);
                Assert.Equal(2098, block.BlockStateId); // Grass block (Python test ID)
                Assert.Equal("minecraft:grass_block", block.BlockName);
            }
        }
        
        // Check that air exists above (y > 64) and dirt exists below (y < 64)
        var airAbove = chunk.GetBlock(0, 65, 0);
        Assert.Equal(0, airAbove.BlockStateId); // Air
        
        var dirtBelow = chunk.GetBlock(0, 63, 0);
        Assert.Equal(2105, dirtBelow.BlockStateId); // Dirt (flat world has dirt at y=63)
    }

    [Fact]
    public void GetChunkSectionForProtocol_ReturnsCorrectData()
    {
        // Arrange
        var blockManager = new BlockManager(useTerrainGeneration: false);
        blockManager.GetOrCreateChunk(0, 0); // Generate chunk
        
        // Act
        var (blockCount, palette, paletteIndices) = blockManager.GetChunkSectionForProtocol(0, 0, 8); // Section 8 contains y=64
        
        // Assert
        Assert.True(blockCount > 0); // Should have grass blocks
        Assert.Contains(2098, palette); // Grass block in palette (Python test ID)
        Assert.Contains(0, palette); // Air in palette
        Assert.Equal(4096, paletteIndices.Count); // 16x16x16 = 4096 blocks
        
        // Validate all palette indices are in valid range
        foreach (var idx in paletteIndices)
        {
            Assert.True(idx >= 0 && idx < palette.Count, $"Palette index {idx} out of range [0, {palette.Count - 1}]");
        }
    }

    [Fact]
    public void GetChunkSectionForProtocol_AirSection_ReturnsSingleValuePalette()
    {
        // Arrange
        var blockManager = new BlockManager(useTerrainGeneration: false);
        blockManager.GetOrCreateChunk(0, 0); // Generate chunk
        
        // Act - Section 4 is y=0-15, should be all dirt (below ground level y=64)
        var (blockCount, palette, paletteIndices) = blockManager.GetChunkSectionForProtocol(0, 0, 4);
        
        // Assert
        Assert.Equal(4096, blockCount); // All dirt blocks (flat world fills below y=63 with dirt)
        Assert.Single(palette); // Single-value palette (dirt)
        Assert.Equal(2105, palette[0]); // Dirt block state ID (Python test ID)
        Assert.Equal(4096, paletteIndices.Count); // 16x16x16 = 4096 blocks
        
        // All indices should be 0 (air)
        foreach (var idx in paletteIndices)
        {
            Assert.Equal(0, idx);
        }
    }

    [Fact]
    public void GenerateHeightmap_Returns256Entries()
    {
        // Arrange
        var blockManager = new BlockManager(useTerrainGeneration: false);
        blockManager.GetOrCreateChunk(0, 0); // Generate chunk
        
        // Act
        var heightmap = blockManager.GenerateHeightmap(0, 0);
        
        // Assert
        Assert.Equal(256, heightmap.Length); // 16x16 = 256 columns
        // For flat world, all should be 64
        foreach (var height in heightmap)
        {
            Assert.Equal(64, height);
        }
    }

    [Fact]
    public void ChunkManager_WorldToChunk_ConvertsCorrectly()
    {
        // Arrange
        var chunkManager = new ChunkManager();
        
        // Act
        var (chunkX, chunkZ) = chunkManager.WorldToChunk(0.0f, 0.0f);
        
        // Assert
        Assert.Equal(0, chunkX);
        Assert.Equal(0, chunkZ);
        
        // Test other coordinates
        var (chunkX2, chunkZ2) = chunkManager.WorldToChunk(32.0f, -16.0f);
        Assert.Equal(2, chunkX2);
        Assert.Equal(-1, chunkZ2);
    }

    [Fact]
    public void ChunkManager_GetChunksInRange_ReturnsCorrectChunks()
    {
        // Arrange
        var chunkManager = new ChunkManager(viewDistance: 2);
        
        // Act
        var chunks = chunkManager.GetChunksInRange(0, 0);
        
        // Assert
        Assert.True(chunks.Count > 0);
        Assert.Contains((0, 0), chunks); // Center chunk
        Assert.Contains((1, 0), chunks); // Adjacent chunk
        Assert.Contains((0, 1), chunks); // Adjacent chunk
        Assert.Contains((-1, 0), chunks); // Adjacent chunk
        Assert.Contains((0, -1), chunks); // Adjacent chunk
    }
}

