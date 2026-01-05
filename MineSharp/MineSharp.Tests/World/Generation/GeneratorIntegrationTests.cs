using MineSharp.World;
using MineSharp.World.Generation;
using MineSharp.World.Generation.Generators;
using Xunit;

namespace MineSharp.Tests.World.Generation;

/// <summary>
/// Integration tests for terrain generators with BlockManager and World.
/// </summary>
public class GeneratorIntegrationTests
{
    [Fact]
    public void BlockManager_WithFlatGenerator_GeneratesFlatWorld()
    {
        // Arrange
        var generator = new FlatWorldGenerator();
        var blockManager = new BlockManager(generator);

        // Act
        var chunk = blockManager.GetOrCreateChunk(0, 0);

        // Assert
        Assert.NotNull(chunk);
        Assert.Equal(2098, chunk.GetBlock(0, 64, 0).BlockStateId); // Grass
        Assert.Equal(2105, chunk.GetBlock(0, 63, 0).BlockStateId); // Dirt
        Assert.Equal(0, chunk.GetBlock(0, 65, 0).BlockStateId); // Air
    }

    [Fact]
    public void BlockManager_WithFlatGenerator_GeneratesCorrectHeightmap()
    {
        // Arrange
        var generator = new FlatWorldGenerator();
        var blockManager = new BlockManager(generator);

        // Act
        var heightmap = blockManager.GenerateHeightmap(0, 0);

        // Assert
        Assert.NotNull(heightmap);
        Assert.Equal(256, heightmap.Length);
        Assert.All(heightmap, value => Assert.Equal(65, value));
    }

    [Fact]
    public void World_WithFlatGenerator_UsesGenerator()
    {
        // Arrange
        var generator = new FlatWorldGenerator();

        // Act
        var world = new MineSharp.World.World(viewDistance: 10, generator: generator);

        // Assert
        Assert.NotNull(world.BlockManager);
        var chunk = world.BlockManager.GetOrCreateChunk(0, 0);
        Assert.Equal(2098, chunk.GetBlock(0, 64, 0).BlockStateId); // Grass
    }

    [Fact]
    public void World_WithoutGenerator_DefaultsToFlat()
    {
        // Arrange & Act
        var world = new MineSharp.World.World(viewDistance: 10, generator: null);

        // Assert - Should default to flat world generator
        Assert.NotNull(world.BlockManager);
        var chunk = world.BlockManager.GetOrCreateChunk(0, 0);
        Assert.Equal(2098, chunk.GetBlock(0, 64, 0).BlockStateId); // Grass
    }

    [Fact]
    public void World_WithVoidGenerator_GeneratesVoidWorld()
    {
        // Arrange
        var generator = new VoidWorldGenerator();

        // Act
        var world = new MineSharp.World.World(viewDistance: 10, generator: generator);
        var chunk = world.BlockManager.GetOrCreateChunk(0, 0);

        // Assert - All blocks should be air
        Assert.Equal(0, chunk.GetBlock(0, 64, 0).BlockStateId);
        Assert.Equal(0, chunk.GetBlock(0, 0, 0).BlockStateId);
        Assert.Equal(0, chunk.GetBlock(15, 15, 100).BlockStateId);
    }

    [Fact]
    public void BlockManager_WithGenerator_CachesChunks()
    {
        // Arrange
        var generator = new FlatWorldGenerator();
        var blockManager = new BlockManager(generator);

        // Act
        var chunk1 = blockManager.GetOrCreateChunk(0, 0);
        var chunk2 = blockManager.GetOrCreateChunk(0, 0);

        // Assert - Should return the same chunk instance
        Assert.Same(chunk1, chunk2);
    }

    [Fact]
    public void BlockManager_WithGenerator_CreatesDifferentChunks()
    {
        // Arrange
        var generator = new FlatWorldGenerator();
        var blockManager = new BlockManager(generator);

        // Act
        var chunk1 = blockManager.GetOrCreateChunk(0, 0);
        var chunk2 = blockManager.GetOrCreateChunk(1, 0);
        var chunk3 = blockManager.GetOrCreateChunk(0, 1);

        // Assert - Should be different instances
        Assert.NotSame(chunk1, chunk2);
        Assert.NotSame(chunk1, chunk3);
        Assert.NotSame(chunk2, chunk3);
        
        // But all should have the same generation (flat world)
        Assert.Equal(2098, chunk1.GetBlock(0, 64, 0).BlockStateId);
        Assert.Equal(2098, chunk2.GetBlock(0, 64, 0).BlockStateId);
        Assert.Equal(2098, chunk3.GetBlock(0, 64, 0).BlockStateId);
    }

    [Fact]
    public void BlockManager_IsChunkLoaded_ReturnsCorrectValue()
    {
        // Arrange
        var generator = new FlatWorldGenerator();
        var blockManager = new BlockManager(generator);

        // Act & Assert - Initially not loaded
        Assert.False(blockManager.IsChunkLoaded(0, 0));
        
        // Load chunk
        blockManager.GetOrCreateChunk(0, 0);
        Assert.True(blockManager.IsChunkLoaded(0, 0));
        
        // Other chunks still not loaded
        Assert.False(blockManager.IsChunkLoaded(1, 0));
    }
}

