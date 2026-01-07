using System;
using MineSharp.Game;
using Xunit;

namespace MineSharp.Tests.Game;

public class PlayerEntityIdTests
{
    [Fact]
    public void Player_HasEntityId()
    {
        // Arrange
        var uuid = Guid.NewGuid();
        int entityId = 1;

        // Act
        var player = new Player(uuid, entityId);

        // Assert
        Assert.Equal(entityId, player.EntityId);
    }

    [Fact]
    public void Player_EntityIdIsImmutable()
    {
        // Arrange
        var uuid = Guid.NewGuid();
        int entityId = 5;
        var player = new Player(uuid, entityId);

        // Act & Assert
        // EntityId should be a getter-only property, so we can't change it
        // This test verifies the property exists and returns the correct value
        Assert.Equal(5, player.EntityId);
    }

    [Fact]
    public void Player_EntityIdCanBeDifferentFromOne()
    {
        // Arrange
        var uuid1 = Guid.NewGuid();
        var uuid2 = Guid.NewGuid();
        int entityId1 = 1;
        int entityId2 = 2;

        // Act
        var player1 = new Player(uuid1, entityId1);
        var player2 = new Player(uuid2, entityId2);

        // Assert
        Assert.Equal(1, player1.EntityId);
        Assert.Equal(2, player2.EntityId);
        Assert.NotEqual(player1.EntityId, player2.EntityId);
    }

    [Fact]
    public void Player_EntityIdIsSetInConstructor()
    {
        // Arrange
        var uuid = Guid.NewGuid();
        int[] entityIds = { 1, 5, 10, 100, 1000 };

        // Act & Assert
        foreach (int entityId in entityIds)
        {
            var player = new Player(uuid, entityId);
            Assert.Equal(entityId, player.EntityId);
        }
    }
}

