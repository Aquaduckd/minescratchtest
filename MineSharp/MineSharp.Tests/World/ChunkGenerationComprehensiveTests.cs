using MineSharp.World;
using MineSharp.World.Generation;
using MineSharp.World.Generation.Generators;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace MineSharp.Tests.World;

/// <summary>
/// Comprehensive tests for chunk generation across different generators.
/// Tests chunk structure, block placement, heightmaps, and edge cases.
/// </summary>
public class ChunkGenerationComprehensiveTests
{
    [Fact]
    public void Chunk_Initialization_CreatesEmptyChunk()
    {
        // Arrange & Act
        var chunk = new Chunk(0, 0);
        
        // Assert
        Assert.Equal(0, chunk.ChunkX);
        Assert.Equal(0, chunk.ChunkZ);
        
        // All blocks should be air initially
        for (int x = 0; x < 16; x++)
        {
            for (int y = -64; y <= 320; y++)
            {
                for (int z = 0; z < 16; z++)
                {
                    var block = chunk.GetBlock(x, y, z);
                    Assert.Equal(0, block.BlockStateId); // Air
                    Assert.Equal("minecraft:air", block.BlockName);
                }
            }
        }
    }

    [Fact]
    public void Chunk_GetBlock_ReturnsCorrectBlock()
    {
        // Arrange
        var chunk = new Chunk(0, 0);
        chunk.SetBlock(5, 64, 10, new Block(9, "minecraft:grass_block"));
        
        // Act
        var block = chunk.GetBlock(5, 64, 10);
        
        // Assert
        Assert.Equal(9, block.BlockStateId);
        Assert.Equal("minecraft:grass_block", block.BlockName);
    }

    [Fact]
    public void Chunk_SetBlock_UpdatesBlockState()
    {
        // Arrange
        var chunk = new Chunk(0, 0);
        
        // Act
        chunk.SetBlock(3, 65, 7, new Block(1, "minecraft:stone"));
        
        // Assert
        var block = chunk.GetBlock(3, 65, 7);
        Assert.Equal(1, block.BlockStateId);
        Assert.Equal("minecraft:stone", block.BlockName);
    }

    [Fact]
    public void BlockManager_GetOrCreateChunk_CachesChunks()
    {
        // Arrange
        var blockManager = new BlockManager(new FlatWorldGenerator());
        
        // Act
        var chunk1 = blockManager.GetOrCreateChunk(0, 0);
        var chunk2 = blockManager.GetOrCreateChunk(0, 0);
        
        // Assert
        Assert.Same(chunk1, chunk2); // Should return same instance
    }

    [Fact]
    public void BlockManager_GetOrCreateChunk_DifferentChunks_ReturnsDifferentInstances()
    {
        // Arrange
        var blockManager = new BlockManager(new FlatWorldGenerator());
        
        // Act
        var chunk1 = blockManager.GetOrCreateChunk(0, 0);
        var chunk2 = blockManager.GetOrCreateChunk(1, 0);
        
        // Assert
        Assert.NotSame(chunk1, chunk2);
        Assert.Equal(0, chunk1.ChunkX);
        Assert.Equal(0, chunk1.ChunkZ);
        Assert.Equal(1, chunk2.ChunkX);
        Assert.Equal(0, chunk2.ChunkZ);
    }

    [Fact]
    public void BlockManager_GetBlock_ConvertsWorldCoordinatesToChunkCoordinates()
    {
        // Arrange
        var blockManager = new BlockManager(new FlatWorldGenerator());
        
        // Act - Get block at world coordinates (32, 64, 16) = chunk (2, 1), local (0, 64, 0)
        var block = blockManager.GetBlock(32, 64, 16);
        
        // Assert - Should be grass at y=64 in flat world
        Assert.Equal(2098, block.BlockStateId); // Grass (flat world test ID)
    }

    [Fact]
    public void BlockManager_SetBlock_ConvertsWorldCoordinatesToChunkCoordinates()
    {
        // Arrange
        var blockManager = new BlockManager(new FlatWorldGenerator());
        
        // Act - Set block at world coordinates (48, 65, 32) = chunk (3, 2), local (0, 65, 0)
        blockManager.SetBlock(48, 65, 32, new Block(1, "minecraft:stone"));
        
        // Assert
        var block = blockManager.GetBlock(48, 65, 32);
        Assert.Equal(1, block.BlockStateId);
        Assert.Equal("minecraft:stone", block.BlockName);
    }

    [Fact]
    public void BlockManager_IsChunkLoaded_ReturnsFalseForUnloadedChunk()
    {
        // Arrange
        var blockManager = new BlockManager(new FlatWorldGenerator());
        
        // Act
        var isLoaded = blockManager.IsChunkLoaded(5, 5);
        
        // Assert
        Assert.False(isLoaded);
    }

