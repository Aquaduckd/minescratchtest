namespace MineSharp.World.ChunkDiffs;

using System.Collections.Concurrent;
using System.Linq;
using MineSharp.World;

/// <summary>
/// Manages chunk diffs across all chunks.
/// Thread-safe singleton that persists diffs in memory.
/// </summary>
public class ChunkDiffManager
{
    // Key: (chunkX, chunkZ), Value: ChunkDiff for that chunk
    private readonly ConcurrentDictionary<(int chunkX, int chunkZ), ChunkDiff> _diffs;
    
    private ChunkDiffManager()
    {
        _diffs = new ConcurrentDictionary<(int chunkX, int chunkZ), ChunkDiff>();
    }
    
    private static ChunkDiffManager? _instance;
    private static readonly object _lock = new object();
    
    /// <summary>
    /// Gets the singleton instance of ChunkDiffManager.
    /// </summary>
    public static ChunkDiffManager Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new ChunkDiffManager();
                    }
                }
            }
            return _instance;
        }
    }
    
    /// <summary>
    /// Gets or creates a ChunkDiff for the specified chunk.
    /// </summary>
    public ChunkDiff GetOrCreateDiff(int chunkX, int chunkZ)
    {
        return _diffs.GetOrAdd((chunkX, chunkZ), _ => new ChunkDiff(chunkX, chunkZ));
    }
    
    /// <summary>
    /// Gets a ChunkDiff for the specified chunk, or null if it doesn't exist.
    /// </summary>
    public ChunkDiff? GetDiff(int chunkX, int chunkZ)
    {
        _diffs.TryGetValue((chunkX, chunkZ), out ChunkDiff? diff);
        return diff;
    }
    
    /// <summary>
    /// Records a block change at world coordinates (x, y, z).
    /// Automatically determines which chunk the block belongs to.
    /// </summary>
    public void RecordBlockChange(int x, int y, int z, int blockStateId)
    {
        int chunkX = x >> 4;  // Divide by 16
        int chunkZ = z >> 4;
        
        var diff = GetOrCreateDiff(chunkX, chunkZ);
        diff.SetBlock(x, y, z, blockStateId);
    }
    
    /// <summary>
    /// Applies all diffs for a chunk to the chunk object.
    /// Should be called after chunk generation, before sending to clients.
    /// </summary>
    public void ApplyDiffsToChunk(Chunk chunk)
    {
        var diff = GetDiff(chunk.ChunkX, chunk.ChunkZ);
        if (diff == null || diff.IsEmpty)
        {
            return; // No diffs to apply
        }
        
        foreach (var change in diff.GetAllChanges())
        {
            // Convert world coordinates to local chunk coordinates
            int localX = change.X - (chunk.ChunkX * 16);
            int localZ = change.Z - (chunk.ChunkZ * 16);
            
            // Validate coordinates (should already be validated, but double-check)
            if (localX < 0 || localX >= 16 || localZ < 0 || localZ >= 16)
            {
                continue; // Skip invalid coordinates
            }
            
            var block = new Block(change.BlockStateId);
            chunk.SetBlock(localX, change.Y, localZ, block);
        }
    }
    
    /// <summary>
    /// Clears all diffs for a chunk (useful for resetting chunks).
    /// </summary>
    public void ClearDiff(int chunkX, int chunkZ)
    {
        if (_diffs.TryRemove((chunkX, chunkZ), out ChunkDiff? diff))
        {
            diff.Clear();
        }
    }
    
    /// <summary>
    /// Gets statistics about stored diffs.
    /// </summary>
    public (int chunkCount, int totalBlockChanges) GetStatistics()
    {
        int totalBlockChanges = _diffs.Values.Sum(diff => diff.Count);
        return (_diffs.Count, totalBlockChanges);
    }
}



