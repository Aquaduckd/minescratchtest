using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MineSharp.Game;
using MineSharp.Network.Handlers;

namespace MineSharp.Network.ChunkLoading;

/// <summary>
/// Manages a pool of worker tasks that process chunk load requests.
/// Workers pull from a priority queue and load chunks.
/// </summary>
public class ChunkLoadWorkerPool
{
    private readonly int _workerCount;
    private readonly ChunkLoadRequestManager _requestManager;
    private readonly ClientConnection _connection;
    private readonly PlayHandler _playHandler;
    private readonly Player _player;
    private readonly CancellationTokenSource _shutdownToken = new();
    
    private readonly List<Task> _workers = new();
    private readonly PriorityQueue<ChunkLoadRequest, int> _priorityQueue = new();
    private readonly object _queueLock = new();
    private readonly TimeSpan _loadTimeout = TimeSpan.FromSeconds(5);

    public ChunkLoadWorkerPool(
        int workerCount,
        ChunkLoadRequestManager requestManager,
        ClientConnection connection,
        PlayHandler playHandler,
        Player player)
    {
        _workerCount = workerCount;
        _requestManager = requestManager;
        _connection = connection;
        _playHandler = playHandler;
        _player = player;
    }

    /// <summary>
    /// Starts all worker tasks.
    /// </summary>
    public void Start()
    {
        if (_workers.Count > 0)
        {
            return; // Already started
        }
        
        for (int i = 0; i < _workerCount; i++)
        {
            var workerId = i;
            var workerTask = Task.Run(async () => await WorkerLoop(workerId, _shutdownToken.Token));
            _workers.Add(workerTask);
        }
    }

    /// <summary>
    /// Stops all worker tasks and waits for completion.
    /// </summary>
    public void Stop()
    {
        _shutdownToken.Cancel();
        
        try
        {
            Task.WaitAll(_workers.ToArray(), TimeSpan.FromSeconds(2));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  │  ⚠ Warning: Error waiting for worker pool shutdown: {ex.Message}");
        }
        
        _workers.Clear();
        _shutdownToken.Dispose();
    }

    /// <summary>
    /// Adds a request to the priority queue.
    /// </summary>
    public void EnqueueRequest(ChunkLoadRequest request)
    {
        lock (_queueLock)
        {
            // Use negative priority because PriorityQueue is min-heap (we want max priority first)
            _priorityQueue.Enqueue(request, -request.Priority);
        }
    }

    /// <summary>
    /// Refreshes the queue with current queued requests from the manager.
    /// </summary>
    public void RefreshQueue()
    {
        lock (_queueLock)
        {
            // Clear current queue
            while (_priorityQueue.Count > 0)
            {
                _priorityQueue.Dequeue();
            }
            
            // Add all queued requests from manager
            foreach (var request in _requestManager.GetQueuedRequests())
            {
                _priorityQueue.Enqueue(request, -request.Priority);
            }
        }
    }

    /// <summary>
    /// Worker task that processes requests from the queue.
    /// </summary>
    private async Task WorkerLoop(int workerId, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                ChunkLoadRequest? request = null;
                
                // Try to get a request from the queue
                lock (_queueLock)
                {
                    if (_priorityQueue.Count > 0)
                    {
                        request = _priorityQueue.Dequeue();
                    }
                }
                
                if (request == null)
                {
                    // No requests available, refresh queue from manager
                    RefreshQueue();
                    
                    // Still no requests, wait a bit
                    if (_priorityQueue.Count == 0)
                    {
                        await Task.Delay(50, cancellationToken);
                        continue;
                    }
                    
                    // Try again after refresh
                    lock (_queueLock)
                    {
                        if (_priorityQueue.Count > 0)
                        {
                            request = _priorityQueue.Dequeue();
                        }
                    }
                }
                
                if (request == null)
                {
                    continue;
                }
                
                // Check if request is still valid
                var currentRequest = _requestManager.GetRequest(request.ChunkX, request.ChunkZ);
                if (currentRequest == null || currentRequest.State == ChunkLoadState.Cancelled || currentRequest.State == ChunkLoadState.Loaded)
                {
                    continue; // Skip cancelled or already loaded chunks
                }
                
                // Update request to Loading state
                var loadingRequest = currentRequest.MarkStarted();
                _requestManager.UpdateRequest(loadingRequest);
                
                try
                {
                    // Load chunk with timeout
                    bool success = await LoadChunkWithTimeout(loadingRequest, cancellationToken);
                    
                    if (success)
                    {
                        // Verify chunk is still desired before marking as loaded
                        var finalRequest = _requestManager.GetRequest(request.ChunkX, request.ChunkZ);
                        if (finalRequest != null && finalRequest.State != ChunkLoadState.Cancelled)
                        {
                            var loadedRequest = finalRequest.WithState(ChunkLoadState.Loaded);
                            _requestManager.UpdateRequest(loadedRequest);
                            
                            // Also update player's loaded chunks
                            _player.MarkChunkLoaded(request.ChunkX, request.ChunkZ);
                        }
                    }
                    else
                    {
                        // Timeout or error - mark as failed
                        var failedRequest = loadingRequest.MarkFailed("Timeout or error during load");
                        _requestManager.UpdateRequest(failedRequest);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Cancellation requested - mark as cancelled
                    var cancelledRequest = loadingRequest.WithState(ChunkLoadState.Cancelled);
                    _requestManager.UpdateRequest(cancelledRequest);
                }
                catch (Exception ex)
                {
                    // Error during load - mark as failed
                    var failedRequest = loadingRequest.MarkFailed(ex.Message);
                    _requestManager.UpdateRequest(failedRequest);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when shutdown is requested
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  │  ✗ Error in worker {workerId}: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads a single chunk with timeout.
    /// </summary>
    private async Task<bool> LoadChunkWithTimeout(ChunkLoadRequest request, CancellationToken cancellationToken)
    {
        try
        {
            using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeoutCts.CancelAfter(_loadTimeout);
                
                await _playHandler.SendChunkDataForLoaderAsync(_connection, request.ChunkX, request.ChunkZ);
                
                return true;
            }
        }
        catch (OperationCanceledException)
        {
            return false; // Timeout or cancellation
        }
        catch (Exception)
        {
            return false; // Error
        }
    }
}

