namespace MineSharp.World.ChunkDiffs;

using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Stores a collection of block changes for a specific chunk.
/// Thread-safe for concurrent access.
/// </summary>
public class ChunkDiff
{
    public int ChunkX { get; }
    public int ChunkZ { get; }
    
    private readonly Dictionary<(int x, int y, int z), BlockChange> _changes;
    private readonly object _lock = new object();

    public ChunkDiff(int chunkX, int chunkZ)
    {
        ChunkX = chunkX;
        ChunkZ = chunkZ;
        _changes = new Dictionary<(int, int, int), BlockChange>();
    }

    /// <summary>
    /// Sets a block change at the specified world coordinates.
    /// </summary>
    public void SetBlock(int x, int y, int z, int blockStateId)
    {
        lock (_lock)
        {
            var key = (x, y, z);
            _changes[key] = new BlockChange(x, y, z, blockStateId);
        }
    }

    /// <summary>
    /// Gets a block change at the specified world coordinates, or null if not changed.
    /// </summary>
    public BlockChange? GetBlockChange(int x, int y, int z)
    {
        lock (_lock)
        {
            _changes.TryGetValue((x, y, z), out var change);
            return change;
        }
    }

    /// <summary>
    /// Gets the block state ID at the specified world coordinates, or 0 if not changed.
    /// </summary>
    public int GetBlock(int x, int y, int z)
    {
        lock (_lock)
        {
            if (_changes.TryGetValue((x, y, z), out var change))
            {
                return change.BlockStateId;
            }
            return 0; // No change recorded
        }
    }

    /// <summary>
    /// Gets all block changes in this chunk.
    /// </summary>
    public IEnumerable<BlockChange> GetAllChanges()
    {
        lock (_lock)
        {
            return _changes.Values.ToList();
        }
    }

    /// <summary>
    /// Checks if this chunk diff is empty (no changes).
    /// </summary>
    public bool IsEmpty
    {
        get
        {
            lock (_lock)
            {
                return _changes.Count == 0;
            }
        }
    }

    /// <summary>
    /// Gets the number of block changes in this chunk.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _changes.Count;
            }
        }
    }

    /// <summary>
    /// Clears all block changes in this chunk.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _changes.Clear();
        }
    }
}

