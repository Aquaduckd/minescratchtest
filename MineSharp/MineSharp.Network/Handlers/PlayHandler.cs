using MineSharp.Core;
using MineSharp.Core.Protocol;
using MineSharp.Core.Protocol.PacketTypes;
using MineSharp.Game;
using System;
using System.Collections.Generic;

namespace MineSharp.Network.Handlers;

/// <summary>
/// Handles packets in the Play state.
/// </summary>
public class PlayHandler
{
    private readonly MineSharp.World.World? _world;

    public PlayHandler(MineSharp.World.World? world = null)
    {
        _world = world;
    }
    public async Task SendInitialPlayPacketsAsync(ClientConnection connection)
    {
        Console.WriteLine("  │  → Sending initial PLAY state packets...");
        
        // Create player if not already created
        if (connection.Player == null && connection.PlayerUuid.HasValue)
        {
            var player = new MineSharp.Game.Player(connection.PlayerUuid.Value, viewDistance: 10);
            connection.Player = player;
            
            // Add player to world if available
            if (_world != null)
            {
                _world.AddPlayer(player);
                Console.WriteLine($"  │  ✓ Player created and added to world (UUID: {connection.PlayerUuid})");
            }
            else
            {
                Console.WriteLine($"  │  ✓ Player created (UUID: {connection.PlayerUuid})");
            }
        }
        
        // Send Login (play) packet
        await SendLoginPlayPacketAsync(connection);
        
        // Update player position to spawn position BEFORE loading chunks
        // This ensures chunk loading uses the correct position
        if (connection.Player != null)
        {
            var spawnPosition = new MineSharp.Core.DataTypes.Vector3(0.0f, 65.0f, 0.0f);
            connection.Player.UpdatePosition(spawnPosition);
        }
        
        // Create and initialize ChunkLoader
        if (_world != null && connection.Player != null)
        {
            var chunkLoader = new ChunkLoader(connection, _world, connection.Player, this);
            connection.ChunkLoader = chunkLoader;
            chunkLoader.StartLoading();
            Console.WriteLine("  │  ✓ ChunkLoader created and started");
        }
        
        // Send Update Time
        await SendUpdateTimeAsync(connection, 0, 6000, true);
        
        // Send Game Event (event 13: "Start waiting for level chunks")
        // This tells the client to wait for chunks before rendering
        await SendGameEventAsync(connection, 13, 0.0f);
        
        // Send Set Center Chunk (spawn chunk at 0, 0)
        await SendSetCenterChunkAsync(connection, 0, 0);
        
        // Send a 3x3 grid of chunks around spawn FIRST (9 chunks total)
        // This ensures immediate ground exists when player spawns
        if (connection.ChunkLoader != null && connection.Player != null)
        {
            await SendSpawnChunksAsync(connection, connection.Player);
        }
        
        // Send Synchronize Player Position (spawn at 0, 65, 0) AFTER spawn chunks are loaded
        // This ensures the player spawns on solid ground without waiting for all chunks
        await SendSynchronizePlayerPositionAsync(connection, 0.0, 65.0, 0.0, 0.0f, 0.0f, 0, 0);
        
        // Now load the rest of the chunks in the view distance using ChunkLoader
        if (connection.ChunkLoader != null && connection.Player != null && _world != null)
        {
            // Calculate desired chunks for full view distance
            var chunkManager = _world.ChunkManager;
            var desiredChunks = new HashSet<(int X, int Z)>(chunkManager.GetChunksInRange(connection.Player.ChunkX, connection.Player.ChunkZ));
            
            // Update ChunkLoader with desired chunks (will load remaining chunks in background)
            connection.ChunkLoader.UpdateDesiredChunks(desiredChunks);
            Console.WriteLine($"  │  → ChunkLoader updated with {desiredChunks.Count} desired chunks");
        }
        
        Console.WriteLine("  └─");
    }

