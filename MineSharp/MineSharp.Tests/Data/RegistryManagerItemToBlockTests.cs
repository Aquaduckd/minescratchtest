using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MineSharp.Data;
using Xunit;

namespace MineSharp.Tests.Data;

public class RegistryManagerItemToBlockTests
{
    private string CreateTempDataDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }

    private void CreateTestRegistriesFile(string dataPath, Dictionary<string, JsonElement> registries)
    {
        var registriesFile = Path.Combine(dataPath, "registries.json");
        var json = JsonSerializer.Serialize(registries);
        File.WriteAllText(registriesFile, json);
    }

    private void CreateTestBlocksFile(string dataPath, Dictionary<string, JsonElement> blocks)
    {
        var blocksFile = Path.Combine(dataPath, "blocks.json");
        var json = JsonSerializer.Serialize(blocks);
        File.WriteAllText(blocksFile, json);
    }

    [Fact]
    public void GetItemNameByProtocolId_WithValidItemId_ReturnsItemName()
    {
        // Arrange
        var tempDir = CreateTempDataDirectory();
        try
        {
            var registries = new Dictionary<string, JsonElement>
            {
                ["minecraft:item"] = JsonSerializer.Deserialize<JsonElement>(@"{
                    ""entries"": {
                        ""minecraft:stone"": { ""protocol_id"": 10 },
                        ""minecraft:dirt"": { ""protocol_id"": 20 }
                    }
                }")
            };
            CreateTestRegistriesFile(tempDir, registries);

            var registryManager = new RegistryManager();
            registryManager.LoadRegistries(tempDir);

            // Act
            var itemName = registryManager.GetItemNameByProtocolId(10);

            // Assert
            Assert.Equal("minecraft:stone", itemName);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void GetItemNameByProtocolId_WithInvalidItemId_ReturnsNull()
    {
        // Arrange
        var tempDir = CreateTempDataDirectory();
        try
        {
            var registries = new Dictionary<string, JsonElement>
            {
                ["minecraft:item"] = JsonSerializer.Deserialize<JsonElement>(@"{
                    ""entries"": {
                        ""minecraft:stone"": { ""protocol_id"": 10 }
                    }
                }")
            };
            CreateTestRegistriesFile(tempDir, registries);

            var registryManager = new RegistryManager();
            registryManager.LoadRegistries(tempDir);

            // Act
            var itemName = registryManager.GetItemNameByProtocolId(999);

            // Assert
            Assert.Null(itemName);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void GetDefaultBlockStateIdByBlockName_WithValidBlock_ReturnsDefaultStateId()
    {
        // Arrange
        var tempDir = CreateTempDataDirectory();
        try
        {
            var blocks = new Dictionary<string, JsonElement>
            {
                ["minecraft:stone"] = JsonSerializer.Deserialize<JsonElement>(@"{
                    ""states"": [
                        { ""id"": 100, ""properties"": {} },
                        { ""id"": 101, ""default"": true, ""properties"": {} },
                        { ""id"": 102, ""properties"": {} }
                    ]
                }")
            };
            CreateTestBlocksFile(tempDir, blocks);

            var registryManager = new RegistryManager();
            registryManager.LoadRegistries(tempDir);

            // Act
            var blockStateId = registryManager.GetDefaultBlockStateIdByBlockName("minecraft:stone");

            // Assert
            Assert.Equal(101, blockStateId);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void GetDefaultBlockStateIdByBlockName_WithNoDefaultState_ReturnsFirstStateId()
    {
        // Arrange
        var tempDir = CreateTempDataDirectory();
        try
        {
            var blocks = new Dictionary<string, JsonElement>
            {
                ["minecraft:dirt"] = JsonSerializer.Deserialize<JsonElement>(@"{
                    ""states"": [
                        { ""id"": 200, ""properties"": {} },
                        { ""id"": 201, ""properties"": {} }
                    ]
                }")
            };
            CreateTestBlocksFile(tempDir, blocks);

            var registryManager = new RegistryManager();
            registryManager.LoadRegistries(tempDir);

            // Act
            var blockStateId = registryManager.GetDefaultBlockStateIdByBlockName("minecraft:dirt");

            // Assert
            Assert.Equal(200, blockStateId);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void GetDefaultBlockStateIdByBlockName_WithInvalidBlock_ReturnsNull()
    {
        // Arrange
        var tempDir = CreateTempDataDirectory();
        try
        {
            var blocks = new Dictionary<string, JsonElement>
            {
                ["minecraft:stone"] = JsonSerializer.Deserialize<JsonElement>(@"{
                    ""states"": [
                        { ""id"": 100, ""default"": true, ""properties"": {} }
                    ]
                }")
            };
            CreateTestBlocksFile(tempDir, blocks);

            var registryManager = new RegistryManager();
            registryManager.LoadRegistries(tempDir);

            // Act
            var blockStateId = registryManager.GetDefaultBlockStateIdByBlockName("minecraft:nonexistent");

            // Assert
            Assert.Null(blockStateId);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ResolveBlockStateIdForItemProtocolId_WithValidItem_ReturnsBlockStateId()
    {
        // Arrange
        var tempDir = CreateTempDataDirectory();
        try
        {
            var registries = new Dictionary<string, JsonElement>
            {
                ["minecraft:item"] = JsonSerializer.Deserialize<JsonElement>(@"{
                    ""entries"": {
                        ""minecraft:stone"": { ""protocol_id"": 10 }
                    }
                }")
            };
            CreateTestRegistriesFile(tempDir, registries);

            var blocks = new Dictionary<string, JsonElement>
            {
                ["minecraft:stone"] = JsonSerializer.Deserialize<JsonElement>(@"{
                    ""states"": [
                        { ""id"": 100, ""default"": true, ""properties"": {} }
                    ]
                }")
            };
            CreateTestBlocksFile(tempDir, blocks);

            var registryManager = new RegistryManager();
            registryManager.LoadRegistries(tempDir);

            // Act
            var blockStateId = registryManager.ResolveBlockStateIdForItemProtocolId(10);

            // Assert
            Assert.Equal(100, blockStateId);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ResolveBlockStateIdForItemProtocolId_WithNonBlockItem_ReturnsNull()
    {
        // Arrange
        var tempDir = CreateTempDataDirectory();
        try
        {
            var registries = new Dictionary<string, JsonElement>
            {
                ["minecraft:item"] = JsonSerializer.Deserialize<JsonElement>(@"{
                    ""entries"": {
                        ""minecraft:apple"": { ""protocol_id"": 50 }
                    }
                }")
            };
            CreateTestRegistriesFile(tempDir, registries);

            var blocks = new Dictionary<string, JsonElement>
            {
                ["minecraft:stone"] = JsonSerializer.Deserialize<JsonElement>(@"{
                    ""states"": [
                        { ""id"": 100, ""default"": true, ""properties"": {} }
                    ]
                }")
            };
            CreateTestBlocksFile(tempDir, blocks);

            var registryManager = new RegistryManager();
            registryManager.LoadRegistries(tempDir);

            // Act
            var blockStateId = registryManager.ResolveBlockStateIdForItemProtocolId(50);

            // Assert
            Assert.Null(blockStateId);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ResolveBlockStateIdForItemProtocolId_WithInvalidItemId_ReturnsNull()
    {
        // Arrange
        var tempDir = CreateTempDataDirectory();
        try
        {
            var registries = new Dictionary<string, JsonElement>
            {
                ["minecraft:item"] = JsonSerializer.Deserialize<JsonElement>(@"{
                    ""entries"": {
                        ""minecraft:stone"": { ""protocol_id"": 10 }
                    }
                }")
            };
            CreateTestRegistriesFile(tempDir, registries);

            var registryManager = new RegistryManager();
            registryManager.LoadRegistries(tempDir);

            // Act
            var blockStateId = registryManager.ResolveBlockStateIdForItemProtocolId(999);

            // Assert
            Assert.Null(blockStateId);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}




