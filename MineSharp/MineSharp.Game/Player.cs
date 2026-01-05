using MineSharp.Core.DataTypes;

namespace MineSharp.Game;

/// <summary>
/// Represents a player in the world.
/// Encapsulates all player-specific state including position, rotation, and inventory.
/// </summary>
public class Player
{
    public Guid Uuid { get; }
    public Vector3 Position { get; private set; }
    public int ChunkX { get; private set; }
    public int ChunkZ { get; private set; }
    public float Yaw { get; private set; }
    public float Pitch { get; private set; }
    public int ViewDistance { get; }
    public Inventory Inventory { get; }
    public HashSet<(int X, int Z)> LoadedChunks { get; }

    public Player(Guid uuid, int viewDistance = 10)
    {
        Uuid = uuid;
        ViewDistance = viewDistance;
        Position = new Vector3(0, 64, 0);
        ChunkX = 0;
        ChunkZ = 0;
        Yaw = 0;
        Pitch = 0;
        Inventory = new Inventory();
        LoadedChunks = new HashSet<(int, int)>();
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
    }

    /// <summary>
    /// Marks a chunk as loaded for this player.
    /// </summary>
    public void MarkChunkLoaded(int chunkX, int chunkZ)
    {
        LoadedChunks.Add((chunkX, chunkZ));
    }

    /// <summary>
    /// Marks a chunk as unloaded for this player.
    /// </summary>
    public void MarkChunkUnloaded(int chunkX, int chunkZ)
    {
        LoadedChunks.Remove((chunkX, chunkZ));
    }


    public Vector3 CalculateDropVelocity()
    {
        // TODO: Implement drop velocity calculation
        throw new NotImplementedException();
    }
}

