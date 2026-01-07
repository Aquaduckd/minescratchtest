using MineSharp.Game;
using MineSharp.Network.Handlers;
using MineSharp.World;

namespace MineSharp.Network;

/// <summary>
/// Manages chunk loading for a single player connection.
/// Maintains a desired chunks set and uses cancellation tokens to handle concurrent loading.
/// </summary>
public class ChunkLoader
{
    // Desired state: chunks that SHOULD be loaded
    private readonly HashSet<(int X, int Z)> _desiredChunks = new();
    private readonly object _desiredChunksLock = new();
    
    // Active loads: chunks currently being loaded with their cancellation tokens
    private readonly Dictionary<(int X, int Z), CancellationTokenSource> _activeLoads = new();
    private readonly object _activeLoadsLock = new();
    
    // Already loaded chunks (for reference)
    private readonly HashSet<(int X, int Z)> _loadedChunks = new();
    private readonly object _loadedChunksLock = new();
    
    // Background task that processes the loading queue
    private Task? _loadingTask;
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
    }

    /// <summary>
    /// Updates the desired chunks set.
    /// Adds new chunks to load and cancels loads for chunks that are no longer desired.
    /// </summary>
    public void UpdateDesiredChunks(HashSet<(int X, int Z)> newDesiredChunks)
    {
        lock (_desiredChunksLock)
        {
            // Find chunks to remove (no longer desired)
            var chunksToRemove = new List<(int X, int Z)>();
            foreach (var chunk in _desiredChunks)
            {
                if (!newDesiredChunks.Contains(chunk))
                {
                    chunksToRemove.Add(chunk);
                }
            }
            
            // Cancel loads for chunks that are no longer desired
            lock (_activeLoadsLock)
            {
                foreach (var chunk in chunksToRemove)
                {
                    if (_activeLoads.TryGetValue(chunk, out var cts))
                    {
                        cts.Cancel();
                        cts.Dispose();
                        _activeLoads.Remove(chunk);
                    }
                }
            }
            
            // Unload chunks that are no longer desired
            foreach (var chunk in chunksToRemove)
            {
                UnloadChunk(chunk.X, chunk.Z);
            }
            
            // Update desired chunks set
            _desiredChunks.Clear();
            foreach (var chunk in newDesiredChunks)
            {
                _desiredChunks.Add(chunk);
            }
        }
        
        // Trigger background loading task to process new chunks
        // (It will check desired chunks and start loading if needed)
    }

    /// <summary>
    /// Starts the background loading task that processes the chunk loading queue.
    /// </summary>
    public void StartLoading()
    {
        // Don't start if already running
        if (_loadingTask != null && !_loadingTask.IsCompleted)
        {
            return;
        }
        
        // Start background task
        _loadingTask = Task.Run(async () =>
        {
            try
            {
                while (!_shutdownToken.Token.IsCancellationRequested)
                {
                    // Get chunks that need loading
                    var chunksToLoad = new List<(int X, int Z)>();
                    
                    lock (_desiredChunksLock)
                    {
                        foreach (var chunk in _desiredChunks)
                        {
                            // Check if already loaded
                            if (IsChunkLoaded(chunk.X, chunk.Z))
                            {
                                continue;
                            }
                            
                            // Check if already loading
                            lock (_activeLoadsLock)
                            {
                                if (_activeLoads.ContainsKey(chunk))
                                {
                                    continue;
                                }
                            }
                            
                            chunksToLoad.Add(chunk);
                        }
                    }
                    
                    // Start loading tasks for chunks that need loading
                    foreach (var (chunkX, chunkZ) in chunksToLoad)
                    {
                        // Check shutdown token
                        if (_shutdownToken.Token.IsCancellationRequested)
                        {
                            break;
                        }
                        
                        // Create cancellation token for this specific chunk load
                        var chunkCts = CancellationTokenSource.CreateLinkedTokenSource(_shutdownToken.Token);
                        
                        // Add to active loads
                        lock (_activeLoadsLock)
                        {
                            // Double-check it's still needed
                            lock (_desiredChunksLock)
                            {
                                if (!_desiredChunks.Contains((chunkX, chunkZ)) || IsChunkLoaded(chunkX, chunkZ))
                                {
                                    chunkCts.Dispose();
                                    continue;
                                }
                            }
                            
                            _activeLoads[(chunkX, chunkZ)] = chunkCts;
                        }
                        
                        // Start loading task (fire and forget, but tracked via activeLoads)
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await LoadChunkAsync(chunkX, chunkZ, chunkCts.Token);
                            }
                            catch (OperationCanceledException)
                            {
                                // Expected when chunk is removed from desired set
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"  │  ✗ Error loading chunk ({chunkX}, {chunkZ}): {ex.Message}");
                            }
                            finally
                            {
                                // Remove from active loads
                                lock (_activeLoadsLock)
                                {
                                    if (_activeLoads.TryGetValue((chunkX, chunkZ), out var cts))
                                    {
                                        cts.Dispose();
                                        _activeLoads.Remove((chunkX, chunkZ));
                                    }
                                }
                            }
                        });
                    }
                    
                    // Wait a bit before checking again (avoid busy-waiting)
                    await Task.Delay(100, _shutdownToken.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when shutdown is requested
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  │  ✗ Error in chunk loader background task: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Loads a single chunk asynchronously.
    /// Checks cancellation token and verifies chunk is still desired before marking as loaded.
    /// </summary>
    private async Task LoadChunkAsync(int chunkX, int chunkZ, CancellationToken cancellationToken)
    {
        // Check cancellation before starting
        cancellationToken.ThrowIfCancellationRequested();
        
        // Verify chunk is still desired
        lock (_desiredChunksLock)
        {
            if (!_desiredChunks.Contains((chunkX, chunkZ)))
            {
                throw new OperationCanceledException($"Chunk ({chunkX}, {chunkZ}) is no longer desired");
            }
        }
        
        // Send chunk data
        await _playHandler.SendChunkDataForLoaderAsync(_connection, chunkX, chunkZ);
        
        // Check cancellation again after send
        cancellationToken.ThrowIfCancellationRequested();
        
        // Verify chunk is still desired before marking as loaded
        bool stillDesired;
        lock (_desiredChunksLock)
        {
            stillDesired = _desiredChunks.Contains((chunkX, chunkZ));
        }
        
        if (!stillDesired)
        {
            // Chunk was removed from desired set during send - don't mark as loaded
            throw new OperationCanceledException($"Chunk ({chunkX}, {chunkZ}) was removed from desired set during load");
        }
        
        // Mark as loaded (thread-safe)
        lock (_loadedChunksLock)
        {
            if (_loadedChunks.Add((chunkX, chunkZ)))
            {
                // Also update player's loaded chunks
                _player.MarkChunkLoaded(chunkX, chunkZ);
            }
        }
    }

    /// <summary>
    /// Unloads a chunk.
    /// Removes it from loaded set and sends unload packet if needed.
    /// </summary>
    public void UnloadChunk(int chunkX, int chunkZ)
    {
        lock (_loadedChunksLock)
        {
            if (_loadedChunks.Remove((chunkX, chunkZ)))
            {
                // Also remove from player's loaded chunks
                _player.MarkChunkUnloaded(chunkX, chunkZ);
                
                // TODO: Send unload chunk packet if needed
                // For now, Minecraft client handles this automatically when chunks go out of range
            }
        }
    }

    /// <summary>
    /// Shuts down the chunk loader.
    /// Cancels all active loads and stops the background task.
    /// </summary>
    public void Shutdown()
    {
        // Cancel shutdown token to stop background task
        _shutdownToken.Cancel();
        
        // Cancel all active loads
        lock (_activeLoadsLock)
        {
            foreach (var kvp in _activeLoads)
            {
                kvp.Value.Cancel();
                kvp.Value.Dispose();
            }
            _activeLoads.Clear();
        }
        
        // Wait for background task to complete (with timeout)
        if (_loadingTask != null)
        {
            try
            {
                _loadingTask.Wait(TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  │  ⚠ Warning: Error waiting for chunk loader shutdown: {ex.Message}");
            }
        }
        
        _shutdownToken.Dispose();
    }

    /// <summary>
    /// Gets the current set of loaded chunks (thread-safe).
    /// </summary>
    public HashSet<(int X, int Z)> GetLoadedChunks()
    {
        lock (_loadedChunksLock)
        {
            return new HashSet<(int X, int Z)>(_loadedChunks);
        }
    }

    /// <summary>
    /// Checks if a chunk is currently loaded (thread-safe).
    /// </summary>
    public bool IsChunkLoaded(int chunkX, int chunkZ)
    {
        lock (_loadedChunksLock)
        {
            return _loadedChunks.Contains((chunkX, chunkZ));
        }
    }
}

