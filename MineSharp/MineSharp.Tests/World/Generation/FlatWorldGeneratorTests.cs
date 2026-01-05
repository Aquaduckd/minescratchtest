using MineSharp.World;
using MineSharp.World.Generation.Generators;
using Xunit;

namespace MineSharp.Tests.World.Generation;

public class FlatWorldGeneratorTests
{
    [Fact]
    public void GeneratorId_IsFlat()
    {
        var generator = new FlatWorldGenerator();
        Assert.Equal("flat", generator.GeneratorId);
    }

    [Fact]
    public void DisplayName_IsFlatWorld()
    {
        var generator = new FlatWorldGenerator();
        Assert.Equal("Flat World", generator.DisplayName);
    }

    [Fact]
    public void GenerateChunk_CreatesGrassAtY64()
    {
        // Arrange
        var generator = new FlatWorldGenerator();
        var chunk = new Chunk(0, 0);

        // Act
        generator.GenerateChunk(chunk, 0, 0);

        // Assert - Grass should be at y=64
        var block = chunk.GetBlock(0, 64, 0);
        Assert.Equal(2098, block.BlockStateId); // Grass block ID
        Assert.Equal("minecraft:grass_block", block.BlockName);
    }

    [Fact]
    public void GenerateChunk_CreatesDirtAtY63()
    {
        // Arrange
        var generator = new FlatWorldGenerator();
        var chunk = new Chunk(0, 0);

        // Act
        generator.GenerateChunk(chunk, 0, 0);

        // Assert - Dirt should be at y=63
        var block = chunk.GetBlock(0, 63, 0);
        Assert.Equal(2105, block.BlockStateId); // Dirt block ID
        Assert.Equal("minecraft:dirt", block.BlockName);
    }

    [Fact]
    public void GenerateChunk_FillsBelowY63WithDirt()
    {
        // Arrange
        var generator = new FlatWorldGenerator();
        var chunk = new Chunk(0, 0);

        // Act
        generator.GenerateChunk(chunk, 0, 0);

        // Assert - All blocks below y=63 should be dirt
        for (int y = -64; y < 63; y++)
        {
            var block = chunk.GetBlock(0, y, 0);
            Assert.Equal(2105, block.BlockStateId); // Dirt block ID
        }
    }

    [Fact]
    public void GenerateChunk_LeavesAirAboveY64()
    {
        // Arrange
        var generator = new FlatWorldGenerator();
        var chunk = new Chunk(0, 0);

        // Act
        generator.GenerateChunk(chunk, 0, 0);

        // Assert - All blocks above y=64 should be air
        for (int y = 65; y <= 100; y++) // Test a range above ground
        {
            var block = chunk.GetBlock(0, y, 0);
            Assert.Equal(0, block.BlockStateId); // Air block ID
            Assert.True(block.IsAir());
        }
    }

    [Fact]
    public void GenerateChunk_FillsEntireChunk()
    {
        // Arrange
        var generator = new FlatWorldGenerator();
        var chunk = new Chunk(0, 0);

        // Act
        generator.GenerateChunk(chunk, 0, 0);

        // Assert - Check multiple positions across the chunk
        // Grass at y=64
        Assert.Equal(2098, chunk.GetBlock(0, 64, 0).BlockStateId);
        Assert.Equal(2098, chunk.GetBlock(15, 64, 0).BlockStateId);
        Assert.Equal(2098, chunk.GetBlock(0, 64, 15).BlockStateId);
        Assert.Equal(2098, chunk.GetBlock(15, 64, 15).BlockStateId);
        
        // Dirt at y=63
        Assert.Equal(2105, chunk.GetBlock(0, 63, 0).BlockStateId);
        Assert.Equal(2105, chunk.GetBlock(15, 63, 15).BlockStateId);
        
        // Dirt below
        Assert.Equal(2105, chunk.GetBlock(0, 0, 0).BlockStateId);
        Assert.Equal(2105, chunk.GetBlock(15, -64, 15).BlockStateId);
        
        // Air above
        Assert.Equal(0, chunk.GetBlock(0, 65, 0).BlockStateId);
        Assert.Equal(0, chunk.GetBlock(15, 100, 15).BlockStateId);
    }

    [Fact]
    public void GenerateHeightmap_ReturnsCorrectValues()
    {
        // Arrange
        var generator = new FlatWorldGenerator();

        // Act
        var heightmap = generator.GenerateHeightmap(0, 0);

        // Assert
        Assert.NotNull(heightmap);
        Assert.Equal(256, heightmap.Length); // 16x16 = 256 columns
        
        // All values should be 65 (ground at y=64, heightmap is y+1)
        for (int i = 0; i < 256; i++)
        {
            Assert.Equal(65, heightmap[i]);
        }
    }

    [Fact]
    public void GenerateHeightmap_IsConsistentAcrossChunks()
    {
        // Arrange
        var generator = new FlatWorldGenerator();

        // Act
        var heightmap1 = generator.GenerateHeightmap(0, 0);
        var heightmap2 = generator.GenerateHeightmap(1, 0);
        var heightmap3 = generator.GenerateHeightmap(0, 1);
        var heightmap4 = generator.GenerateHeightmap(-5, 10);

        // Assert - All should be identical for flat world
        Assert.Equal(heightmap1, heightmap2);
        Assert.Equal(heightmap1, heightmap3);
        Assert.Equal(heightmap1, heightmap4);
    }

    [Fact]
    public void GetConfigSchema_ReturnsNull()
    {
        // Arrange
        var generator = new FlatWorldGenerator();

        // Act
        var schema = generator.GetConfigSchema();

        // Assert
        Assert.Null(schema);
    }

    [Fact]
    public void Configure_AcceptsNullConfig()
    {
        // Arrange
        var generator = new FlatWorldGenerator();

        // Act & Assert - Should not throw
        generator.Configure(null);
        
        // Verify generator still works
        var chunk = new Chunk(0, 0);
        generator.GenerateChunk(chunk, 0, 0);
        Assert.Equal(2098, chunk.GetBlock(0, 64, 0).BlockStateId);
    }

    [Fact]
    public void Configure_AcceptsEmptyConfig()
    {
        // Arrange
        var generator = new FlatWorldGenerator();
        var config = new MineSharp.World.Generation.GeneratorConfig();

        // Act & Assert - Should not throw
        generator.Configure(config);
        
        // Verify generator still works
        var chunk = new Chunk(0, 0);
        generator.GenerateChunk(chunk, 0, 0);
        Assert.Equal(2098, chunk.GetBlock(0, 64, 0).BlockStateId);
    }
}

