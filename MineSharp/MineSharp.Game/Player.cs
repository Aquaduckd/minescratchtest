using MineSharp.Core.DataTypes;

namespace MineSharp.Game;

/// <summary>
/// Represents a player in the world.
/// Encapsulates all player-specific state including position, rotation, and inventory.
/// </summary>
public class Player
{
    public Guid Uuid { get; }
    public int EntityId { get; }
    public Vector3 Position { get; private set; }
    public int ChunkX { get; private set; }
    public int ChunkZ { get; private set; }
    public float Yaw { get; private set; }
    public float Pitch { get; private set; }
    public float HeadYaw { get; private set; }
    public int ViewDistance { get; }
    public Inventory Inventory { get; }
    public byte GameMode { get; set; } // 0=Survival, 1=Creative, 2=Adventure, 3=Spectator
    public bool IsSneaking { get; private set; }
    public HashSet<(int X, int Z)> LoadedChunks { get; }
    private readonly HashSet<(int X, int Z)> _chunksLoadingInProgress = new HashSet<(int X, int Z)>();
    private readonly object _chunkLock = new object();

    // Entity visibility tracking
    public HashSet<int> VisibleEntityIds { get; }
    private readonly object _entityVisibilityLock = new object();

    public Player(Guid uuid, int entityId, int viewDistance = 10)
    {
        Uuid = uuid;
        EntityId = entityId;
        ViewDistance = viewDistance;
        Position = new Vector3(0, 64, 0);
        ChunkX = 0;
        ChunkZ = 0;
        Yaw = 0;
        Pitch = 0;
        HeadYaw = 0;
        Inventory = new Inventory();
        LoadedChunks = new HashSet<(int, int)>();
        VisibleEntityIds = new HashSet<int>();
    }

    public (int oldChunkX, int oldChunkZ, int newChunkX, int newChunkZ)? UpdatePosition(Vector3 newPosition)
    {
        // Store old chunk coordinates
        var oldChunkX = ChunkX;
        var oldChunkZ = ChunkZ;
        
        // Update position
        Position = newPosition;
        
        // Calculate new chunk coordinates (floor division by 16)
        var newChunkX = (int)Math.Floor(newPosition.X / 16.0);
        var newChunkZ = (int)Math.Floor(newPosition.Z / 16.0);
        
        // Update chunk coordinates
        ChunkX = newChunkX;
        ChunkZ = newChunkZ;
        
        // Check if chunk boundary was crossed
        if (oldChunkX != newChunkX || oldChunkZ != newChunkZ)
        {
            return (oldChunkX, oldChunkZ, newChunkX, newChunkZ);
        }
        
        return null;
    }

    public void UpdateRotation(float yaw, float pitch)
    {
        Yaw = yaw;
        Pitch = pitch;
        // Head yaw typically follows body yaw, but can be different when looking around
        // For now, we'll update head yaw to match body yaw
        // In the future, we can add separate head yaw updates from client
        HeadYaw = yaw;
    }
    
    public void UpdateHeadYaw(float headYaw)
    {
        HeadYaw = headYaw;
    }
    
    /// <summary>
    /// Updates the sneaking state of the player.
    /// Returns true if the sneaking state changed.
    /// </summary>
    public bool UpdateSneakingState(bool isSneaking)
    {
        if (IsSneaking != isSneaking)
        {
            IsSneaking = isSneaking;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Marks a chunk as being loaded (thread-safe).
    /// Returns true if the chunk can be loaded (not already loaded or loading).
    /// </summary>
    public bool MarkChunkLoading(int chunkX, int chunkZ)
    {
        lock (_chunkLock)
        {
            if (LoadedChunks.Contains((chunkX, chunkZ)) || _chunksLoadingInProgress.Contains((chunkX, chunkZ)))
            {
                return false; // Already loaded or loading
            }
            _chunksLoadingInProgress.Add((chunkX, chunkZ));
            return true;
        }
    }

    /// <summary>
    /// Marks a chunk as loaded for this player (thread-safe).
    /// Should only be called after successfully sending the chunk.
    /// Returns true if the chunk was actually added (wasn't already loaded).
    /// </summary>
    public bool MarkChunkLoaded(int chunkX, int chunkZ)
    {
        lock (_chunkLock)
        {
            _chunksLoadingInProgress.Remove((chunkX, chunkZ)); // Remove from loading set
            return LoadedChunks.Add((chunkX, chunkZ));
        }
    }

    /// <summary>
    /// Marks a chunk loading as failed (thread-safe).
    /// Removes it from the loading set so it can be retried.
    /// </summary>
    public void MarkChunkLoadingFailed(int chunkX, int chunkZ)
    {
        lock (_chunkLock)
        {
            _chunksLoadingInProgress.Remove((chunkX, chunkZ));
        }
    }

    /// <summary>
    /// Checks if a chunk is loaded (thread-safe).
    /// </summary>
    public bool IsChunkLoaded(int chunkX, int chunkZ)
    {
        lock (_chunkLock)
        {
            return LoadedChunks.Contains((chunkX, chunkZ));
        }
    }

    /// <summary>
    /// Checks if a chunk is currently being loaded (thread-safe).
    /// </summary>
    public bool IsChunkLoading(int chunkX, int chunkZ)
    {
        lock (_chunkLock)
        {
            return _chunksLoadingInProgress.Contains((chunkX, chunkZ));
        }
    }

    /// <summary>
    /// Marks a chunk as unloaded for this player (thread-safe).
    /// </summary>
    public void MarkChunkUnloaded(int chunkX, int chunkZ)
    {
        lock (_chunkLock)
        {
            LoadedChunks.Remove((chunkX, chunkZ));
        }
    }


    public Vector3 CalculateDropVelocity()
    {
        // TODO: Implement drop velocity calculation
        throw new NotImplementedException();
    }

    /// <summary>
    /// Adds an entity to the visible entities set (thread-safe).
    /// Returns true if the entity was added (wasn't already visible).
    /// </summary>
    public bool AddVisibleEntity(int entityId)
    {
        lock (_entityVisibilityLock)
        {
            return VisibleEntityIds.Add(entityId);
        }
    }

    /// <summary>
    /// Removes an entity from the visible entities set (thread-safe).
    /// Returns true if the entity was removed (was visible).
    /// </summary>
    public bool RemoveVisibleEntity(int entityId)
    {
        lock (_entityVisibilityLock)
        {
            return VisibleEntityIds.Remove(entityId);
        }
    }

    /// <summary>
    /// Checks if an entity is currently visible (thread-safe).
    /// </summary>
    public bool IsEntityVisible(int entityId)
    {
        lock (_entityVisibilityLock)
        {
            return VisibleEntityIds.Contains(entityId);
        }
    }

    /// <summary>
    /// Gets a copy of all visible entity IDs (thread-safe).
    /// </summary>
    public HashSet<int> GetVisibleEntities()
    {
        lock (_entityVisibilityLock)
        {
            return new HashSet<int>(VisibleEntityIds);
        }
    }

    /// <summary>
    /// Clears all visible entities (thread-safe).
    /// </summary>
    public void ClearVisibleEntities()
    {
        lock (_entityVisibilityLock)
        {
            VisibleEntityIds.Clear();
        }
    }
}

