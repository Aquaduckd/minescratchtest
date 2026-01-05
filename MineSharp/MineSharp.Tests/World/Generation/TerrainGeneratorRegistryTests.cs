using System;
using System.Linq;
using MineSharp.World;
using MineSharp.World.Generation;
using MineSharp.World.Generation.Generators;
using Xunit;

namespace MineSharp.Tests.World.Generation;

public class TerrainGeneratorRegistryTests
{
    [Fact]
    public void Constructor_RegistersDefaultGenerators()
    {
        // Arrange & Act
        var registry = new TerrainGeneratorRegistry();

        // Assert
        Assert.True(registry.IsRegistered("flat"));
        Assert.True(registry.IsRegistered("void"));
    }

    [Fact]
    public void GetGenerator_Flat_ReturnsFlatWorldGenerator()
    {
        // Arrange
        var registry = new TerrainGeneratorRegistry();

        // Act
        var generator = registry.GetGenerator("flat");

        // Assert
        Assert.NotNull(generator);
        Assert.IsType<FlatWorldGenerator>(generator);
        Assert.Equal("flat", generator.GeneratorId);
        Assert.Equal("Flat World", generator.DisplayName);
    }

    [Fact]
    public void GetGenerator_Void_ReturnsVoidWorldGenerator()
    {
        // Arrange
        var registry = new TerrainGeneratorRegistry();

        // Act
        var generator = registry.GetGenerator("void");

        // Assert
        Assert.NotNull(generator);
        Assert.IsType<VoidWorldGenerator>(generator);
        Assert.Equal("void", generator.GeneratorId);
    }

    [Fact]
    public void GetGenerator_UnknownId_ThrowsArgumentException()
    {
        // Arrange
        var registry = new TerrainGeneratorRegistry();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => registry.GetGenerator("unknown"));
        Assert.Contains("Unknown generator ID", exception.Message);
        Assert.Contains("unknown", exception.Message);
    }

    [Fact]
    public void GetGenerator_NullId_ThrowsArgumentException()
    {
        // Arrange
        var registry = new TerrainGeneratorRegistry();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => registry.GetGenerator(null!));
    }

    [Fact]
    public void GetGenerator_EmptyId_ThrowsArgumentException()
    {
        // Arrange
        var registry = new TerrainGeneratorRegistry();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => registry.GetGenerator(""));
    }

    [Fact]
    public void GetGenerator_CachesInstances()
    {
        // Arrange
        var registry = new TerrainGeneratorRegistry();

        // Act
        var generator1 = registry.GetGenerator("flat");
        var generator2 = registry.GetGenerator("flat");

        // Assert - Should return the same instance (cached)
        Assert.Same(generator1, generator2);
    }

    [Fact]
    public void Register_NewGenerator_CanBeRetrieved()
    {
        // Arrange
        var registry = new TerrainGeneratorRegistry();
        
        // Create a test generator
        var testGenerator = new TestGenerator();

        // Act
        registry.Register("test", () => testGenerator);
        var retrieved = registry.GetGenerator("test");

        // Assert
        Assert.NotNull(retrieved);
        Assert.Same(testGenerator, retrieved);
        Assert.True(registry.IsRegistered("test"));
    }

    [Fact]
    public void Register_OverwritesExisting()
    {
        // Arrange
        var registry = new TerrainGeneratorRegistry();
        var generator1 = new TestGenerator();
        var generator2 = new TestGenerator();

        // Act
        registry.Register("test", () => generator1);
        var first = registry.GetGenerator("test");
        
        registry.Register("test", () => generator2);
        var second = registry.GetGenerator("test");

        // Assert
        Assert.Same(generator1, first);
        Assert.Same(generator2, second);
        Assert.NotSame(first, second);
    }

    [Fact]
    public void Register_NullFactory_ThrowsArgumentNullException()
    {
        // Arrange
        var registry = new TerrainGeneratorRegistry();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => registry.Register("test", null!));
    }

    [Fact]
    public void Register_NullId_ThrowsArgumentException()
    {
        // Arrange
        var registry = new TerrainGeneratorRegistry();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => registry.Register(null!, () => new TestGenerator()));
    }

    [Fact]
    public void GetRegisteredGeneratorIds_ReturnsAllRegisteredIds()
    {
        // Arrange
        var registry = new TerrainGeneratorRegistry();

        // Act
        var ids = registry.GetRegisteredGeneratorIds().ToList();

        // Assert
        Assert.Contains("flat", ids);
        Assert.Contains("void", ids);
        Assert.True(ids.Count >= 2);
    }

    [Fact]
    public void IsRegistered_ExistingGenerator_ReturnsTrue()
    {
        // Arrange
        var registry = new TerrainGeneratorRegistry();

        // Act & Assert
        Assert.True(registry.IsRegistered("flat"));
        Assert.True(registry.IsRegistered("void"));
    }

    [Fact]
    public void IsRegistered_UnknownGenerator_ReturnsFalse()
    {
        // Arrange
        var registry = new TerrainGeneratorRegistry();

        // Act & Assert
        Assert.False(registry.IsRegistered("unknown"));
        Assert.False(registry.IsRegistered(""));
    }

    // Test helper generator
    private class TestGenerator : ITerrainGenerator
    {
        public string GeneratorId => "test";
        public string DisplayName => "Test Generator";

        public void GenerateChunk(Chunk chunk, int chunkX, int chunkZ)
        {
            // Test implementation - do nothing
        }

        public int[] GenerateHeightmap(int chunkX, int chunkZ)
        {
            return new int[256];
        }

        public GeneratorConfigSchema? GetConfigSchema()
        {
            return null;
        }

        public void Configure(GeneratorConfig? config)
        {
            // No-op
        }
    }
}

