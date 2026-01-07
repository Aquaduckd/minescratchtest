using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace MineSharp.Network.ChunkLoading;

/// <summary>
/// Manages chunk load requests and desired chunks set.
/// Handles debouncing of updates and creates/updates load requests.
/// </summary>
public class ChunkLoadRequestManager
{
    private readonly HashSet<(int X, int Z)> _desiredChunks = new();
    private readonly object _desiredChunksLock = new();
    
    private readonly ConcurrentDictionary<(int X, int Z), ChunkLoadRequest> _requests = new();
    
    private readonly int _debounceMs;
    private DateTime _lastUpdateTime = DateTime.MinValue;
    private HashSet<(int X, int Z)>? _pendingDesiredChunks;
    private bool _hasProcessedInitialUpdate = false;
    private readonly object _updateLock = new();

    public ChunkLoadRequestManager(int debounceMs = 150)
    {
        _debounceMs = debounceMs;
    }

    /// <summary>
    /// Updates the desired chunks set (debounced).
    /// </summary>
    public void UpdateDesiredChunks(HashSet<(int X, int Z)> newDesiredChunks)
    {
        lock (_updateLock)
        {
            _pendingDesiredChunks = new HashSet<(int X, int Z)>(newDesiredChunks);
            _lastUpdateTime = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Processes pending updates if debounce period has elapsed.
    /// Returns true if updates were processed, false if still debouncing.
    /// </summary>
    public bool ProcessPendingUpdates(int playerChunkX, int playerChunkZ)
    {
        HashSet<(int X, int Z)>? pendingChunks = null;
        bool shouldProcess = false;
        
        lock (_updateLock)
        {
            if (_pendingDesiredChunks == null)
            {
                return false; // No pending updates
            }
            
            // First update processes immediately (no debounce needed)
            // Subsequent updates are debounced
            var timeSinceUpdate = DateTime.UtcNow - _lastUpdateTime;
            bool isFirstUpdate = !_hasProcessedInitialUpdate;
            
            if (isFirstUpdate || timeSinceUpdate.TotalMilliseconds >= _debounceMs)
            {
                pendingChunks = _pendingDesiredChunks;
                _pendingDesiredChunks = null;
                _hasProcessedInitialUpdate = true;
                shouldProcess = true;
            }
        }
        
        if (!shouldProcess || pendingChunks == null)
        {
            return false;
        }
        
        // Process the update
        lock (_desiredChunksLock)
        {
            // Find chunks to remove (no longer desired)
            var chunksToRemove = new List<(int X, int Z)>();
            foreach (var chunk in _desiredChunks)
            {
                if (!pendingChunks.Contains(chunk))
                {
                    chunksToRemove.Add(chunk);
                }
            }
            
            // Cancel requests for removed chunks
            foreach (var chunk in chunksToRemove)
            {
                CancelRequest(chunk.X, chunk.Z);
            }
            
            // Update desired chunks set
            _desiredChunks.Clear();
            foreach (var chunk in pendingChunks)
            {
                _desiredChunks.Add(chunk);
                
                // Create or update request for new chunks
                if (!_requests.ContainsKey(chunk))
                {
                    var priority = ChunkLoadPriorityCalculator.CalculatePriority(
                        chunk.X, chunk.Z, playerChunkX, playerChunkZ, DateTime.UtcNow, 0, isStable: true);
                    
                    var request = new ChunkLoadRequest(
                        chunk.X, chunk.Z, ChunkLoadState.Pending, priority, DateTime.UtcNow);
                    
                    _requests[chunk] = request;
                }
                else
                {
                    // Update priority for existing request if it's not loading
                    var existing = _requests[chunk];
                    if (existing.State != ChunkLoadState.Loading && existing.State != ChunkLoadState.Loaded)
                    {
                        var newPriority = ChunkLoadPriorityCalculator.CalculatePriority(
                            chunk.X, chunk.Z, playerChunkX, playerChunkZ, existing.CreatedAt, existing.RetryCount, isStable: true);
                        
                        var updated = existing.WithPriority(newPriority);
                        if (updated.State == ChunkLoadState.Cancelled)
                        {
                            // Re-activate cancelled chunks
                            updated = updated.WithState(ChunkLoadState.Pending);
                        }
                        
                        _requests[chunk] = updated;
                    }
                }
            }
        }
        
        return true;
    }

    /// <summary>
    /// Gets all requests that are ready to be loaded (Queued or Pending state).
    /// </summary>
    public IEnumerable<ChunkLoadRequest> GetQueuedRequests()
    {
        return _requests.Values
            .Where(r => r.State == ChunkLoadState.Queued || r.State == ChunkLoadState.Pending)
            .OrderByDescending(r => r.Priority);
    }

    /// <summary>
    /// Gets a request by chunk coordinates.
    /// </summary>
    public ChunkLoadRequest? GetRequest(int chunkX, int chunkZ)
    {
        _requests.TryGetValue((chunkX, chunkZ), out var request);
        return request;
    }

    /// <summary>
    /// Updates a request (creates new immutable instance).
    /// </summary>
    public void UpdateRequest(ChunkLoadRequest request)
    {
        _requests[(request.ChunkX, request.ChunkZ)] = request;
    }

    /// <summary>
    /// Cancels a request (sets state to Cancelled).
    /// </summary>
    public void CancelRequest(int chunkX, int chunkZ)
    {
        if (_requests.TryGetValue((chunkX, chunkZ), out var request))
        {
            var cancelled = request.WithState(ChunkLoadState.Cancelled);
            _requests[(chunkX, chunkZ)] = cancelled;
        }
    }

    /// <summary>
    /// Gets the current desired chunks set (thread-safe).
    /// </summary>
    public HashSet<(int X, int Z)> GetDesiredChunks()
    {
        lock (_desiredChunksLock)
        {
            return new HashSet<(int X, int Z)>(_desiredChunks);
        }
    }
}

