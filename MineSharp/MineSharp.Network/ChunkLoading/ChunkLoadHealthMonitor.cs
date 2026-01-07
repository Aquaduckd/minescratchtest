using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MineSharp.Network.ChunkLoading;

/// <summary>
/// Monitors chunk loading health and performs auto-recovery.
/// Detects stuck loads, failed chunks, and orphaned state.
/// </summary>
public class ChunkLoadHealthMonitor
{
    private readonly ChunkLoadRequestManager _requestManager;
    private readonly TimeSpan _checkInterval;
    private readonly TimeSpan _stuckLoadTimeout;
    private readonly CancellationTokenSource _shutdownToken = new();
    private Task? _monitorTask;

    public ChunkLoadHealthMonitor(
        ChunkLoadRequestManager requestManager,
        TimeSpan checkInterval,
        TimeSpan stuckLoadTimeout)
    {
        _requestManager = requestManager;
        _checkInterval = checkInterval;
        _stuckLoadTimeout = stuckLoadTimeout;
    }

    /// <summary>
    /// Starts the health monitoring task.
    /// </summary>
    public void Start()
    {
        if (_monitorTask != null && !_monitorTask.IsCompleted)
        {
            return; // Already running
        }
        
        _monitorTask = Task.Run(async () => await MonitorLoop(_shutdownToken.Token));
    }

    /// <summary>
    /// Stops the health monitoring task.
    /// </summary>
    public void Stop()
    {
        _shutdownToken.Cancel();
        
        if (_monitorTask != null)
        {
            try
            {
                _monitorTask.Wait(TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  │  ⚠ Warning: Error waiting for health monitor shutdown: {ex.Message}");
            }
        }
        
        _shutdownToken.Dispose();
    }

    /// <summary>
    /// Health check loop that runs periodically.
    /// </summary>
    private async Task MonitorLoop(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                RecoverStuckLoads();
                RecoverFailedChunks();
                FixOrphanedState();
                
                await Task.Delay(_checkInterval, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when shutdown is requested
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  │  ✗ Error in health monitor: {ex.Message}");
        }
    }

    /// <summary>
    /// Detects and recovers from stuck loads.
    /// </summary>
    private void RecoverStuckLoads()
    {
        var stuckRequests = new List<ChunkLoadRequest>();
        
        // Find requests that have been in Loading state too long
        foreach (var request in _requestManager.GetQueuedRequests())
        {
            // Check all requests, not just queued ones
            // We need to check requests in Loading state
            // For now, we'll check via GetRequest for all desired chunks
        }
        
        var desiredChunks = _requestManager.GetDesiredChunks();
        foreach (var chunk in desiredChunks)
        {
            var request = _requestManager.GetRequest(chunk.X, chunk.Z);
            if (request != null && request.State == ChunkLoadState.Loading && request.StartedAt.HasValue)
            {
                var loadDuration = DateTime.UtcNow - request.StartedAt.Value;
                if (loadDuration > _stuckLoadTimeout)
                {
                    stuckRequests.Add(request);
                }
            }
        }
        
        // Recover stuck loads by marking as failed (will retry)
        foreach (var request in stuckRequests)
        {
            var failed = request.MarkFailed($"Stuck load timeout after {_stuckLoadTimeout.TotalSeconds}s");
            _requestManager.UpdateRequest(failed);
            Console.WriteLine($"  │  ⚠ Recovered stuck load: chunk ({request.ChunkX}, {request.ChunkZ})");
        }
    }

    /// <summary>
    /// Detects and recovers from failed chunks.
    /// </summary>
    private void RecoverFailedChunks()
    {
        var desiredChunks = _requestManager.GetDesiredChunks();
        var retryableRequests = new List<ChunkLoadRequest>();
        
        foreach (var chunk in desiredChunks)
        {
            var request = _requestManager.GetRequest(chunk.X, chunk.Z);
            if (request != null && request.State == ChunkLoadState.Failed)
            {
                // Retry failed chunks after cooldown (e.g., 2 seconds)
                const int retryCooldownSeconds = 2;
                if (request.LastRetryAt == null || 
                    (DateTime.UtcNow - request.LastRetryAt.Value).TotalSeconds >= retryCooldownSeconds)
                {
                    // Limit retries (e.g., max 3 retries)
                    const int maxRetries = 3;
                    if (request.RetryCount < maxRetries)
                    {
                        retryableRequests.Add(request);
                    }
                }
            }
        }
        
        // Mark failed chunks for retry
        foreach (var request in retryableRequests)
        {
            var retry = request.MarkForRetry().WithState(ChunkLoadState.Pending);
            _requestManager.UpdateRequest(retry);
        }
    }

    /// <summary>
    /// Detects and fixes orphaned state.
    /// </summary>
    private void FixOrphanedState()
    {
        // Check for requests that are in invalid states
        // For example, requests that are Loaded but not in desired chunks
        var desiredChunks = _requestManager.GetDesiredChunks();
        var desiredChunksSet = new HashSet<(int X, int Z)>(desiredChunks);
        
        // This is a placeholder - full implementation would check all requests
        // and fix any orphaned state (e.g., Loaded chunks not in desired set)
    }
}