    /// <summary>
    /// Sends a 3x3 grid of chunks around spawn for immediate ground rendering.
    /// Uses ChunkLoader to load chunks synchronously for immediate spawn.
    /// </summary>
    public async Task SendSpawnChunksAsync(ClientConnection connection, Player player)
    {
        if (connection.ChunkLoader == null || _world == null) return;
        
        Console.WriteLine("  │  → Sending spawn chunks (3x3 grid)...");
        
        const int spawnChunkX = 0;
        const int spawnChunkZ = 0;
        const int radius = 1; // 3x3 grid: -1 to +1 = 3 chunks per axis
        
        // Collect all chunks for 3x3 grid
        var spawnChunks = new HashSet<(int X, int Z)>();
        for (int x = -radius; x <= radius; x++)
        {
            for (int z = -radius; z <= radius; z++)
            {
                spawnChunks.Add((spawnChunkX + x, spawnChunkZ + z));
            }
        }
        
        // Update ChunkLoader with spawn chunks (will start loading them)
        connection.ChunkLoader.UpdateDesiredChunks(spawnChunks);
        
        // Wait for all spawn chunks to be loaded (synchronous wait for immediate spawn)
        int maxWaitTime = 5000; // 5 seconds max wait
        int waitInterval = 50; // Check every 50ms
        int waited = 0;
        
        while (waited < maxWaitTime)
        {
            bool allLoaded = true;
            foreach (var chunk in spawnChunks)
            {
                if (!connection.ChunkLoader.IsChunkLoaded(chunk.X, chunk.Z))
                {
                    allLoaded = false;
                    break;
                }
            }
            
            if (allLoaded)
            {
                break;
            }
            
            await Task.Delay(waitInterval);
            waited += waitInterval;
        }
        
        int loadedCount = 0;
        foreach (var chunk in spawnChunks)
        {
            if (connection.ChunkLoader.IsChunkLoaded(chunk.X, chunk.Z))
            {
                loadedCount++;
            }
        }
        
        Console.WriteLine($"  │  ✓ Spawn chunks loaded ({loadedCount}/{spawnChunks.Count} chunks)");
    }

    public async Task SendInitialChunksAsync(ClientConnection connection)
    {
        // This method is now handled by ChunkLoader.UpdateDesiredChunks()
        // Kept for backward compatibility but no longer needed
        if (connection.ChunkLoader == null || connection.Player == null || _world == null) return;
        
        Console.WriteLine("  │  → ChunkLoader is handling remaining chunks in background...");
        // ChunkLoader is already running and will load chunks as needed
    }

