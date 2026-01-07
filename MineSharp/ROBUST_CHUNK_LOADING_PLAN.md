# Robust Chunk Loading System Plan

## Overview
Design a chunk loading system that maintains a desired state (list of chunks that should be loaded) and uses cancellation tokens to handle concurrent loading with proper cleanup.

## Architecture

### 1. `ChunkLoader` Class
A dedicated class that manages chunk loading for a single player connection.

**Responsibilities:**
- Maintain a thread-safe set of "desired chunks" (chunks that should be loaded)
- Track active loading tasks with cancellation tokens
- Process the queue of chunks to load in background threads
- Cancel loads when chunks are removed from the desired set
- Handle chunk unloading

**State:**
```csharp
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
    private readonly World _world;
    private readonly Player _player;
    private readonly ChunkManager _chunkManager;
}
```

**Key Methods:**
- `UpdateDesiredChunks(HashSet<(int, int)> newDesiredChunks)`: Update the desired chunks list
  - Add new chunks to desired set
  - Remove chunks from desired set (and cancel their loads)
  - Trigger background loading task if needed
  
- `StartLoading()`: Start background task that processes loading queue
  - Continuously monitor desired chunks
  - For each desired chunk that's not loaded and not loading:
    - Create CancellationTokenSource
    - Start loading task with cancellation token
    - Track the active load
    
- `LoadChunkAsync(int chunkX, int chunkZ, CancellationToken cancellationToken)`: Load a single chunk
  - Check cancellation token periodically
  - Send chunk data
  - Verify chunk is still desired before marking as loaded
  - Mark as loaded on success
  
- `UnloadChunk(int chunkX, int chunkZ)`: Unload a chunk
  - Remove from loaded set
  - Send unload packet if needed
  
- `Shutdown()`: Clean up all resources
  - Cancel all active loads
  - Stop background task
  - Dispose cancellation tokens

### 2. Integration Points

**Player.cs:**
- Add `ChunkLoader? ChunkLoader { get; set; }` property
- Keep `LoadedChunks` for backward compatibility (updated by ChunkLoader)
- Remove manual chunk loading tracking methods (or keep for compatibility)

**PlayHandler.cs:**
- Replace `UpdatePlayerChunksAsync` calls with `UpdateDesiredChunks` calls
- When player moves:
  1. Calculate desired chunks using `ChunkManager.GetChunksInRange()`
  2. Call `player.ChunkLoader.UpdateDesiredChunks(desiredChunks)`
  3. The ChunkLoader handles the rest

**ClientConnection.cs:**
- Initialize `ChunkLoader` when entering Play state
- Shutdown `ChunkLoader` when disconnecting

## Flow

### Initial Load (Spawn)
1. Calculate desired chunks (3x3 grid around spawn)
2. Create ChunkLoader
3. Call `UpdateDesiredChunks(desiredChunks)`
4. ChunkLoader starts loading chunks in background
5. Send player position after initial chunks loaded

### Player Movement
1. Player position updates
2. Calculate new desired chunks based on current position
3. Call `UpdateDesiredChunks(newDesiredChunks)`
4. ChunkLoader:
   - Identifies chunks to add (start loading with cancellation tokens)
   - Identifies chunks to remove (cancel their loads, unload them)
   - Background task processes the queue

### Chunk Loading Task
1. Check if chunk is still desired (if not, exit early)
2. Check cancellation token (if cancelled, exit early)
3. Send chunk data
4. Check cancellation token again
5. Verify chunk is still desired
6. Mark as loaded

## Benefits

1. **Single Source of Truth**: Desired chunks set is the authoritative state
2. **Proper Cancellation**: Uses CancellationToken for clean task cancellation
3. **Thread Safety**: All state access is properly locked
4. **Separation of Concerns**: Loading logic separated from position tracking
5. **Robustness**: Handles rapid player movement gracefully
6. **Testability**: ChunkLoader can be tested independently

## Implementation Steps

1. Create `ChunkLoader.cs` class with basic structure
2. Implement `UpdateDesiredChunks` method
3. Implement background loading task
4. Implement `LoadChunkAsync` with cancellation support
5. Integrate with Player and PlayHandler
6. Test with rapid player movement
7. Add error handling and logging

