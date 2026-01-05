using System;
using System.Collections.Generic;

namespace MineSharp.World;

/// <summary>
/// Manages chunk loading and unloading based on player position.
/// </summary>
public class ChunkManager
{
    private readonly int _viewDistance;
    private readonly int _loadingRadius;

    public ChunkManager(int viewDistance = 10)
    {
        _viewDistance = viewDistance;
        _loadingRadius = viewDistance + 2;  // Add buffer for neighbors
    }

    public (int chunkX, int chunkZ) WorldToChunk(float worldX, float worldZ)
    {
        // Chunk coordinates are floor division by 16
        int chunkX = (int)Math.Floor(worldX / 16.0);
        int chunkZ = (int)Math.Floor(worldZ / 16.0);
        return (chunkX, chunkZ);
    }

    public List<(int X, int Z)> GetChunksInRange(int centerChunkX, int centerChunkZ)
    {
        var chunks = new List<(int X, int Z)>();
        
        for (int x = -_loadingRadius; x <= _loadingRadius; x++)
        {
            for (int z = -_loadingRadius; z <= _loadingRadius; z++)
            {
                // Check if chunk is within view distance (circular)
                double distance = Math.Sqrt(x * x + z * z);
                if (distance <= _viewDistance)
                {
                    chunks.Add((centerChunkX + x, centerChunkZ + z));
                }
            }
        }
        
        return chunks;
    }

    public List<(int X, int Z)> GetChunksToLoad(HashSet<(int X, int Z)> currentlyLoaded, int centerChunkX, int centerChunkZ)
    {
        var chunksInRange = GetChunksInRange(centerChunkX, centerChunkZ);
        var chunksToLoad = new List<(int X, int Z)>();
        
        foreach (var chunk in chunksInRange)
        {
            if (!currentlyLoaded.Contains(chunk))
            {
                chunksToLoad.Add(chunk);
            }
        }
        
        return chunksToLoad;
    }

    public List<(int X, int Z)> GetChunksToUnload(HashSet<(int X, int Z)> currentlyLoaded, int centerChunkX, int centerChunkZ)
    {
        var chunksInRange = GetChunksInRange(centerChunkX, centerChunkZ);
        var chunksInRangeSet = new HashSet<(int X, int Z)>(chunksInRange);
        var chunksToUnload = new List<(int X, int Z)>();
        
        foreach (var chunk in currentlyLoaded)
        {
            if (!chunksInRangeSet.Contains(chunk))
            {
                chunksToUnload.Add(chunk);
            }
        }
        
        return chunksToUnload;
    }
}

