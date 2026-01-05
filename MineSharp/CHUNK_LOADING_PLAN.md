# Chunk Loading/Unloading System Plan

## Overview
Maintain a 10x10 square of chunks (view distance 10) around each player, automatically loading new chunks and unloading distant ones as players move.

## Current State

### What We Have
- ✅ `Player.LoadedChunks` - HashSet tracking loaded chunks per player
- ✅ `ChunkManager.GetChunksInRange()` - Gets all chunks within view distance (circular)
- ✅ `ChunkManager.GetChunksToLoad()` - Calculates chunks that need loading
- ✅ `ChunkManager.GetChunksToUnload()` - Calculates chunks that should be unloaded
- ✅ `Player.UpdatePosition()` - Detects chunk boundary crossings
- ✅ `PlayHandler.SendChunkDataAsync()` - Sends chunk data packets to client
- ✅ `PlayHandler.SendSetCenterChunkAsync()` - Updates client's center chunk

### What We Need
- ❌ Method to update player's loaded chunks set
- ❌ Method to trigger chunk loading/unloading when boundary crossed
- ❌ Method to send unload chunk packets (if needed)
- ❌ Integration with position update handlers
- ❌ Initial chunk loading on PLAY state entry

## Architecture

### Key Components

1. **Player Chunk Tracking**
   - `Player.LoadedChunks` - Already exists, tracks (chunkX, chunkZ) tuples
   - Methods to mark chunks as loaded/unloaded

2. **Chunk Loading Logic**
   - Calculate chunks to load/unload using `ChunkManager`
   - Send chunk data packets for new chunks
   - Update player's `LoadedChunks` set
   - Update center chunk if needed

3. **Chunk Unloading Logic**
   - Calculate chunks outside view distance
   - Mark chunks as unloaded in player's set
   - Optionally send unload chunk packets (Minecraft protocol may handle this automatically)

4. **Trigger Points**
   - Initial PLAY state entry (spawn position)
   - Chunk boundary crossings (detected in position update handlers)

## Implementation Plan

### Phase 1: Player Chunk Management Methods

**File: `MineSharp.Game/Player.cs`**

Add helper methods:
```csharp
public void MarkChunkLoaded(int chunkX, int chunkZ)
{
    LoadedChunks.Add((chunkX, chunkZ));
}

public void MarkChunkUnloaded(int chunkX, int chunkZ)
{
    LoadedChunks.Remove((chunkX, chunkZ));
}

public List<(int X, int Z)> GetChunksToLoad(ChunkManager chunkManager)
{
    return chunkManager.GetChunksToLoad(LoadedChunks, ChunkX, ChunkZ);
}

public List<(int X, int Z)> GetChunksToUnload(ChunkManager chunkManager)
{
    return chunkManager.GetChunksToUnload(LoadedChunks, ChunkX, ChunkZ);
}
```

### Phase 2: Chunk Loading/Unloading Handler

**File: `MineSharp.Network/Handlers/PlayHandler.cs`**

Add method to handle chunk updates:
```csharp
public async Task UpdatePlayerChunksAsync(ClientConnection connection, Player player)
{
    if (_world == null) return;
    
    var chunkManager = _world.ChunkManager;
    
    // Get chunks that need to be loaded
    var chunksToLoad = player.GetChunksToLoad(chunkManager);
    
    // Get chunks that should be unloaded
    var chunksToUnload = player.GetChunksToUnload(chunkManager);
    
    // Load new chunks
    if (chunksToLoad.Count > 0)
    {
        Console.WriteLine($"  │  → Loading {chunksToLoad.Count} new chunk(s)...");
        
        foreach (var (chunkX, chunkZ) in chunksToLoad)
        {
            try
            {
                await SendChunkDataAsync(connection, chunkX, chunkZ);
                player.MarkChunkLoaded(chunkX, chunkZ);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  │  ✗ Error loading chunk ({chunkX}, {chunkZ}): {ex.Message}");
            }
        }
        
        // Update center chunk to player's current chunk
        await SendSetCenterChunkAsync(connection, player.ChunkX, player.ChunkZ);
    }
    
    // Unload distant chunks
    if (chunksToUnload.Count > 0)
    {
        Console.WriteLine($"  │  → Unloading {chunksToUnload.Count} distant chunk(s)...");
        
        foreach (var (chunkX, chunkZ) in chunksToUnload)
        {
            player.MarkChunkUnloaded(chunkX, chunkZ);
            // Note: Minecraft client automatically unloads chunks outside view distance
            // We may not need to send explicit unload packets, but we track it server-side
        }
    }
}
```

