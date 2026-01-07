using MineSharp.Game;
using System.Collections.Concurrent;

namespace MineSharp.World;

/// <summary>
/// Manages entity lifecycle and updates.
/// </summary>
public class EntityManager
{
    private readonly ConcurrentDictionary<int, Entity> _entities;
    private int _nextPlayerEntityId;  // Entity IDs for players (start at 1)
    private int _nextNonPlayerEntityId;  // Entity IDs for non-player entities (start at 1000)
    private readonly object _playerIdLock = new object();
    private readonly object _nonPlayerIdLock = new object();

    public EntityManager()
    {
        _entities = new ConcurrentDictionary<int, Entity>();
        _nextPlayerEntityId = 1;  // Players start at 1
        _nextNonPlayerEntityId = 1000;  // Non-player entities start at 1000
    }

    /// <summary>
    /// Gets the next available entity ID for a player.
    /// Players get IDs starting from 1.
    /// </summary>
    public int GetNextPlayerEntityId()
    {
        lock (_playerIdLock)
        {
            return _nextPlayerEntityId++;
        }
    }

    /// <summary>
    /// Gets the next available entity ID for a non-player entity.
    /// Non-player entities get IDs starting from 1000.
    /// </summary>
    public int GetNextNonPlayerEntityId()
    {
        lock (_nonPlayerIdLock)
        {
            return _nextNonPlayerEntityId++;
        }
    }

    /// <summary>
    /// Gets the next available entity ID.
    /// For now, this returns a non-player entity ID.
    /// Use GetNextPlayerEntityId() for players.
    /// </summary>
    public int GetNextEntityId()
    {
        return GetNextNonPlayerEntityId();
    }

    /// <summary>
    /// Spawns an entity and registers it with the entity manager.
    /// The entity must already have an EntityId assigned.
    /// </summary>
    /// <param name="entity">The entity to spawn</param>
    /// <returns>The entity's ID</returns>
    /// <exception cref="ArgumentException">Thrown if entity ID is already in use</exception>
    public int SpawnEntity(Entity entity)
    {
        if (entity == null)
        {
            throw new ArgumentNullException(nameof(entity));
        }

        if (!_entities.TryAdd(entity.EntityId, entity))
        {
            throw new ArgumentException($"Entity ID {entity.EntityId} is already in use", nameof(entity));
        }

        return entity.EntityId;
    }

    /// <summary>
    /// Removes an entity from the entity manager.
    /// </summary>
    /// <param name="entityId">The ID of the entity to remove</param>
    /// <returns>True if the entity was removed, false if it didn't exist</returns>
    public bool RemoveEntity(int entityId)
    {
        return _entities.TryRemove(entityId, out _);
    }

    /// <summary>
    /// Gets an entity by its ID.
    /// </summary>
    /// <param name="entityId">The ID of the entity to retrieve</param>
    /// <returns>The entity if found, null otherwise</returns>
    public Entity? GetEntity(int entityId)
    {
        _entities.TryGetValue(entityId, out var entity);
        return entity;
    }

    /// <summary>
    /// Updates all entities.
    /// </summary>
    /// <param name="deltaTime">Time elapsed since last update</param>
    public void UpdateEntities(TimeSpan deltaTime)
    {
        foreach (var entity in _entities.Values)
        {
            try
            {
                entity.Update(deltaTime);
            }
            catch (NotImplementedException)
            {
                // Entity.Update() is not implemented yet, skip for now
            }
        }
    }

    /// <summary>
    /// Gets all registered entities.
    /// </summary>
    public IEnumerable<Entity> GetAllEntities()
    {
        return _entities.Values;
    }

    /// <summary>
    /// Gets the count of registered entities.
    /// </summary>
    public int EntityCount => _entities.Count;
}

