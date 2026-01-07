using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MineSharp.Game;
using Xunit;

namespace MineSharp.Tests.Game;

public class PlayerEntityVisibilityTests
{
    [Fact]
    public void AddVisibleEntity_AddsEntity()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), entityId: 1);
        int entityId = 2;

        // Act
        bool added = player.AddVisibleEntity(entityId);

        // Assert
        Assert.True(added);
        Assert.True(player.IsEntityVisible(entityId));
        Assert.Contains(entityId, player.VisibleEntityIds);
    }

    [Fact]
    public void AddVisibleEntity_ReturnsFalseWhenAlreadyVisible()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), entityId: 1);
        int entityId = 2;
        player.AddVisibleEntity(entityId);

        // Act
        bool added = player.AddVisibleEntity(entityId);

        // Assert
        Assert.False(added);
        Assert.True(player.IsEntityVisible(entityId));
    }

    [Fact]
    public void RemoveVisibleEntity_RemovesEntity()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), entityId: 1);
        int entityId = 2;
        player.AddVisibleEntity(entityId);

        // Act
        bool removed = player.RemoveVisibleEntity(entityId);

        // Assert
        Assert.True(removed);
        Assert.False(player.IsEntityVisible(entityId));
        Assert.DoesNotContain(entityId, player.VisibleEntityIds);
    }

    [Fact]
    public void RemoveVisibleEntity_ReturnsFalseWhenNotVisible()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), entityId: 1);
        int entityId = 2;

        // Act
        bool removed = player.RemoveVisibleEntity(entityId);

        // Assert
        Assert.False(removed);
        Assert.False(player.IsEntityVisible(entityId));
    }

    [Fact]
    public void IsEntityVisible_ReturnsTrueWhenVisible()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), entityId: 1);
        int entityId = 2;
        player.AddVisibleEntity(entityId);

        // Act
        bool isVisible = player.IsEntityVisible(entityId);

        // Assert
        Assert.True(isVisible);
    }

    [Fact]
    public void IsEntityVisible_ReturnsFalseWhenNotVisible()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), entityId: 1);
        int entityId = 2;

        // Act
        bool isVisible = player.IsEntityVisible(entityId);

        // Assert
        Assert.False(isVisible);
    }

    [Fact]
    public void GetVisibleEntities_ReturnsCopyOfVisibleEntities()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), entityId: 1);
        player.AddVisibleEntity(2);
        player.AddVisibleEntity(3);
        player.AddVisibleEntity(4);

        // Act
        var visibleEntities = player.GetVisibleEntities();

        // Assert
        Assert.Equal(3, visibleEntities.Count);
        Assert.Contains(2, visibleEntities);
        Assert.Contains(3, visibleEntities);
        Assert.Contains(4, visibleEntities);
    }

    [Fact]
    public void GetVisibleEntities_ReturnsIndependentCopy()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), entityId: 1);
        player.AddVisibleEntity(2);

        // Act
        var visibleEntities = player.GetVisibleEntities();
        visibleEntities.Add(5); // Modify the copy

        // Assert
        // Original should not be affected
        Assert.Single(player.VisibleEntityIds);
        Assert.Contains(2, player.VisibleEntityIds);
        Assert.DoesNotContain(5, player.VisibleEntityIds);
    }

    [Fact]
    public void ClearVisibleEntities_RemovesAllEntities()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), entityId: 1);
        player.AddVisibleEntity(2);
        player.AddVisibleEntity(3);
        player.AddVisibleEntity(4);

        // Act
        player.ClearVisibleEntities();

        // Assert
        Assert.Empty(player.VisibleEntityIds);
        Assert.False(player.IsEntityVisible(2));
        Assert.False(player.IsEntityVisible(3));
        Assert.False(player.IsEntityVisible(4));
    }

    [Fact]
    public void EntityVisibility_IsThreadSafe()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), entityId: 1);
        var addedIds = new System.Collections.Concurrent.ConcurrentBag<int>();
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < 100; i++)
        {
            int entityId = i + 10; // Start from 10 to avoid conflict with player's entity ID
            tasks.Add(Task.Run(() =>
            {
                player.AddVisibleEntity(entityId);
                addedIds.Add(entityId);
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert
        Assert.Equal(100, addedIds.Count);
        // All entities should be visible
        foreach (int entityId in addedIds)
        {
            Assert.True(player.IsEntityVisible(entityId));
        }
        Assert.Equal(100, player.VisibleEntityIds.Count);
    }

    [Fact]
    public void EntityVisibility_MultipleOperations_IsThreadSafe()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), entityId: 1);
        var tasks = new List<Task>();

        // Act - Mix of add and remove operations
        for (int i = 0; i < 50; i++)
        {
            int entityId = i + 10;
            tasks.Add(Task.Run(() =>
            {
                player.AddVisibleEntity(entityId);
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Remove some entities
        for (int i = 0; i < 25; i++)
        {
            int entityId = i + 10;
            tasks.Add(Task.Run(() =>
            {
                player.RemoveVisibleEntity(entityId);
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert
        // Should have 25 entities visible (entities 35-59)
        Assert.Equal(25, player.VisibleEntityIds.Count);
        for (int i = 10; i < 35; i++)
        {
            Assert.False(player.IsEntityVisible(i));
        }
        for (int i = 35; i < 60; i++)
        {
            Assert.True(player.IsEntityVisible(i));
        }
    }

    [Fact]
    public void VisibleEntityIds_InitiallyEmpty()
    {
        // Arrange & Act
        var player = new Player(Guid.NewGuid(), entityId: 1);

        // Assert
        Assert.Empty(player.VisibleEntityIds);
    }
}

