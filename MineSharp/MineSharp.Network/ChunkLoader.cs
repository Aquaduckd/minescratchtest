using MineSharp.Game;
using MineSharp.Network.ChunkLoading;
using MineSharp.Network.Handlers;
using MineSharp.World;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MineSharp.Network;

/// <summary>
/// Manages chunk loading for a single player connection.
/// Uses the robust chunk loading system with request management, worker pool, and health monitoring.
/// </summary>
public class ChunkLoader
{
    // New robust system components
    private readonly ChunkLoadRequestManager _requestManager;
    private readonly ChunkLoadWorkerPool _workerPool;
    private readonly ChunkLoadHealthMonitor _healthMonitor;
    
    // Background task that processes pending updates and refreshes queue
    private Task? _updateProcessorTask;
    private readonly CancellationTokenSource _shutdownToken = new();
    
    // Dependencies
    private readonly ClientConnection _connection;
    private readonly MineSharp.World.World _world;
    private readonly Player _player;
    private readonly ChunkManager _chunkManager;
    private readonly PlayHandler _playHandler;

    public ChunkLoader(ClientConnection connection, MineSharp.World.World world, Player player, PlayHandler playHandler)
    {
        _connection = connection;
        _world = world;
        _player = player;
        _chunkManager = world.ChunkManager;
        _playHandler = playHandler;
        
        // Initialize robust system components
        _requestManager = new ChunkLoadRequestManager(debounceMs: 150);
        _workerPool = new ChunkLoadWorkerPool(
            workerCount: 6,
            requestManager: _requestManager,
            connection: _connection,
            playHandler: _playHandler,
            player: _player);
        _healthMonitor = new ChunkLoadHealthMonitor(
            requestManager: _requestManager,
            checkInterval: TimeSpan.FromSeconds(2),
            stuckLoadTimeout: TimeSpan.FromSeconds(10));
    }

    /// <summary>
    /// Updates the desired chunks set (debounced).
    /// Adds new chunks to load and cancels loads for chunks that are no longer desired.
    /// </summary>
    public void UpdateDesiredChunks(HashSet<(int X, int Z)> newDesiredChunks)
    {
        // Update request manager (debounced)
        _requestManager.UpdateDesiredChunks(newDesiredChunks);
        
        // Unload chunks that are no longer desired
        var currentDesired = _requestManager.GetDesiredChunks();
        var loadedChunks = GetLoadedChunks();
        
        foreach (var loadedChunk in loadedChunks)
        {
            if (!currentDesired.Contains(loadedChunk))
            {
                UnloadChunk(loadedChunk.X, loadedChunk.Z);
            }
        }
    }

    /// <summary>
    /// Forces immediate processing of pending updates (bypasses debounce).
    /// Useful for spawn chunks that need immediate loading.
    /// </summary>
    public void ProcessUpdatesImmediately()
    {
        int playerChunkX = _player.ChunkX;
        int playerChunkZ = _player.ChunkZ;
        
        if (_requestManager.ProcessPendingUpdates(playerChunkX, playerChunkZ))
        {
            // Updates were processed, refresh worker queue
            _workerPool.RefreshQueue();
        }
    }

    /// <summary>
    /// Starts the chunk loading system (worker pool, health monitor, and update processor).
    /// </summary>
    public void StartLoading()
    {
        // Start worker pool
        _workerPool.Start();
        
        // Start health monitor
        _healthMonitor.Start();
        
        // Start update processor task (processes pending updates and refreshes queue)
        if (_updateProcessorTask == null || _updateProcessorTask.IsCompleted)
        {
            _updateProcessorTask = Task.Run(async () => await UpdateProcessorLoop(_shutdownToken.Token));
        }
    }

    /// <summary>
    /// Background task that processes pending updates and refreshes the worker queue.
    /// </summary>
    private async Task UpdateProcessorLoop(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Process pending updates (debounced)
                int playerChunkX = _player.ChunkX;
                int playerChunkZ = _player.ChunkZ;
                
                if (_requestManager.ProcessPendingUpdates(playerChunkX, playerChunkZ))
                {
                    // Updates were processed, refresh worker queue with new requests
                    _workerPool.RefreshQueue();
                }
                
                // Also refresh queue periodically to pick up any new queued requests
                await Task.Delay(100, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when shutdown is requested
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  │  ✗ Error in update processor: {ex.Message}");
        }
    }

    /// <summary>
    /// Unloads a chunk.
    /// Removes it from loaded set and sends unload packet if needed.
    /// </summary>
    public void UnloadChunk(int chunkX, int chunkZ)
    {
        // Remove from player's loaded chunks
        _player.MarkChunkUnloaded(chunkX, chunkZ);
        
        // Cancel request if it exists
        _requestManager.CancelRequest(chunkX, chunkZ);
        
        // TODO: Send unload chunk packet if needed
        // For now, Minecraft client handles this automatically when chunks go out of range
    }

    /// <summary>
    /// Shuts down the chunk loader.
    /// Stops worker pool, health monitor, and update processor.
    /// </summary>
    public void Shutdown()
    {
        // Cancel shutdown token
        _shutdownToken.Cancel();
        
        // Stop all components
        _workerPool.Stop();
        _healthMonitor.Stop();
        
        // Wait for update processor task
        if (_updateProcessorTask != null)
        {
            try
            {
                _updateProcessorTask.Wait(TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  │  ⚠ Warning: Error waiting for update processor shutdown: {ex.Message}");
            }
        }
        
        _shutdownToken.Dispose();
    }

    /// <summary>
    /// Gets the current set of loaded chunks (thread-safe).
    /// </summary>
    public HashSet<(int X, int Z)> GetLoadedChunks()
    {
        // Get loaded chunks from player (which is updated by worker pool)
        return new HashSet<(int X, int Z)>(_player.LoadedChunks);
    }

    /// <summary>
    /// Checks if a chunk is currently loaded (thread-safe).
    /// </summary>
    public bool IsChunkLoaded(int chunkX, int chunkZ)
    {
        return _player.IsChunkLoaded(chunkX, chunkZ);
    }
}