### Phase 3: Integration Points

**File: `MineSharp.Network/Handlers/PlayHandler.cs`**

1. **Initial Chunk Loading** - Update `SendInitialChunksAsync()`:
```csharp
public async Task SendInitialChunksAsync(ClientConnection connection)
{
    if (_world == null || connection.Player == null) return;
    
    var player = connection.Player;
    
    // Use the new UpdatePlayerChunksAsync method
    await UpdatePlayerChunksAsync(connection, player);
    
    Console.WriteLine($"  │  ✓ Initial chunks loaded ({player.LoadedChunks.Count} total)");
}
```

2. **Position Update Handlers** - Update `HandleSetPlayerPositionAsync()` and `HandleSetPlayerPositionAndRotationAsync()`:
```csharp
// After detecting chunk boundary crossing:
if (chunkChange.HasValue)
{
    var (oldChunkX, oldChunkZ, newChunkX, newChunkZ) = chunkChange.Value;
    Console.WriteLine($"  │  → Chunk boundary crossed: ({oldChunkX}, {oldChunkZ}) → ({newChunkX}, {newChunkZ})");
    
    // Trigger chunk loading/unloading
    await UpdatePlayerChunksAsync(connection, player);
}
```

### Phase 4: Chunk Unload Packets (Optional)

**Note**: Minecraft protocol may handle chunk unloading automatically on the client side when chunks are outside view distance. However, if we need explicit control:

**File: `MineSharp.Core/Protocol/PacketBuilder.cs`**

Add method to build unload chunk packet (if needed):
```csharp
public static byte[] BuildUnloadChunkPacket(int chunkX, int chunkZ)
{
    // Packet ID 0x1F (Unload Chunk) - if protocol requires it
    // Most clients handle this automatically, but check protocol docs
}
```

## Chunk Loading Strategy

### View Distance = 10
- **Square**: 21x21 chunks (10 in each direction + center = 21 total per axis)
- **Circular**: ~314 chunks (π * 10²)
- **Current Implementation**: Uses circular distance check

### Loading Behavior
1. **Initial Load**: When player enters PLAY state, load all chunks in range around spawn
2. **Boundary Crossing**: When player crosses chunk boundary:
   - Calculate new chunks to load (chunks in range that aren't loaded)
   - Calculate chunks to unload (loaded chunks outside range)
   - Load new chunks immediately
   - Mark old chunks as unloaded
3. **Center Chunk Update**: Update client's center chunk after loading new chunks

### Performance Considerations
- Load chunks synchronously (one at a time) to avoid overwhelming client
- Consider async/background loading for large chunk sets
- Batch chunk sends if needed (but protocol may require individual packets)

## Testing Strategy

1. **Initial Load Test**
   - Connect to server
   - Verify all chunks in 10x10 radius are loaded
   - Verify `Player.LoadedChunks` contains correct chunks

2. **Boundary Crossing Test**
   - Move player across chunk boundary
   - Verify new chunks are loaded
   - Verify old chunks are marked as unloaded
   - Verify center chunk is updated

3. **Edge Cases**
   - Player moves quickly across multiple chunk boundaries
   - Player moves diagonally (crosses both X and Z boundaries)
   - Player teleports (large position change)

## Implementation Order

1. ✅ Add `MarkChunkLoaded`/`MarkChunkUnloaded` to `Player`
2. ✅ Add `GetChunksToLoad`/`GetChunksToUnload` helpers to `Player`
3. ✅ Implement `UpdatePlayerChunksAsync` in `PlayHandler`
4. ✅ Update `SendInitialChunksAsync` to use new method
5. ✅ Update position handlers to trigger chunk updates
6. ✅ Test initial chunk loading
7. ✅ Test boundary crossing chunk updates

## Notes

- **Chunk Unloading**: Minecraft clients typically handle chunk unloading automatically when chunks are outside view distance. We mainly need to track it server-side for our own state management.
- **Center Chunk**: Should be updated after loading new chunks to ensure client's chunk loading is centered correctly.
- **View Distance**: Currently hardcoded to 10, but `Player.ViewDistance` property exists for future flexibility.
- **Circular vs Square**: Current `ChunkManager` uses circular distance. For a true 10x10 square, we'd need to adjust the logic, but circular is more efficient and matches Minecraft's behavior.

