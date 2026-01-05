using MineSharp.Core;
using MineSharp.Core.Protocol;
using MineSharp.Core.Protocol.PacketTypes;
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
        
        // Send Login (play) packet
        await SendLoginPlayPacketAsync(connection);
        
        // Send Synchronize Player Position (spawn at 0, 65, 0)
        await SendSynchronizePlayerPositionAsync(connection, 0.0, 65.0, 0.0, 0.0f, 0.0f, 0, 0);
        
        // Send Update Time
        await SendUpdateTimeAsync(connection, 0, 6000, true);
        
        // Send Game Event (event 13: "Start waiting for level chunks")
        await SendGameEventAsync(connection, 13, 0.0f);
        
        // Send Set Center Chunk (spawn chunk at 0, 0)
        await SendSetCenterChunkAsync(connection, 0, 0);
        
        // Send initial chunks around spawn (0, 0)
        if (_world != null)
        {
            await SendInitialChunksAsync(connection);
        }
        
        Console.WriteLine("  └─");
    }

    public async Task SendInitialChunksAsync(ClientConnection connection)
    {
        if (_world == null) return;
        
        Console.WriteLine("  │  → Sending initial chunks around spawn...");
        
        // Get chunks in range around spawn (chunk 0, 0)
        var chunksToLoad = _world.ChunkManager.GetChunksInRange(0, 0);
        
        Console.WriteLine($"  │  → Loading {chunksToLoad.Count} chunks...");
        
        int chunksSent = 0;
        foreach (var (chunkX, chunkZ) in chunksToLoad)
        {
            try
            {
                await SendChunkDataAsync(connection, chunkX, chunkZ);
                chunksSent++;
                
                if (chunksSent <= 10 || chunksSent % 10 == 0)
                {
                    Console.WriteLine($"  │  ✓ Chunk ({chunkX}, {chunkZ}) sent ({chunksSent}/{chunksToLoad.Count})");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  │  ✗ Error sending chunk ({chunkX}, {chunkZ}): {ex.Message}");
            }
        }
        
        Console.WriteLine($"  │  ✓ Sent {chunksSent} chunks");
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

    public async Task SendLoginPlayPacketAsync(ClientConnection connection)
    {
        Console.WriteLine("  │  → Sending Login (play) packet...");
        
        try
        {
            var loginPlay = PacketBuilder.BuildLoginPlayPacket(
                entityId: 1,
                dimensionNames: new List<string> { "minecraft:overworld" },
                gameMode: 0, // Survival
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
        // TODO: Implement set player position handling
        throw new NotImplementedException();
    }

    public async Task HandleSetPlayerPositionAndRotationAsync(ClientConnection connection, SetPlayerPositionAndRotationPacket packet)
    {
        // TODO: Implement set player position and rotation handling
        throw new NotImplementedException();
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
}

