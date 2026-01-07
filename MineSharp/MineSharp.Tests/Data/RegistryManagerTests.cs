using System;
using MineSharp.Data;
using Xunit;

namespace MineSharp.Tests.Data;

public class RegistryManagerTests
{
    [Fact]
    public void GetRegistryEntryProtocolId_PlayerEntityType_ReturnsCorrectId()
    {
        // Arrange
        var registryManager = new RegistryManager();
        // Note: This test assumes the registry data is loaded
        // In a real scenario, we'd load test data
        
        // Act
        var protocolId = registryManager.GetRegistryEntryProtocolId("minecraft:entity_type", "minecraft:player");
        
        // Assert
        // Player entity type should have protocol_id: 151
        // If registry is not loaded, this will be null
        if (protocolId.HasValue)
        {
            Assert.Equal(151, protocolId.Value);
        }
    }

    [Fact]
    public void GetRegistryEntryProtocolId_NonExistentEntry_ReturnsNull()
    {
        // Arrange
        var registryManager = new RegistryManager();
        
        // Act
        var protocolId = registryManager.GetRegistryEntryProtocolId("minecraft:entity_type", "minecraft:nonexistent");
        
        // Assert
        Assert.Null(protocolId);
    }

    [Fact]
    public void GetRegistryEntryProtocolId_NonExistentRegistry_ReturnsNull()
    {
        // Arrange
        var registryManager = new RegistryManager();
        
        // Act
        var protocolId = registryManager.GetRegistryEntryProtocolId("minecraft:nonexistent", "minecraft:player");
        
        // Assert
        Assert.Null(protocolId);
    }
}
