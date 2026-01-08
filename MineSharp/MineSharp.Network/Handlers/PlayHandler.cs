using MineSharp.Core;
using MineSharp.Core.DataTypes;
using MineSharp.Core.Protocol;
using MineSharp.Core.Protocol.PacketTypes;
using MineSharp.Data;
using MineSharp.Game;
using MineSharp.Network;
using MineSharp.World;
using MineSharp.World.ChunkDiffs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MineSharp.Network.Handlers;

/// <summary>
/// Handles packets in the Play state.
/// </summary>
public class PlayHandler
{
    private readonly MineSharp.World.World? _world;
    private readonly PlayerVisibilityManager? _visibilityManager;
    private readonly RegistryManager? _registryManager;
    private readonly Func<IEnumerable<ClientConnection>>? _getAllConnections;

    public PlayHandler(MineSharp.World.World? world = null, Func<IEnumerable<ClientConnection>>? getAllConnections = null, RegistryManager? registryManager = null)
    {
        _world = world;
        _registryManager = registryManager;
        _getAllConnections = getAllConnections;
        
        // Create PlayerVisibilityManager if world and connection getter are available
        if (_world != null && getAllConnections != null)
        {
            // Look up player entity type ID from registry
            int playerEntityTypeId = 151; // Default fallback (protocol_id for minecraft:player in entity_type registry)
            if (registryManager != null)
            {
                var protocolId = registryManager.GetRegistryEntryProtocolId("minecraft:entity_type", "minecraft:player");
                if (protocolId.HasValue)
                {
                    playerEntityTypeId = protocolId.Value;
                }
            }
            
            _visibilityManager = new PlayerVisibilityManager(
                world: _world,
                playHandler: this,
                getAllConnections: getAllConnections,
                viewDistanceBlocks: 48.0, // 48 blocks = 3 chunks (default Minecraft view distance for entities)
                playerEntityTypeId: playerEntityTypeId);
        }
    }
    public async Task SendInitialPlayPacketsAsync(ClientConnection connection)
    {
        Console.WriteLine("  │  → Sending initial PLAY state packets...");
        
        // Get or create player
        bool isReconnection = false;
        if (connection.Player == null && connection.PlayerUuid.HasValue)
        {
            Player? player = null;
            
            // Check if player already exists (for reconnection - preserves inventory)
            if (_world != null)
            {
                player = _world.GetPlayer(connection.PlayerUuid.Value);
                if (player != null)
                {
                    Console.WriteLine($"  │  → Found existing player (UUID: {connection.PlayerUuid}, EntityId: {player.EntityId})");
                    isReconnection = true;
                }
                else
                {
                    Console.WriteLine($"  │  → No existing player found (UUID: {connection.PlayerUuid}) - creating new player");
                }
            }
            
            if (player == null)
            {
                // Player doesn't exist, create a new one
                // Get entity ID from world's entity manager
                int entityId = 1; // Default fallback
                if (_world != null)
                {
                    entityId = _world.EntityManager.GetNextPlayerEntityId();
                }
                
                player = new MineSharp.Game.Player(connection.PlayerUuid.Value, entityId, viewDistance: 10);
                
                // Add player to world if available
                if (_world != null)
                {
                    _world.AddPlayer(player);
                    // Note: Players are tracked in PlayerManager, not EntityManager
                    // EntityManager is for non-player entities
                    Console.WriteLine($"  │  ✓ Player created and added to world (UUID: {connection.PlayerUuid}, EntityId: {entityId})");
                }
                else
                {
                    Console.WriteLine($"  │  ✓ Player created (UUID: {connection.PlayerUuid}, EntityId: {entityId})");
                }
            }
            else
            {
                // Player exists, reuse it (preserves inventory and other state)
                // Note: Entity ID might be different for this session, but that's okay
                // The player object itself persists with its inventory
                int newEntityId = 1;
                if (_world != null)
                {
                    newEntityId = _world.EntityManager.GetNextPlayerEntityId();
                }
                Console.WriteLine($"  │  ✓ Player reconnected (UUID: {connection.PlayerUuid}, Old EntityId: {player.EntityId}, New EntityId: {newEntityId}) - inventory preserved");
                
                // Log inventory state for debugging
                var inventory = player.Inventory;
                int itemCount = 0;
                for (int i = 0; i < Inventory.TOTAL_SLOTS; i++)
                {
                    var slot = inventory.GetSlot(i);
                    if (slot != null && !slot.IsEmpty)
                    {
                        itemCount++;
                    }
                }
                Console.WriteLine($"  │  → Inventory has {itemCount} non-empty slot(s)");
            }
            
            if (player != null)
            {
                connection.Player = player;
            }
        }
        
        // Send Login (play) packet
        await SendLoginPlayPacketAsync(connection);
        
        // If player reconnected, send their inventory to the client
        // The client needs to know about the inventory state
        if (isReconnection && connection.Player != null)
        {
            Console.WriteLine($"  │  → Sending inventory to reconnected player...");
            await SendContainerContentAsync(connection, connection.Player.Inventory);
        }
        
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
        
        // Send Player Info Update packets (steps 33/34 in FAQ)
        // Step 33: All existing players to new player
        // Step 34: New player to all existing players
        if (_visibilityManager != null && connection.Player != null)
        {
            await _visibilityManager.SendPlayerInfoUpdatesAsync(connection, connection.Player);
        }
        
        // Notify visibility manager of new player (for entity spawning based on view distance)
        if (_visibilityManager != null && connection.Player != null)
        {
            await _visibilityManager.OnPlayerJoinedAsync(connection, connection.Player);
        }
        
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
        
        // Force immediate processing (bypass debounce for spawn chunks)
        connection.ChunkLoader.ProcessUpdatesImmediately();
        
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
            // Get the player's actual entity ID
            int entityId = 1; // Default fallback
            if (connection.Player != null)
            {
                entityId = connection.Player.EntityId;
            }
            
            byte gameMode = 1; // Creative (0=Survival, 1=Creative, 2=Adventure, 3=Spectator)
            var loginPlay = PacketBuilder.BuildLoginPlayPacket(
                entityId: entityId,
                dimensionNames: new List<string> { "minecraft:overworld" },
                gameMode: gameMode,
                dimensionName: "minecraft:overworld"
            );
            
            // Store game mode on player
            if (connection.Player != null)
            {
                connection.Player.GameMode = gameMode;
            }
            
            await connection.SendPacketAsync(loginPlay);
            Console.WriteLine($"  │  ✓ Login (play) sent (EntityId: {entityId}, GameMode: {gameMode}, {loginPlay.Length} bytes)");
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
        
        // Notify visibility manager of player movement
        if (_visibilityManager != null && player != null)
        {
            await _visibilityManager.OnPlayerMovedAsync(player);
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
        
        // Notify visibility manager of player movement
        if (_visibilityManager != null && player != null)
        {
            await _visibilityManager.OnPlayerMovedAsync(player);
        }
        
        await Task.CompletedTask;
    }

    public async Task HandleSetPlayerRotationAsync(ClientConnection connection, SetPlayerRotationPacket packet)
    {
        Console.WriteLine($"  │  ← Received Set Player Rotation: Yaw: {packet.Yaw:F2}, Pitch: {packet.Pitch:F2}");
        
        var player = connection.Player;
        if (player == null)
        {
            Console.WriteLine($"  │  ⚠ Warning: Received rotation update but player is not set");
            return;
        }
        
        // Update player rotation
        player.UpdateRotation(packet.Yaw, packet.Pitch);
        
        // Notify visibility manager of rotation change (rotation-only update)
        // This will also check and broadcast head yaw updates
        if (_visibilityManager != null && player != null)
        {
            await _visibilityManager.BroadcastRotationUpdateAsync(player);
        }
        
        await Task.CompletedTask;
    }

    /// <summary>
    /// Called when a player disconnects.
    /// Notifies the visibility manager to despawn the player for all other players.
    /// </summary>
    public async Task OnPlayerDisconnectedAsync(Player disconnectedPlayer)
    {
        if (_visibilityManager != null && disconnectedPlayer != null)
        {
            await _visibilityManager.OnPlayerDisconnectedAsync(disconnectedPlayer);
        }
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
        var location = packet.Location;
        int blockX = location.X;
        int blockY = location.Y;
        int blockZ = location.Z;
        
        // Log all player actions for debugging
        string statusName = packet.Status switch
        {
            0 => "Started digging",
            1 => "Cancelled digging",
            2 => "Finished digging",
            _ => $"Unknown ({packet.Status})"
        };
        
        Console.WriteLine($"  │  ← Player Action: {statusName} at ({blockX}, {blockY}, {blockZ}), Face: {packet.Face}, Sequence: {packet.Sequence}");
        
        var player = connection.Player;
        if (player == null)
        {
            Console.WriteLine($"  │  ⚠ Warning: Received player action but player is not set");
            return;
        }

        int entityId = player.EntityId;

        // Handle different statuses
        if (packet.Status == 0) // Started digging
        {
            // In creative mode, blocks break instantly, so we break immediately
            // In survival mode, we show the animation start
            bool isCreative = player.GameMode == 1; // 1 = Creative
            
            if (isCreative)
            {
                Console.WriteLine($"  │  → Creative mode: Block broken instantly! Recording change...");
                
                // Record the block change (set to air) in chunk diff system
                ChunkDiffManager.Instance.RecordBlockChange(blockX, blockY, blockZ, 0); // 0 = air
                
                // Update the block in BlockManager if world is available
                if (_world != null)
                {
                    _world.BlockManager.SetBlock(blockX, blockY, blockZ, Block.Air());
                    
                    // Clear any destroy stage animation (stage 10+ removes it)
                    await BroadcastBlockDestroyStageAsync(blockX, blockY, blockZ, entityId, 10);
                    
                    // Broadcast block change to all players with this chunk loaded
                    await BroadcastBlockChangeAsync(blockX, blockY, blockZ, 0);
                }
                
                Console.WriteLine($"  │  ✓ Block change recorded and applied");
            }
            else
            {
                // Survival mode: Show animation start (stage 0)
                Console.WriteLine($"  │  → Survival mode: Starting block break animation...");
                await BroadcastBlockDestroyStageAsync(blockX, blockY, blockZ, entityId, 0);
            }
        }
        else if (packet.Status == 1) // Cancelled digging
        {
            // Clear the destroy stage animation
            Console.WriteLine($"  │  → Cancelled digging: Clearing animation...");
            await BroadcastBlockDestroyStageAsync(blockX, blockY, blockZ, entityId, 10);
        }
        else if (packet.Status == 2) // Finished digging
        {
            // Survival mode: Block is broken
            Console.WriteLine($"  │  → Survival mode: Block broken! Recording change...");
            
            // Record the block change (set to air) in chunk diff system
            ChunkDiffManager.Instance.RecordBlockChange(blockX, blockY, blockZ, 0); // 0 = air
            
            // Update the block in BlockManager if world is available
            if (_world != null)
            {
                _world.BlockManager.SetBlock(blockX, blockY, blockZ, Block.Air());
                
                // Clear the destroy stage animation (stage 10+ removes it)
                await BroadcastBlockDestroyStageAsync(blockX, blockY, blockZ, entityId, 10);
                
                // Broadcast block change to all players with this chunk loaded
                await BroadcastBlockChangeAsync(blockX, blockY, blockZ, 0);
            }
            
            Console.WriteLine($"  │  ✓ Block change recorded and applied");
        }
    }

    public async Task HandleUseItemOnAsync(ClientConnection connection, UseItemOnPacket packet)
    {
        var clickedLocation = packet.Location;
        int clickedX = clickedLocation.X;
        int clickedY = clickedLocation.Y;
        int clickedZ = clickedLocation.Z;
        
        // Calculate the placement position (one block away from the clicked face)
        int placementX = clickedX;
        int placementY = clickedY;
        int placementZ = clickedZ;
        
        // Face: 0=Bottom, 1=Top, 2=North, 3=South, 4=West, 5=East
        // When clicking on a face, we place the block on the opposite side of that face
        switch (packet.Face)
        {
            case 0: // Bottom face - place block below (y-1)
                placementY = clickedY - 1;
                break;
            case 1: // Top face - place block above (y+1)
                placementY = clickedY + 1;
                break;
            case 2: // North face (Z-) - place block north (z-1)
                placementZ = clickedZ - 1;
                break;
            case 3: // South face (Z+) - place block south (z+1)
                placementZ = clickedZ + 1;
                break;
            case 4: // West face (X-) - place block west (x-1)
                placementX = clickedX - 1;
                break;
            case 5: // East face (X+) - place block east (x+1)
                placementX = clickedX + 1;
                break;
        }
        
        string faceName = packet.Face switch
        {
            0 => "Bottom",
            1 => "Top",
            2 => "North",
            3 => "South",
            4 => "West",
            5 => "East",
            _ => $"Unknown ({packet.Face})"
        };
        
        string handName = packet.Hand == 0 ? "Main Hand" : "Off Hand";
        
        Console.WriteLine($"  │  ← Use Item On: Clicked at ({clickedX}, {clickedY}, {clickedZ}) on {faceName} face, Hand: {handName}");
        Console.WriteLine($"  │  → Placing block at ({placementX}, {placementY}, {placementZ})");
        
        // Get the actual block from player's inventory based on hand
        var player = connection.Player;
        int blockStateId = 0; // Default to air
        
        if (player != null)
        {
            var inventory = player.Inventory;
            ItemStack? heldItem = null;
            
            // Get item from the hand specified in packet
            if (packet.Hand == 0) // Main hand
            {
                heldItem = inventory.GetHeldItem();
            }
            else if (packet.Hand == 1) // Off hand
            {
                // Off hand is slot 40
                heldItem = inventory.GetSlot(40);
            }
            
            if (heldItem != null && !heldItem.IsEmpty)
            {
                // Map item ID (item protocol ID) to proper block state ID using registry
                int itemProtocolId = heldItem.ItemId;
                int? resolvedBlockStateId = _registryManager?.ResolveBlockStateIdForItemProtocolId(itemProtocolId);
                if (resolvedBlockStateId.HasValue)
                {
                    blockStateId = resolvedBlockStateId.Value;
                }
                else
                {
                    Console.WriteLine($"  │  ⚠ Item ID {itemProtocolId} does not map to a placeable block");
                    return;
                }
                
                // Reduce item count only in survival mode (game mode 0)
                // In creative mode (1), adventure mode (2), and spectator mode (3), items are not consumed
                if (player.GameMode == 0) // Survival mode
                {
                    if (heldItem.Count > 1)
                    {
                        var newHeldItem = new ItemStack(heldItem.ItemId, (byte)(heldItem.Count - 1), heldItem.Nbt, heldItem.Damage);
                        if (packet.Hand == 0)
                        {
                            int hotbarSlot = inventory.SelectedHotbarSlotIndex;
                            inventory.SetSlot(hotbarSlot, newHeldItem);
                        }
                        else
                        {
                            inventory.SetSlot(40, newHeldItem);
                        }
                    }
                    else
                    {
                        // Remove item from slot (count was 1)
                        if (packet.Hand == 0)
                        {
                            int hotbarSlot = inventory.SelectedHotbarSlotIndex;
                            inventory.SetSlot(hotbarSlot, null);
                        }
                        else
                        {
                            inventory.SetSlot(40, null);
                        }
                    }
                    
                    // Send updated slot to client
                    var updatedSlotData = (heldItem.Count > 1 
                        ? new ItemStack(heldItem.ItemId, (byte)(heldItem.Count - 1), heldItem.Nbt, heldItem.Damage)
                        : new ItemStack()).ToSlotData();
                    int slotIndex = packet.Hand == 0 
                        ? Inventory.HotbarSlotToIndex(inventory.SelectedHotbarSlotIndex)
                        : 40; // Off-hand slot (TODO: Add constant to Inventory class)
                    await SendContainerSlotAsync(connection, 0, inventory.InventoryStateId, (short)slotIndex, updatedSlotData);
                }
                // In creative/adventure/spectator mode, don't modify inventory
                
                Console.WriteLine($"  │  → Using item ID {blockStateId} from {(packet.Hand == 0 ? "main hand" : "off hand")} (GameMode: {player.GameMode}, {(player.GameMode == 0 ? "consuming item" : "not consuming")})");
            }
            else
            {
                Console.WriteLine($"  │  ⚠ No item in {(packet.Hand == 0 ? "main hand" : "off hand")}, cannot place block");
                return; // Can't place without an item
            }
        }
        
        if (blockStateId == 0)
        {
            Console.WriteLine($"  │  ⚠ Block state ID is 0 (air), cannot place");
            return; // Can't place air
        }
        
        // TODO: Validate placement (check if position is valid, not inside player, etc.)
        // TODO: Check if player has the block in inventory (for survival mode)
        
        // Record the block change in chunk diff system
        ChunkDiffManager.Instance.RecordBlockChange(placementX, placementY, placementZ, blockStateId);
        
        // Update the block in BlockManager if world is available
        if (_world != null)
        {
            var block = new Block(blockStateId);
            _world.BlockManager.SetBlock(placementX, placementY, placementZ, block);
            
            // Broadcast block change to all players with this chunk loaded
            await BroadcastBlockChangeAsync(placementX, placementY, placementZ, blockStateId);
        }
        
        Console.WriteLine($"  │  ✓ Block placed (block state ID: {blockStateId})");
    }

    public async Task HandleClickContainerButtonAsync(ClientConnection connection, ClickContainerButtonPacket packet)
    {
        // TODO: Implement container button click handling
        Console.WriteLine($"  │  ← Click Container Button: Window ID {packet.WindowId}, Button ID {packet.ButtonId}");
        
        var player = connection.Player;
        if (player == null)
        {
            Console.WriteLine($"  │  ⚠ Player not found for connection");
            return;
        }
        
        // TODO: Handle button clicks (brewing stand, etc.)
        // For now, just log the event
        Console.WriteLine($"  │  → Button click not yet implemented");
    }

    public async Task HandleClickContainerAsync(ClientConnection connection, ClickContainerPacket packet)
    {
        // Check if this is a creative menu click (Window ID != 0)
        string windowType = packet.WindowId == 0 ? "Player Inventory" : $"Creative Menu (Window {packet.WindowId})";
        string carriedInfo = packet.CarriedItem.Present 
            ? $"carried: item ID {packet.CarriedItem.ItemId}, count {packet.CarriedItem.ItemCount}" 
            : "carried: empty";
        
        Console.WriteLine($"  │  ← Click Container (0x11): {windowType}, Slot {packet.Slot}, Button {packet.Button}, Mode {packet.Mode}, {carriedInfo}");
        
        // If creative menu (Window ID != 0), log special handling
        if (packet.WindowId != 0)
        {
            if (packet.CarriedItem.Present && !packet.CarriedItem.IsEmpty)
            {
                Console.WriteLine($"  │  → Creative Menu: Adding item ID {packet.CarriedItem.ItemId}, count {packet.CarriedItem.ItemCount} to cursor");
            }
            else if (!packet.CarriedItem.Present)
            {
                // Check what was previously in cursor to see what's being removed
                var previousCursor = connection.Player?.Inventory?.CursorItem;
                if (previousCursor != null && !previousCursor.IsEmpty)
                {
                    Console.WriteLine($"  │  → Creative Menu: REMOVING item ID {previousCursor.ItemId}, count {previousCursor.Count} from cursor");
                }
                else
                {
                    Console.WriteLine($"  │  → Creative Menu: Clearing cursor (was already empty)");
                }
            }
        }
        
        var player = connection.Player;
        if (player == null)
        {
            Console.WriteLine($"  │  ⚠ Player not found for connection");
            return;
        }
        
        var inventory = player.Inventory;
        
        // Validate state ID
        if (packet.StateId != inventory.InventoryStateId)
        {
            Console.WriteLine($"  │  ⚠ State ID mismatch: expected {inventory.InventoryStateId}, got {packet.StateId}");
            // Send container content to resync
            await SendContainerContentAsync(connection, inventory);
            return;
        }
        
        // Handle click based on mode
        bool inventoryChanged = false;
        
        switch (packet.Mode)
        {
            case 0: // Click (left/right mouse button)
                inventoryChanged = await HandleClickMode(connection, inventory, packet);
                break;
            case 1: // Shift+Click
                inventoryChanged = await HandleShiftClickMode(connection, inventory, packet);
                break;
            case 2: // Number Key
                inventoryChanged = await HandleNumberKeyMode(connection, inventory, packet);
                break;
            case 3: // Middle Click (creative mode only)
                // TODO: Implement middle click (pick block in creative)
                Console.WriteLine($"  │  → Middle click not yet implemented");
                break;
            case 4: // Drop
                inventoryChanged = await HandleDropMode(connection, inventory, packet);
                break;
            case 5: // Drag
                // TODO: Implement drag mode
                Console.WriteLine($"  │  → Drag mode not yet implemented");
                break;
            case 6: // Double Click
                inventoryChanged = await HandleDoubleClickMode(connection, inventory, packet);
                break;
            default:
                Console.WriteLine($"  │  ⚠ Unknown click mode: {packet.Mode}");
                break;
        }
        
        if (inventoryChanged)
        {
            // Send updated slot(s) to client
            // For now, send the carried item update
            var cursorItem = inventory.CursorItem;
            var cursorSlotData = cursorItem?.ToSlotData() ?? SlotData.Empty;
            
            // Send slot updates for changed slots
            foreach (var (slotIndex, slotData) in packet.Slots)
            {
                if (slotIndex >= 0 && slotIndex < Inventory.TOTAL_SLOTS)
                {
                    var slot = inventory.GetSlot((int)slotIndex);
                    var updatedSlotData = slot?.ToSlotData() ?? SlotData.Empty;
                    await SendContainerSlotAsync(connection, packet.WindowId, inventory.InventoryStateId, slotIndex, updatedSlotData);
                }
            }
            
            // Send cursor item update (slot -1)
            await SendContainerSlotAsync(connection, packet.WindowId, inventory.InventoryStateId, -1, cursorSlotData);
        }
    }
    
    private async Task<bool> HandleClickMode(ClientConnection connection, Inventory inventory, ClickContainerPacket packet)
    {
        // Mode 0: Click (left/right mouse button)
        // Button 0 = Left, 1 = Right, 2 = Middle
        // Left click: Pick up item or swap with cursor
        // Right click: Pick up half stack or place one item
        
        int slotIndex = packet.Slot;
        var cursorItem = inventory.CursorItem;
        var slotItem = slotIndex >= 0 && slotIndex < Inventory.TOTAL_SLOTS ? inventory.GetSlot(slotIndex) : null;
        
        if (packet.Button == 0) // Left click
        {
            // Swap cursor and slot
            if (slotItem != null)
            {
                inventory.SetCursorItem(slotItem);
            }
            else
            {
                inventory.ClearCursorItem();
            }
            
            if (cursorItem != null && !cursorItem.IsEmpty)
            {
                inventory.SetSlot(slotIndex, cursorItem);
            }
            else
            {
                inventory.SetSlot(slotIndex, null);
            }
            
            return true;
        }
        else if (packet.Button == 1) // Right click
        {
            if (cursorItem != null && !cursorItem.IsEmpty)
            {
                // Place one item from cursor into slot
                if (slotItem == null || slotItem.IsEmpty)
                {
                    // Empty slot: place one item
                    var oneItem = new ItemStack(cursorItem.ItemId, 1, cursorItem.Nbt, cursorItem.Damage);
                    inventory.SetSlot(slotIndex, oneItem);
                    
                    // Reduce cursor count
                    if (cursorItem.Count > 1)
                    {
                        var newCursorItem = new ItemStack(cursorItem.ItemId, (byte)(cursorItem.Count - 1), cursorItem.Nbt, cursorItem.Damage);
                        inventory.SetCursorItem(newCursorItem);
                    }
                    else
                    {
                        inventory.ClearCursorItem();
                    }
                }
                else if (slotItem.CanStackWith(cursorItem) && slotItem.Count < 64)
                {
                    // Stackable: add one item
                    var newSlotItem = new ItemStack(slotItem.ItemId, (byte)(slotItem.Count + 1), slotItem.Nbt, slotItem.Damage);
                    inventory.SetSlot(slotIndex, newSlotItem);
                    
                    if (cursorItem.Count > 1)
                    {
                        var newCursorItem = new ItemStack(cursorItem.ItemId, (byte)(cursorItem.Count - 1), cursorItem.Nbt, cursorItem.Damage);
                        inventory.SetCursorItem(newCursorItem);
                    }
                    else
                    {
                        inventory.ClearCursorItem();
                    }
                }
                // If not stackable or full, do nothing
            }
            else if (slotItem != null && !slotItem.IsEmpty)
            {
                // Pick up half stack (rounded up)
                int halfCount = (slotItem.Count + 1) / 2;
                var split = slotItem.Split(halfCount);
                if (split != null)
                {
                    inventory.SetCursorItem(split);
                    inventory.SetSlot(slotIndex, slotItem);
                }
            }
            
            return true;
        }
        
        return false;
    }
    
    private async Task<bool> HandleShiftClickMode(ClientConnection connection, Inventory inventory, ClickContainerPacket packet)
    {
        // Mode 1: Shift+Click
        // Move item to first available slot in appropriate area (hotbar/main inventory)
        int slotIndex = packet.Slot;
        var slotItem = slotIndex >= 0 && slotIndex < Inventory.TOTAL_SLOTS ? inventory.GetSlot(slotIndex) : null;
        
        if (slotItem == null || slotItem.IsEmpty)
            return false;
        
        // Determine target area
        bool isHotbar = Inventory.IsHotbarSlot(slotIndex);
        int targetStart = isHotbar ? Inventory.MAIN_INVENTORY_START_SLOT : Inventory.HOTBAR_START_SLOT;
        int targetEnd = isHotbar ? Inventory.MAIN_INVENTORY_END_SLOT : Inventory.HOTBAR_END_SLOT;
        
        // Find first available slot in target area
        for (int i = targetStart; i <= targetEnd; i++)
        {
            var targetSlot = inventory.GetSlot(i);
            if (targetSlot == null || targetSlot.IsEmpty)
            {
                // Move item
                inventory.SetSlot(i, slotItem);
                inventory.SetSlot(slotIndex, null);
                return true;
            }
            else if (targetSlot.CanStackWith(slotItem) && targetSlot.Count < 64)
            {
                // Try to stack
                if (targetSlot.TryCombine(slotItem))
                {
                    inventory.SetSlot(i, targetSlot);
                    if (slotItem.IsEmpty)
                    {
                        inventory.SetSlot(slotIndex, null);
                    }
                    else
                    {
                        inventory.SetSlot(slotIndex, slotItem);
                    }
                    return true;
                }
            }
        }
        
        return false;
    }
    
    private async Task<bool> HandleNumberKeyMode(ClientConnection connection, Inventory inventory, ClickContainerPacket packet)
    {
        // Mode 2: Number Key (1-9, maps to hotbar slots 36-44)
        // Button contains the hotbar slot index (0-8)
        int slotIndex = packet.Slot;
        int hotbarSlot = packet.Button; // Button contains hotbar slot for number key mode
        
        if (hotbarSlot < 0 || hotbarSlot > 8)
            return false;
        
        int hotbarIndex = Inventory.HotbarSlotToIndex(hotbarSlot);
        var slotItem = slotIndex >= 0 && slotIndex < Inventory.TOTAL_SLOTS ? inventory.GetSlot(slotIndex) : null;
        var hotbarItem = inventory.GetSlot(hotbarIndex);
        
        // Swap slot and hotbar slot
        inventory.SetSlot(hotbarIndex, slotItem);
        inventory.SetSlot(slotIndex, hotbarItem);
        
        return true;
    }
    
    private async Task<bool> HandleDropMode(ClientConnection connection, Inventory inventory, ClickContainerPacket packet)
    {
        // Mode 4: Drop
        // Button 0 = Drop one, 1 = Drop stack
        int slotIndex = packet.Slot;
        var slotItem = slotIndex >= 0 && slotIndex < Inventory.TOTAL_SLOTS ? inventory.GetSlot(slotIndex) : null;
        
        if (slotItem == null || slotItem.IsEmpty)
            return false;
        
        if (packet.Button == 0) // Drop one
        {
            if (slotItem.Count > 1)
            {
                var newSlotItem = new ItemStack(slotItem.ItemId, (byte)(slotItem.Count - 1), slotItem.Nbt, slotItem.Damage);
                inventory.SetSlot(slotIndex, newSlotItem);
            }
            else
            {
                inventory.SetSlot(slotIndex, null);
            }
            // TODO: Spawn item entity in world
        }
        else if (packet.Button == 1) // Drop stack
        {
            inventory.SetSlot(slotIndex, null);
            // TODO: Spawn item entity in world
        }
        
        return true;
    }
    
    private async Task<bool> HandleDoubleClickMode(ClientConnection connection, Inventory inventory, ClickContainerPacket packet)
    {
        // Mode 6: Double Click
        // Collects all matching items from inventory into cursor
        var cursorItem = inventory.CursorItem;
        
        if (cursorItem == null || cursorItem.IsEmpty)
            return false;
        
        // Find all matching items and combine them
        for (int i = 0; i < Inventory.TOTAL_SLOTS; i++)
        {
            var slotItem = inventory.GetSlot(i);
            if (slotItem != null && !slotItem.IsEmpty && cursorItem.CanStackWith(slotItem))
            {
                if (cursorItem.TryCombine(slotItem))
                {
                    inventory.SetSlot(i, slotItem);
                    inventory.SetCursorItem(cursorItem);
                    
                    if (cursorItem.Count >= 64) // Full stack, stop collecting
                        break;
                }
            }
        }
        
        return true;
    }
    
    private async Task SendContainerSlotAsync(ClientConnection connection, byte windowId, int stateId, short slot, SlotData slotData)
    {
        try
        {
            var packet = PacketBuilder.BuildSetContainerSlotPacket(windowId, stateId, slot, slotData);
            await connection.SendPacketAsync(packet);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  │  ✗ Error sending Set Container Slot: {ex.Message}");
        }
    }
    
    private async Task SendContainerContentAsync(ClientConnection connection, Inventory inventory)
    {
        try
        {
            // Build all slots
            var slots = new List<SlotData>();
            for (int i = 0; i < Inventory.TOTAL_SLOTS; i++)
            {
                var slot = inventory.GetSlot(i);
                slots.Add(slot?.ToSlotData() ?? SlotData.Empty);
            }
            
            var cursorItem = inventory.CursorItem;
            var carriedItem = cursorItem?.ToSlotData() ?? SlotData.Empty;
            
            var packet = PacketBuilder.BuildSetContainerContentPacket(0, inventory.InventoryStateId, slots, carriedItem);
            await connection.SendPacketAsync(packet);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  │  ✗ Error sending Set Container Content: {ex.Message}");
        }
    }

    public async Task HandleCloseContainerAsync(ClientConnection connection, CloseContainerPacket packet)
    {
        // TODO: Implement container close handling
        Console.WriteLine($"  │  ← Close Container: Window ID {packet.WindowId}");
        
        var player = connection.Player;
        if (player == null)
        {
            Console.WriteLine($"  │  ⚠ Player not found for connection");
            return;
        }
        
        // TODO: Handle container closing
        // For player inventory (window ID 0), this shouldn't normally happen
        // For other containers, handle cleanup
        
        if (packet.WindowId == 0)
        {
            Console.WriteLine($"  │  → Player inventory cannot be closed (ignoring)");
        }
        else
        {
            Console.WriteLine($"  │  → Container close handling not yet implemented");
        }
    }

    public async Task HandleSetHeldItemAsync(ClientConnection connection, SetHeldItemPacket packet)
    {
        // TODO: Implement held item change handling
        Console.WriteLine($"  │  ← Set Held Item: Slot {packet.Slot}");
        
        var player = connection.Player;
        if (player == null)
        {
            Console.WriteLine($"  │  ⚠ Player not found for connection");
            return;
        }
        
        var inventory = player.Inventory;
        
        // Validate slot range (0-8 for hotbar)
        if (packet.Slot < 0 || packet.Slot > 8)
        {
            Console.WriteLine($"  │  ⚠ Invalid hotbar slot: {packet.Slot} (must be 0-8)");
            return;
        }
        
        // Update selected hotbar slot
        inventory.SetSelectedHotbarSlot(packet.Slot);
        
        var heldItem = inventory.GetHeldItem();
        if (heldItem != null)
        {
            Console.WriteLine($"  │  ✓ Hotbar slot changed to {packet.Slot} (holding item ID {heldItem.ItemId}, count {heldItem.Count})");
        }
        else
        {
            Console.WriteLine($"  │  ✓ Hotbar slot changed to {packet.Slot} (empty)");
        }
    }

    public async Task HandleSetCreativeModeSlotAsync(ClientConnection connection, SetCreativeModeSlotPacket packet)
    {
        // Log packet details
        string slotDescription = packet.Slot == -1 ? "Cursor (creative menu)" : $"Slot {packet.Slot}";
        string itemDescription;
        
        if (packet.SlotData == null || !packet.SlotData.Present || packet.SlotData.IsEmpty)
        {
            // Item/block is being removed
            var previousItem = packet.Slot == -1 
                ? connection.Player?.Inventory?.CursorItem 
                : (packet.Slot >= 0 && packet.Slot < Inventory.TOTAL_SLOTS 
                    ? connection.Player?.Inventory?.GetSlot((int)packet.Slot) 
                    : null);
            
            if (previousItem != null && !previousItem.IsEmpty)
            {
                itemDescription = $"REMOVING item ID {previousItem.ItemId}, count {previousItem.Count}";
            }
            else
            {
                itemDescription = "REMOVING (slot was already empty)";
            }
        }
        else
        {
            // Item/block is being added/placed
            itemDescription = $"SETTING item ID {packet.SlotData.ItemId}, count {packet.SlotData.ItemCount}";
        }
        
        Console.WriteLine($"  │  ← Set Creative Mode Slot (0x37): {slotDescription}, {itemDescription}");
        
        var player = connection.Player;
        if (player == null)
        {
            Console.WriteLine($"  │  ⚠ Player not found for connection");
            return;
        }
        
        var inventory = player.Inventory;
        
        // Validate slot range
        if (packet.Slot < -1 || packet.Slot >= Inventory.TOTAL_SLOTS)
        {
            Console.WriteLine($"  │  ⚠ Invalid slot: {packet.Slot} (must be -1 to {Inventory.TOTAL_SLOTS - 1})");
            return;
        }
        
        // Convert SlotData to ItemStack
        var itemStack = ItemStack.FromSlotData(packet.SlotData);
        
        // Slot -1 means set cursor item (creative menu click or drop outside inventory)
        if (packet.Slot == -1)
        {
            if (itemStack.IsEmpty)
            {
                // Getting the previous item before clearing
                var previousItem = inventory.CursorItem;
                inventory.ClearCursorItem();
                
                if (previousItem != null && !previousItem.IsEmpty)
                {
                    Console.WriteLine($"  │  ✓ Cursor cleared (removed item ID {previousItem.ItemId}, count {previousItem.Count})");
                }
                else
                {
                    Console.WriteLine($"  │  ✓ Cursor cleared (was already empty)");
                }
            }
            else
            {
                inventory.SetCursorItem(itemStack);
                Console.WriteLine($"  │  ✓ Cursor set to item ID {itemStack.ItemId}, count {itemStack.Count}");
                
                // Send cursor update to client
                var cursorSlotData = itemStack.ToSlotData();
                await SendContainerSlotAsync(connection, 0, inventory.InventoryStateId, -1, cursorSlotData);
            }
            return;
        }
        
        // Get previous item before setting slot
        var previousSlotItem = inventory.GetSlot((int)packet.Slot);
        
        // Set slot in inventory
        inventory.SetSlot(packet.Slot, itemStack);
        
        if (itemStack.IsEmpty)
        {
            if (previousSlotItem != null && !previousSlotItem.IsEmpty)
            {
                Console.WriteLine($"  │  ✓ Slot {packet.Slot} cleared (removed item ID {previousSlotItem.ItemId}, count {previousSlotItem.Count})");
            }
            else
            {
                Console.WriteLine($"  │  ✓ Slot {packet.Slot} cleared (was already empty)");
            }
        }
        else
        {
            Console.WriteLine($"  │  ✓ Slot {packet.Slot} set to item ID {itemStack.ItemId}, count {itemStack.Count}");
        }
        
        // Send slot update to client
        var slotData = itemStack.ToSlotData();
        await SendContainerSlotAsync(connection, 0, inventory.InventoryStateId, (short)packet.Slot, slotData);
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
    /// Broadcasts a block change to all players who have the chunk containing the block loaded.
    /// </summary>
    private async Task BroadcastBlockChangeAsync(int blockX, int blockY, int blockZ, int blockStateId)
    {
        if (_world == null || _getAllConnections == null)
            return;

        // Calculate which chunk this block is in
        int chunkX = (int)Math.Floor(blockX / 16.0);
        int chunkZ = (int)Math.Floor(blockZ / 16.0);

        // Get all connections
        var allConnections = _getAllConnections().ToList();
        int broadcastCount = 0;

        foreach (var connection in allConnections)
        {
            // Check if this connection's player has the chunk loaded
            if (connection.Player != null && connection.Player.IsChunkLoaded(chunkX, chunkZ))
            {
                try
                {
                    var packet = PacketBuilder.BuildBlockUpdatePacket(blockX, blockY, blockZ, blockStateId);
                    await connection.SendPacketAsync(packet);
                    broadcastCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  │  ✗ Error broadcasting block change to player {connection.PlayerUuid}: {ex.Message}");
                }
            }
        }

        if (broadcastCount > 0)
        {
            Console.WriteLine($"  │  → Broadcast block change to {broadcastCount} player(s) with chunk ({chunkX}, {chunkZ}) loaded");
        }
    }

    /// <summary>
    /// Broadcasts a block destroy stage animation to all players who have the chunk containing the block loaded.
    /// </summary>
    private async Task BroadcastBlockDestroyStageAsync(int blockX, int blockY, int blockZ, int entityId, byte destroyStage)
    {
        if (_world == null || _getAllConnections == null)
            return;

        // Calculate which chunk this block is in
        int chunkX = (int)Math.Floor(blockX / 16.0);
        int chunkZ = (int)Math.Floor(blockZ / 16.0);

        // Get all connections
        var allConnections = _getAllConnections().ToList();
        int broadcastCount = 0;

        foreach (var connection in allConnections)
        {
            // Check if this connection's player has the chunk loaded
            if (connection.Player != null && connection.Player.IsChunkLoaded(chunkX, chunkZ))
            {
                try
                {
                    var packet = PacketBuilder.BuildSetBlockDestroyStagePacket(entityId, blockX, blockY, blockZ, destroyStage);
                    await connection.SendPacketAsync(packet);
                    broadcastCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  │  ✗ Error broadcasting destroy stage to player {connection.PlayerUuid}: {ex.Message}");
                }
            }
        }

        if (broadcastCount > 0)
        {
            string stageDesc = destroyStage < 10 ? $"stage {destroyStage}" : "clear animation";
            Console.WriteLine($"  │  → Broadcast destroy stage ({stageDesc}) to {broadcastCount} player(s) with chunk ({chunkX}, {chunkZ}) loaded");
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