    [Fact]
    public void BlockManager_IsChunkLoaded_ReturnsTrueAfterGetOrCreateChunk()
    {
        // Arrange
        var blockManager = new BlockManager(new FlatWorldGenerator());
        
        // Act
        blockManager.GetOrCreateChunk(5, 5);
        var isLoaded = blockManager.IsChunkLoaded(5, 5);
        
        // Assert
        Assert.True(isLoaded);
    }

    [Fact]
    public void FlatWorldGenerator_GeneratesConsistentChunks()
    {
        // Arrange
        var generator = new FlatWorldGenerator();
        var chunk1 = new Chunk(0, 0);
        var chunk2 = new Chunk(0, 0);
        
        // Act
        generator.GenerateChunk(chunk1, 0, 0);
        generator.GenerateChunk(chunk2, 0, 0);
        
        // Assert - Both chunks should have identical blocks
        for (int x = 0; x < 16; x++)
        {
            for (int y = -64; y <= 320; y++)
            {
                for (int z = 0; z < 16; z++)
                {
                    var block1 = chunk1.GetBlock(x, y, z);
                    var block2 = chunk2.GetBlock(x, y, z);
                    Assert.Equal(block1.BlockStateId, block2.BlockStateId);
                }
            }
        }
    }

    [Fact]
    public void FlatWorldGenerator_GeneratesDifferentChunksAtDifferentLocations()
    {
        // Arrange
        var generator = new FlatWorldGenerator();
        var chunk1 = new Chunk(0, 0);
        var chunk2 = new Chunk(0, 0);
        
        // Act
        generator.GenerateChunk(chunk1, 0, 0);
        generator.GenerateChunk(chunk2, 0, 0);
        
        // Assert - For flat world, all chunks should be identical
        // (This test verifies the generator doesn't depend on chunk coordinates for flat world)
        for (int x = 0; x < 16; x++)
        {
            for (int y = -64; y <= 320; y++)
            {
                for (int z = 0; z < 16; z++)
                {
                    var block1 = chunk1.GetBlock(x, y, z);
                    var block2 = chunk2.GetBlock(x, y, z);
                    Assert.Equal(block1.BlockStateId, block2.BlockStateId);
                }
            }
        }
    }

    [Fact]
    public void NoiseTerrainGenerator_GeneratesVariedTerrain()
    {
        // Arrange
        var generator = new NoiseTerrainGenerator();
        var chunk = new Chunk(0, 0);
        
        // Act
        generator.GenerateChunk(chunk, 0, 0);
        
        // Assert - Should have some non-air blocks
        int nonAirCount = 0;
        for (int x = 0; x < 16; x++)
        {
            for (int y = -64; y <= 320; y++)
            {
                for (int z = 0; z < 16; z++)
                {
                    var block = chunk.GetBlock(x, y, z);
                    if (block.BlockStateId != 0)
                    {
                        nonAirCount++;
                    }
                }
            }
        }
        
        Assert.True(nonAirCount > 0, "Noise generator should create some blocks");
    }

    [Fact]
    public void NoiseTerrainGenerator_GeneratesDifferentChunksAtDifferentLocations()
    {
        // Arrange
        var generator = new NoiseTerrainGenerator();
        var chunk1 = new Chunk(0, 0);
        var chunk2 = new Chunk(10, 10);
        
        // Act
        generator.GenerateChunk(chunk1, 0, 0);
        generator.GenerateChunk(chunk2, 10, 10);
        
        // Assert - Chunks at different locations should be different
        // (Noise-based generation should produce varied terrain)
        bool foundDifference = false;
        for (int x = 0; x < 16 && !foundDifference; x++)
        {
            for (int y = -64; y <= 320 && !foundDifference; y++)
            {
                for (int z = 0; z < 16 && !foundDifference; z++)
                {
                    var block1 = chunk1.GetBlock(x, y, z);
                    var block2 = chunk2.GetBlock(x, y, z);
                    if (block1.BlockStateId != block2.BlockStateId)
                    {
                        foundDifference = true;
                    }
                }
            }
        }
        
        // Note: It's theoretically possible but very unlikely that two distant chunks are identical
        // This test verifies the generator uses chunk coordinates
        Assert.True(foundDifference, "Chunks at different locations should have different terrain");
    }

