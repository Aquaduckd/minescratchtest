using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MineSharp.Game;
using MineSharp.World;
using Xunit;

namespace MineSharp.Tests.World;

public class EntityManagerTests
{
    [Fact]
    public void GetNextPlayerEntityId_StartsAtOne()
    {
        // Arrange
        var manager = new EntityManager();

        // Act
        int id1 = manager.GetNextPlayerEntityId();
        int id2 = manager.GetNextPlayerEntityId();
        int id3 = manager.GetNextPlayerEntityId();

        // Assert
        Assert.Equal(1, id1);
        Assert.Equal(2, id2);
        Assert.Equal(3, id3);
    }

    [Fact]
    public void GetNextNonPlayerEntityId_StartsAtThousand()
    {
        // Arrange
        var manager = new EntityManager();

        // Act
        int id1 = manager.GetNextNonPlayerEntityId();
        int id2 = manager.GetNextNonPlayerEntityId();
        int id3 = manager.GetNextNonPlayerEntityId();

        // Assert
        Assert.Equal(1000, id1);
        Assert.Equal(1001, id2);
        Assert.Equal(1002, id3);
    }

    [Fact]
    public void GetNextPlayerEntityId_IsThreadSafe()
    {
        // Arrange
        var manager = new EntityManager();
        var ids = new System.Collections.Concurrent.ConcurrentBag<int>();
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                ids.Add(manager.GetNextPlayerEntityId());
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert
        Assert.Equal(100, ids.Count);
        // All IDs should be unique
        Assert.Equal(100, ids.Distinct().Count());
        // IDs should be in range 1-100
        Assert.All(ids, id => Assert.True(id >= 1 && id <= 100));
    }

    [Fact]
    public void SpawnEntity_RegistersEntity()
    {
        // Arrange
        var manager = new EntityManager();
        var entityId = manager.GetNextNonPlayerEntityId();
        var entity = new TestEntity(entityId);

        // Act
        int registeredId = manager.SpawnEntity(entity);

        // Assert
        Assert.Equal(entityId, registeredId);
        Assert.Equal(1, manager.EntityCount);
        Assert.Same(entity, manager.GetEntity(entityId));
    }

    [Fact]
    public void SpawnEntity_ThrowsWhenEntityIdAlreadyExists()
    {
        // Arrange
        var manager = new EntityManager();
        var entityId = manager.GetNextNonPlayerEntityId();
        var entity1 = new TestEntity(entityId);
        var entity2 = new TestEntity(entityId);

        // Act
        manager.SpawnEntity(entity1);

        // Assert
        Assert.Throws<ArgumentException>(() => manager.SpawnEntity(entity2));
    }

    [Fact]
    public void SpawnEntity_ThrowsWhenEntityIsNull()
    {
        // Arrange
        var manager = new EntityManager();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => manager.SpawnEntity(null!));
    }

    [Fact]
    public void RemoveEntity_RemovesEntity()
    {
        // Arrange
        var manager = new EntityManager();
        var entityId = manager.GetNextNonPlayerEntityId();
        var entity = new TestEntity(entityId);
        manager.SpawnEntity(entity);

        // Act
        bool removed = manager.RemoveEntity(entityId);

        // Assert
        Assert.True(removed);
        Assert.Equal(0, manager.EntityCount);
        Assert.Null(manager.GetEntity(entityId));
    }

    [Fact]
    public void RemoveEntity_ReturnsFalseWhenEntityDoesNotExist()
    {
        // Arrange
        var manager = new EntityManager();

        // Act
        bool removed = manager.RemoveEntity(9999);

        // Assert
        Assert.False(removed);
    }

    [Fact]
    public void GetEntity_ReturnsEntityWhenExists()
    {
        // Arrange
        var manager = new EntityManager();
        var entityId = manager.GetNextNonPlayerEntityId();
        var entity = new TestEntity(entityId);
        manager.SpawnEntity(entity);

        // Act
        var retrieved = manager.GetEntity(entityId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Same(entity, retrieved);
    }

    [Fact]
    public void GetEntity_ReturnsNullWhenEntityDoesNotExist()
    {
        // Arrange
        var manager = new EntityManager();

        // Act
        var retrieved = manager.GetEntity(9999);

        // Assert
        Assert.Null(retrieved);
    }

    [Fact]
    public void GetAllEntities_ReturnsAllEntities()
    {
        // Arrange
        var manager = new EntityManager();
        var entity1 = new TestEntity(manager.GetNextNonPlayerEntityId());
        var entity2 = new TestEntity(manager.GetNextNonPlayerEntityId());
        var entity3 = new TestEntity(manager.GetNextNonPlayerEntityId());
        manager.SpawnEntity(entity1);
        manager.SpawnEntity(entity2);
        manager.SpawnEntity(entity3);

        // Act
        var allEntities = manager.GetAllEntities().ToList();

        // Assert
        Assert.Equal(3, allEntities.Count);
        Assert.Contains(entity1, allEntities);
        Assert.Contains(entity2, allEntities);
        Assert.Contains(entity3, allEntities);
    }

    [Fact]
    public void EntityCount_ReturnsCorrectCount()
    {
        // Arrange
        var manager = new EntityManager();

        // Act & Assert
        Assert.Equal(0, manager.EntityCount);

        var entity1 = new TestEntity(manager.GetNextNonPlayerEntityId());
        manager.SpawnEntity(entity1);
        Assert.Equal(1, manager.EntityCount);

        var entity2 = new TestEntity(manager.GetNextNonPlayerEntityId());
        manager.SpawnEntity(entity2);
        Assert.Equal(2, manager.EntityCount);

        manager.RemoveEntity(entity1.EntityId);
        Assert.Equal(1, manager.EntityCount);
    }

    // Test helper class
    private class TestEntity : Entity
    {
        public TestEntity(int entityId) : base(entityId)
        {
        }
    }
}