    public async Task SendChunkDataAsync(ClientConnection connection, int chunkX, int chunkZ)
    {
        if (_world == null) return;
        
        try
        {
            var chunkData = PacketBuilder.BuildChunkDataPacket(chunkX, chunkZ, _world.BlockManager);
            await connection.SendPacketAsync(chunkData);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  │  ✗ Error building/sending chunk ({chunkX}, {chunkZ}): {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Sends chunk data. Can be called by ChunkLoader.
    /// </summary>
    public async Task SendChunkDataForLoaderAsync(ClientConnection connection, int chunkX, int chunkZ)
    {
        await SendChunkDataAsync(connection, chunkX, chunkZ);
    }

    public async Task SendLoginPlayPacketAsync(ClientConnection connection)
    {
        Console.WriteLine("  │  → Sending Login (play) packet...");
        
        try
        {
            var loginPlay = PacketBuilder.BuildLoginPlayPacket(
                entityId: 1,
                dimensionNames: new List<string> { "minecraft:overworld" },
                gameMode: 1, // Creative (0=Survival, 1=Creative, 2=Adventure, 3=Spectator)
                dimensionName: "minecraft:overworld"
            );
            await connection.SendPacketAsync(loginPlay);
            Console.WriteLine($"  │  ✓ Login (play) sent ({loginPlay.Length} bytes)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  │  ✗ Error sending Login (play): {ex.Message}");
            throw;
        }
    }

    public async Task SendSynchronizePlayerPositionAsync(ClientConnection connection, double x, double y, double z, float yaw, float pitch, int flags, int teleportId)
    {
        Console.WriteLine($"  │  → Sending Synchronize Player Position...");
        
        try
        {
            var playerPos = PacketBuilder.BuildSynchronizePlayerPositionPacket(x, y, z, yaw, pitch, flags, teleportId);
            await connection.SendPacketAsync(playerPos);
            Console.WriteLine($"  │  ✓ Player Position sent ({playerPos.Length} bytes)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  │  ✗ Error sending Player Position: {ex.Message}");
            throw;
        }
    }

    public async Task SendUpdateTimeAsync(ClientConnection connection, long worldAge, long timeOfDay, bool timeIncreasing = true)
    {
        Console.WriteLine($"  │  → Sending Update Time...");
        
        try
        {
            var updateTime = PacketBuilder.BuildUpdateTimePacket(worldAge, timeOfDay, timeIncreasing);
            await connection.SendPacketAsync(updateTime);
            Console.WriteLine($"  │  ✓ Update Time sent ({updateTime.Length} bytes)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  │  ✗ Error sending Update Time: {ex.Message}");
            throw;
        }
    }

    public async Task SendGameEventAsync(ClientConnection connection, byte eventId, float value)
    {
        Console.WriteLine($"  │  → Sending Game Event (event {eventId})...");
        
        try
        {
            var gameEvent = PacketBuilder.BuildGameEventPacket(eventId, value);
            await connection.SendPacketAsync(gameEvent);
            Console.WriteLine($"  │  ✓ Game Event sent ({gameEvent.Length} bytes)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  │  ✗ Error sending Game Event: {ex.Message}");
            throw;
        }
    }

    public async Task SendSetCenterChunkAsync(ClientConnection connection, int chunkX, int chunkZ)
    {
        Console.WriteLine($"  │  → Sending Set Center Chunk ({chunkX}, {chunkZ})...");
        
        try
        {
            var setCenterChunk = PacketBuilder.BuildSetCenterChunkPacket(chunkX, chunkZ);
            await connection.SendPacketAsync(setCenterChunk);
            Console.WriteLine($"  │  ✓ Set Center Chunk sent ({setCenterChunk.Length} bytes)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  │  ✗ Error sending Set Center Chunk: {ex.Message}");
            throw;
        }
    }

    public async Task HandleSetPlayerPositionAsync(ClientConnection connection, SetPlayerPositionPacket packet)
    {
        Console.WriteLine($"  │  ← Received Set Player Position: ({packet.X:F2}, {packet.Y:F2}, {packet.Z:F2})");
        
        var player = connection.Player;
        if (player == null)
        {
            Console.WriteLine($"  │  ⚠ Warning: Received position update but player is not set");
            return;
        }
        
        // Update player position
        var newPosition = new MineSharp.Core.DataTypes.Vector3((float)packet.X, (float)packet.Y, (float)packet.Z);
        var chunkChange = player.UpdatePosition(newPosition);
        
        // Handle chunk boundary crossing
        if (chunkChange.HasValue)
        {
            var (oldChunkX, oldChunkZ, newChunkX, newChunkZ) = chunkChange.Value;
            Console.WriteLine($"  │  → Chunk boundary crossed: ({oldChunkX}, {oldChunkZ}) → ({newChunkX}, {newChunkZ})");
            
            // Update ChunkLoader with new desired chunks (non-blocking)
            if (connection.ChunkLoader != null && _world != null)
            {
                var chunkManager = _world.ChunkManager;
                var desiredChunks = new HashSet<(int X, int Z)>(chunkManager.GetChunksInRange(newChunkX, newChunkZ));
                connection.ChunkLoader.UpdateDesiredChunks(desiredChunks);
                
                // Update center chunk
                await SendSetCenterChunkAsync(connection, newChunkX, newChunkZ);
            }
        }
        
        await Task.CompletedTask;
    }

    public async Task HandleSetPlayerPositionAndRotationAsync(ClientConnection connection, SetPlayerPositionAndRotationPacket packet)
    {
        Console.WriteLine($"  │  ← Received Set Player Position and Rotation: ({packet.X:F2}, {packet.Y:F2}, {packet.Z:F2}), Yaw: {packet.Yaw:F2}, Pitch: {packet.Pitch:F2}");
        
        var player = connection.Player;
        if (player == null)
        {
            Console.WriteLine($"  │  ⚠ Warning: Received position/rotation update but player is not set");
            return;
        }
        
        // Update player position
        var newPosition = new MineSharp.Core.DataTypes.Vector3((float)packet.X, (float)packet.Y, (float)packet.Z);
        var chunkChange = player.UpdatePosition(newPosition);
        
        // Update player rotation
        player.UpdateRotation(packet.Yaw, packet.Pitch);
        
        // Handle chunk boundary crossing
        if (chunkChange.HasValue)
        {
            var (oldChunkX, oldChunkZ, newChunkX, newChunkZ) = chunkChange.Value;
            Console.WriteLine($"  │  → Chunk boundary crossed: ({oldChunkX}, {oldChunkZ}) → ({newChunkX}, {newChunkZ})");
            
            // Update ChunkLoader with new desired chunks (non-blocking)
            if (connection.ChunkLoader != null && _world != null)
            {
                var chunkManager = _world.ChunkManager;
                var desiredChunks = new HashSet<(int X, int Z)>(chunkManager.GetChunksInRange(newChunkX, newChunkZ));
                connection.ChunkLoader.UpdateDesiredChunks(desiredChunks);
                
                // Update center chunk
                await SendSetCenterChunkAsync(connection, newChunkX, newChunkZ);
            }
        }
        
        await Task.CompletedTask;
    }

    public async Task HandleSetPlayerRotationAsync(ClientConnection connection, SetPlayerRotationPacket packet)
    {
        // TODO: Implement set player rotation handling
        throw new NotImplementedException();
    }

    public async Task HandleKeepAliveAsync(ClientConnection connection, KeepAlivePacket packet)
    {
        // Verify the keep alive ID matches what we sent
        if (connection.LastKeepAliveId.HasValue && connection.LastKeepAliveId.Value == packet.KeepAliveId)
        {
            Console.WriteLine($"  │  ✓ Keep Alive response received (ID: {packet.KeepAliveId})");
            // Client is still connected and responding
        }
        else
        {
            Console.WriteLine($"  │  ⚠ Keep Alive response ID mismatch: expected {connection.LastKeepAliveId}, got {packet.KeepAliveId}");
            // This could indicate a timing issue or client problem, but we'll continue
        }
    }

    public async Task HandlePlayerActionAsync(ClientConnection connection, PlayerActionPacket packet)
    {
        // TODO: Implement player action handling
        throw new NotImplementedException();
    }

    public async Task SendChunkDataAsync(ClientConnection connection, int chunkX, int chunkZ, byte[] chunkData, bool fullChunk)
    {
        // TODO: Implement chunk data sending
        throw new NotImplementedException();
    }

    public async Task SendUpdateLightAsync(ClientConnection connection, int chunkX, int chunkZ, object lightData)
    {
        // TODO: Implement update light sending
        throw new NotImplementedException();
    }

    public async Task SendKeepAliveAsync(ClientConnection connection, long keepAliveId)
    {
        Console.WriteLine($"  │  → Sending Keep Alive (ID: {keepAliveId})...");
        try
        {
            var keepAlivePacket = PacketBuilder.BuildKeepAlivePacket(keepAliveId);
            await connection.SendPacketAsync(keepAlivePacket);
            Console.WriteLine($"  │  ✓ Keep Alive sent ({keepAlivePacket.Length} bytes)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  │  ✗ Error sending Keep Alive: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Updates player's loaded chunks by loading new chunks and unloading distant ones.
    /// Maintains a square region of chunks around the player based on view distance.
    /// 
    /// DEPRECATED: This method is replaced by ChunkLoader. Use ChunkLoader.UpdateDesiredChunks() instead.
    /// </summary>
    [Obsolete("Use ChunkLoader.UpdateDesiredChunks() instead")]
    public async Task UpdatePlayerChunksAsync(ClientConnection connection, Player player)
    {
        if (_world == null) return;
        
        var chunkManager = _world.ChunkManager;
        
        // Capture player's chunk position at the start to ensure consistency
        // This prevents issues where the player moves during chunk loading
        int targetChunkX = player.ChunkX;
        int targetChunkZ = player.ChunkZ;
        
        // Get chunks that need to be loaded based on captured position
        // We'll re-check loaded chunks dynamically during the loop to avoid stale data
        var chunksToLoad = chunkManager.GetChunksToLoad(player.LoadedChunks, targetChunkX, targetChunkZ);
        
        // Get chunks that should be unloaded based on captured position
        var chunksToUnload = chunkManager.GetChunksToUnload(player.LoadedChunks, targetChunkX, targetChunkZ);
        
        // Load new chunks
        if (chunksToLoad.Count > 0)
        {
            Console.WriteLine($"  │  → Loading {chunksToLoad.Count} new chunk(s)...");
            
            // Sort chunks by Manhattan distance from target chunk (closest first)
            // This ensures chunks near the target position load first for better UX
            chunksToLoad.Sort((a, b) =>
            {
                int distA = Math.Abs(a.X - targetChunkX) + Math.Abs(a.Z - targetChunkZ);
                int distB = Math.Abs(b.X - targetChunkX) + Math.Abs(b.Z - targetChunkZ);
                return distA.CompareTo(distB);
            });
            
            foreach (var (chunkX, chunkZ) in chunksToLoad)
            {
                // Re-check loaded chunks dynamically (another task might have loaded this)
                // This prevents race conditions when multiple chunk loading tasks run simultaneously
                if (player.IsChunkLoaded(chunkX, chunkZ))
                {
                    continue; // Already loaded by another task, skip
                }
                
                // Check if another task is already loading this chunk
                if (player.IsChunkLoading(chunkX, chunkZ))
                {
                    continue; // Another task is loading it, skip
                }
                
                // Mark as loading BEFORE sending (prevents duplicate sends)
                if (!player.MarkChunkLoading(chunkX, chunkZ))
                {
                    continue; // Another task started loading it, skip
                }
                
                // Verify chunk is still needed (player might have moved)
                // Only load chunks that are still in range of the target position
                var chunksInRange = chunkManager.GetChunksInRange(targetChunkX, targetChunkZ);
                if (!chunksInRange.Contains((chunkX, chunkZ)))
                {
                    // Player moved, this chunk is no longer needed
                    player.MarkChunkLoadingFailed(chunkX, chunkZ);
                    continue;
                }
                
                try
                {
                    // Send chunk data - this will throw if connection is broken or send fails
                    await SendChunkDataAsync(connection, chunkX, chunkZ);
                    
                    // CRITICAL: Before marking as loaded, verify chunk is still needed for player's CURRENT position
                    // Player might have moved during the send, making this chunk no longer relevant
                    int currentChunkX = player.ChunkX;
                    int currentChunkZ = player.ChunkZ;
                    var currentChunksInRange = chunkManager.GetChunksInRange(currentChunkX, currentChunkZ);
                    
                    if (!currentChunksInRange.Contains((chunkX, chunkZ)))
                    {
                        // Player moved - this chunk is no longer in range of current position
                        // Don't mark as loaded, let it be retried for the new position
                        player.MarkChunkLoadingFailed(chunkX, chunkZ);
                        Console.WriteLine($"  │  → Chunk ({chunkX}, {chunkZ}) sent but player moved from ({targetChunkX}, {targetChunkZ}) to ({currentChunkX}, {currentChunkZ}) - not marking as loaded");
                        continue;
                    }
                    
                    // Only mark as loaded AFTER successful send AND verification it's still needed
                    // MarkChunkLoaded will remove it from loading set and add to loaded set
                    if (player.MarkChunkLoaded(chunkX, chunkZ))
                    {
                        // Successfully marked as loaded
                    }
                    else
                    {
                        // Another task loaded it between our check and mark - that's fine
                        Console.WriteLine($"  │  → Chunk ({chunkX}, {chunkZ}) was loaded by another task during send");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  │  ✗ Error loading chunk ({chunkX}, {chunkZ}): {ex.Message}");
                    // Mark loading as failed so it can be retried
                    player.MarkChunkLoadingFailed(chunkX, chunkZ);
                    // Don't mark as loaded if send failed - will retry on next update
                }
            }
            
            // Update center chunk to target chunk (the position we were loading for)
            await SendSetCenterChunkAsync(connection, targetChunkX, targetChunkZ);
        }
        
        // Unload distant chunks
        if (chunksToUnload.Count > 0)
        {
            Console.WriteLine($"  │  → Unloading {chunksToUnload.Count} distant chunk(s)...");
            
            foreach (var (chunkX, chunkZ) in chunksToUnload)
            {
                player.MarkChunkUnloaded(chunkX, chunkZ);
                // Note: Minecraft client automatically unloads chunks outside view distance
                // We track it server-side for our own state management
            }
        }
    }
}