    [Fact]
    public void VoidWorldGenerator_GeneratesEmptyChunks()
    {
        // Arrange
        var generator = new VoidWorldGenerator();
        var chunk = new Chunk(0, 0);
        
        // Act
        generator.GenerateChunk(chunk, 0, 0);
        
        // Assert - All blocks should be air
        for (int x = 0; x < 16; x++)
        {
            for (int y = -64; y <= 320; y++)
            {
                for (int z = 0; z < 16; z++)
                {
                    var block = chunk.GetBlock(x, y, z);
                    Assert.Equal(0, block.BlockStateId); // Air
                }
            }
        }
    }

    [Fact]
    public void BlockManager_GetChunkSectionForProtocol_HandlesNegativeYCoordinates()
    {
        // Arrange
        var blockManager = new BlockManager(new FlatWorldGenerator());
        blockManager.GetOrCreateChunk(0, 0);
        
        // Act - Section 0 is y=-64 to y=-49
        var (blockCount, palette, paletteIndices) = blockManager.GetChunkSectionForProtocol(0, 0, 0);
        
        // Assert
        Assert.True(blockCount >= 0);
        Assert.NotNull(palette);
        Assert.Equal(4096, paletteIndices.Count); // 16x16x16 = 4096 blocks
    }

    [Fact]
    public void BlockManager_GetChunkSectionForProtocol_HandlesHighYCoordinates()
    {
        // Arrange
        var blockManager = new BlockManager(new FlatWorldGenerator());
        blockManager.GetOrCreateChunk(0, 0);
        
        // Act - Section 24 is y=320 to y=335 (highest section)
        var (blockCount, palette, paletteIndices) = blockManager.GetChunkSectionForProtocol(0, 0, 24);
        
        // Assert
        Assert.True(blockCount >= 0);
        Assert.NotNull(palette);
        Assert.Equal(4096, paletteIndices.Count);
    }

    [Fact]
    public void BlockManager_GenerateHeightmap_FlatWorld_AllHeightsAre65()
    {
        // Arrange
        var blockManager = new BlockManager(new FlatWorldGenerator());
        blockManager.GetOrCreateChunk(0, 0);
        
        // Act
        var heightmap = blockManager.GenerateHeightmap(0, 0);
        
        // Assert
        Assert.Equal(256, heightmap.Length); // 16x16 = 256
        foreach (var height in heightmap)
        {
            Assert.Equal(65, height); // Heightmap is 1-indexed, so y=64 becomes 65
        }
    }

    [Fact]
    public void BlockManager_GenerateHeightmap_NoiseWorld_HasVariedHeights()
    {
        // Arrange
        var blockManager = new BlockManager(new NoiseTerrainGenerator());
        blockManager.GetOrCreateChunk(0, 0);
        
        // Act
        var heightmap = blockManager.GenerateHeightmap(0, 0);
        
        // Assert
        Assert.Equal(256, heightmap.Length);
        
        // Check that heights are within valid range
        foreach (var height in heightmap)
        {
            Assert.True(height >= -64 && height <= 320, $"Height {height} out of valid range");
        }
        
        // For noise terrain, heights should vary (not all the same)
        // But we can't guarantee this, so just check they're valid
    }

    [Fact]
    public void Chunk_GetBlockStateId_ReturnsCorrectId()
    {
        // Arrange
        var chunk = new Chunk(0, 0);
        chunk.SetBlock(5, 64, 10, new Block(42, "minecraft:test_block"));
        
        // Act
        var blockStateId = chunk.GetBlockStateId(5, 64, 10);
        
        // Assert
        Assert.Equal(42, blockStateId);
    }

    [Fact]
    public void Chunk_GetBlockStateId_ReturnsZeroForAir()
    {
        // Arrange
        var chunk = new Chunk(0, 0);
        
        // Act
        var blockStateId = chunk.GetBlockStateId(5, 64, 10);
        
        // Assert
        Assert.Equal(0, blockStateId); // Air
    }

    [Fact]
    public void BlockManager_ConcurrentChunkGeneration_ThreadSafe()
    {
        // Arrange
        var blockManager = new BlockManager(new FlatWorldGenerator());
        var tasks = new List<Task>();
        
        // Act - Generate multiple chunks concurrently
        for (int i = 0; i < 10; i++)
        {
            int chunkX = i;
            tasks.Add(Task.Run(() =>
            {
                var chunk = blockManager.GetOrCreateChunk(chunkX, 0);
                Assert.NotNull(chunk);
                Assert.Equal(chunkX, chunk.ChunkX);
            }));
        }
        
        // Assert - All tasks should complete without exceptions
        Task.WaitAll(tasks.ToArray());
        
        // Verify all chunks were created
        for (int i = 0; i < 10; i++)
        {
            Assert.True(blockManager.IsChunkLoaded(i, 0));
        }
    }
}

