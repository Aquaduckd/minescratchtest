using MineSharp.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace MineSharp.Tests.Data;

public class RegistryManagerTests : IDisposable
{
    private readonly string _testDataDir;
    private readonly RegistryManager _registryManager;

    public RegistryManagerTests()
    {
        _testDataDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDataDir);
        _registryManager = new RegistryManager();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDataDir))
        {
            Directory.Delete(_testDataDir, true);
        }
    }

    [Fact]
    public void LoadRegistries_WithValidFiles_LoadsSuccessfully()
    {
        // Arrange
        CreateTestRegistriesFile();
        CreateTestBiomesFile();
        CreateTestDamageTypesFile();

        // Act
        _registryManager.LoadRegistries(_testDataDir);

        // Assert - No exception thrown
        Assert.True(true);
    }

    [Fact]
    public void GetRegistryEntries_BiomeRegistry_ReturnsBiomes()
    {
        // Arrange
        CreateTestBiomesFile();
        _registryManager.LoadRegistries(_testDataDir);

        // Act
        var entries = _registryManager.GetRegistryEntries("minecraft:worldgen/biome");

        // Assert
        Assert.NotNull(entries);
        Assert.True(entries.Count > 0);
        Assert.Contains(entries, e => e.EntryId == "minecraft:plains");
        Assert.Contains(entries, e => e.EntryId == "minecraft:desert");
        Assert.All(entries, e => Assert.Null(e.NbtData)); // No NBT data for biomes
    }

    [Fact]
    public void GetRegistryEntries_DamageTypeRegistry_ReturnsDamageTypes()
    {
        // Arrange
        CreateTestDamageTypesFile();
        _registryManager.LoadRegistries(_testDataDir);

        // Act
        var entries = _registryManager.GetRegistryEntries("minecraft:damage_type");

        // Assert
        Assert.NotNull(entries);
        Assert.True(entries.Count > 0);
        Assert.Contains(entries, e => e.EntryId == "minecraft:in_fire");
        Assert.Contains(entries, e => e.EntryId == "minecraft:generic");
    }

    [Fact]
    public void GetRegistryEntries_FromRegistriesJson_ReturnsEntries()
    {
        // Arrange
        CreateTestRegistriesFile();
        _registryManager.LoadRegistries(_testDataDir);

        // Act
        var entries = _registryManager.GetRegistryEntries("minecraft:dimension_type");

        // Assert
        Assert.NotNull(entries);
        Assert.True(entries.Count > 0);
        // Should have entries from registries.json
    }

    [Fact]
    public void GetRegistryEntries_FromRegistryDataJson_ReturnsEntries()
    {
        // Arrange
        CreateTestRegistryDataFile();
        _registryManager.LoadRegistries(_testDataDir);

        // Act
        var entries = _registryManager.GetRegistryEntries("minecraft:cat_variant");

        // Assert
        Assert.NotNull(entries);
        Assert.True(entries.Count > 0);
        Assert.Contains(entries, e => e.EntryId == "minecraft:persian");
        Assert.Contains(entries, e => e.EntryId == "minecraft:tabby");
    }

    [Fact]
    public void GetRegistryEntries_UnknownRegistry_ReturnsFallback()
    {
        // Arrange
        _registryManager.LoadRegistries(_testDataDir); // No files loaded

        // Act
        var entries = _registryManager.GetRegistryEntries("minecraft:dimension_type");

        // Assert
        Assert.NotNull(entries);
        Assert.Single(entries);
        Assert.Equal("minecraft:overworld", entries[0].EntryId);
    }

    [Fact]
    public void GetRegistryEntries_BiomeRegistry_NoFile_ReturnsFallback()
    {
        // Arrange
        _registryManager.LoadRegistries(_testDataDir); // No biomes.json

        // Act
        var entries = _registryManager.GetRegistryEntries("minecraft:worldgen/biome");

        // Assert
        Assert.NotNull(entries);
        Assert.Single(entries);
        Assert.Equal("minecraft:plains", entries[0].EntryId);
    }

    [Fact]
    public void GetRequiredRegistryIds_ReturnsAllRequiredRegistries()
    {
        // Act
        var registryIds = _registryManager.GetRequiredRegistryIds();

        // Assert
        Assert.NotNull(registryIds);
        Assert.Equal(11, registryIds.Count);
        Assert.Contains("minecraft:dimension_type", registryIds);
        Assert.Contains("minecraft:cat_variant", registryIds);
        Assert.Contains("minecraft:worldgen/biome", registryIds);
        Assert.Contains("minecraft:damage_type", registryIds);
    }

    [Fact]
    public void GetRegistryEntries_CatVariant_ReturnsAllVariants()
    {
        // Arrange
        CreateTestRegistryDataFile();
        _registryManager.LoadRegistries(_testDataDir);

        // Act
        var entries = _registryManager.GetRegistryEntries("minecraft:cat_variant");

        // Assert
        Assert.NotNull(entries);
        Assert.True(entries.Count >= 11); // Should have at least 11 cat variants
        var entryIds = entries.Select(e => e.EntryId).ToList();
        Assert.Contains("minecraft:persian", entryIds);
        Assert.Contains("minecraft:tabby", entryIds);
        Assert.Contains("minecraft:black", entryIds);
    }

    // Helper methods to create test JSON files
    private void CreateTestRegistriesFile()
    {
        var registries = new Dictionary<string, object>
        {
            ["minecraft:dimension_type"] = new
            {
                entries = new Dictionary<string, object>
                {
                    ["minecraft:overworld"] = new { protocol_id = 0 },
                    ["minecraft:the_nether"] = new { protocol_id = 1 },
                    ["minecraft:the_end"] = new { protocol_id = 2 }
                }
            }
        };
        var json = System.Text.Json.JsonSerializer.Serialize(registries);
        File.WriteAllText(Path.Combine(_testDataDir, "registries.json"), json);
    }

    private void CreateTestBiomesFile()
    {
        var biomes = new[] { "minecraft:plains", "minecraft:desert", "minecraft:ocean" };
        var json = System.Text.Json.JsonSerializer.Serialize(biomes);
        File.WriteAllText(Path.Combine(_testDataDir, "biomes.json"), json);
    }

    private void CreateTestDamageTypesFile()
    {
        var damageTypes = new[] { "minecraft:in_fire", "minecraft:generic", "minecraft:explosion" };
        var json = System.Text.Json.JsonSerializer.Serialize(damageTypes);
        File.WriteAllText(Path.Combine(_testDataDir, "damage_types.json"), json);
    }

    private void CreateTestRegistryDataFile()
    {
        var registryData = new Dictionary<string, Dictionary<string, object>>
        {
            ["minecraft:cat_variant"] = new Dictionary<string, object>
            {
                ["minecraft:persian"] = new { asset_id = "minecraft:entity/cat/persian" },
                ["minecraft:tabby"] = new { asset_id = "minecraft:entity/cat/tabby" },
                ["minecraft:black"] = new { asset_id = "minecraft:entity/cat/black" },
                ["minecraft:red"] = new { asset_id = "minecraft:entity/cat/red" },
                ["minecraft:siamese"] = new { asset_id = "minecraft:entity/cat/siamese" },
                ["minecraft:british_shorthair"] = new { asset_id = "minecraft:entity/cat/british_shorthair" },
                ["minecraft:calico"] = new { asset_id = "minecraft:entity/cat/calico" },
                ["minecraft:ragdoll"] = new { asset_id = "minecraft:entity/cat/ragdoll" },
                ["minecraft:white"] = new { asset_id = "minecraft:entity/cat/white" },
                ["minecraft:jellie"] = new { asset_id = "minecraft:entity/cat/jellie" },
                ["minecraft:all_black"] = new { asset_id = "minecraft:entity/cat/all_black" }
            }
        };
        var json = System.Text.Json.JsonSerializer.Serialize(registryData);
        File.WriteAllText(Path.Combine(_testDataDir, "registry_data.json"), json);
    }
}

