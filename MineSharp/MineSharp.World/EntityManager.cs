using MineSharp.Game;
using System.Collections.Concurrent;

namespace MineSharp.World;

/// <summary>
/// Manages entity lifecycle and updates.
/// </summary>
public class EntityManager
{
    private readonly ConcurrentDictionary<int, Entity> _entities;
    private int _nextEntityId;

    public EntityManager()
    {
        _entities = new ConcurrentDictionary<int, Entity>();
        _nextEntityId = 1000;  // Start entity IDs at 1000 (player is usually 1)
    }

    public int SpawnEntity(Entity entity)
    {
        // TODO: Implement entity spawning
        throw new NotImplementedException();
    }

    public void RemoveEntity(int entityId)
    {
        // TODO: Implement entity removal
        throw new NotImplementedException();
    }

    public Entity? GetEntity(int entityId)
    {
        // TODO: Implement entity retrieval
        throw new NotImplementedException();
    }

    public void UpdateEntities(TimeSpan deltaTime)
    {
        // TODO: Implement entity updates
        throw new NotImplementedException();
    }

    public int GetNextEntityId()
    {
        // TODO: Implement next entity ID generation
        throw new NotImplementedException();
    }
}

