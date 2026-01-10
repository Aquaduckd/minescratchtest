using MineSharp.Core.Protocol;
using MineSharp.Game;
using MineSharp.Network.Handlers;
using MineSharp.World;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MineSharp.Network;

/// <summary>
/// Manages player visibility - which players can see each other.
/// Handles spawning/despawning players for clients based on distance.
/// </summary>
public class PlayerVisibilityManager
{
    private readonly MineSharp.World.World _world;
    private readonly PlayHandler _playHandler;
    private readonly Func<IEnumerable<ClientConnection>> _getAllConnections;
    private readonly double _viewDistanceBlocks;
    
    // Track last sent position per viewer-entity pair for delta calculations
    // Key: (viewerUuid, entityUuid) - tracks where each viewer last saw each entity
    private readonly Dictionary<(Guid viewerUuid, Guid entityUuid), (double X, double Y, double Z)> _lastSentPositions = new();
    private readonly object _positionLock = new object();
    
    // Track last sent rotation per viewer-entity pair for rotation updates
    // Key: (viewerUuid, entityUuid) - tracks rotation each viewer last saw for each entity
    private readonly Dictionary<(Guid viewerUuid, Guid entityUuid), (float Yaw, float Pitch)> _lastSentRotations = new();
    private readonly object _rotationLock = new object();
    
    // Track last sent head yaw per viewer-entity pair for head rotation updates
    // Key: (viewerUuid, entityUuid) - tracks head yaw each viewer last saw for each entity
    private readonly Dictionary<(Guid viewerUuid, Guid entityUuid), float> _lastSentHeadYaw = new();
    private readonly object _headYawLock = new object();
    
    // Player entity type ID (from minecraft:entity_type registry)
    // TODO: Look this up from registry instead of hardcoding
    // For now, using a placeholder - this needs to be the actual player entity type ID
    private readonly int _playerEntityTypeId;

    public PlayerVisibilityManager(
        MineSharp.World.World world,
        PlayHandler playHandler,
        Func<IEnumerable<ClientConnection>> getAllConnections,
        double viewDistanceBlocks = 48.0, // Default: 48 blocks = 3 chunks
        int playerEntityTypeId = 1) // Placeholder - needs to be looked up from registry
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _playHandler = playHandler ?? throw new ArgumentNullException(nameof(playHandler));
        _getAllConnections = getAllConnections ?? throw new ArgumentNullException(nameof(getAllConnections));
        _viewDistanceBlocks = viewDistanceBlocks;
        _playerEntityTypeId = playerEntityTypeId;
    }

    /// <summary>
    /// Called when a new player joins the game.
    /// Spawns the new player for all existing players, and spawns all existing players for the new player.
    /// </summary>
    public async Task OnPlayerJoinedAsync(ClientConnection newConnection, Player newPlayer)
    {
        if (newConnection == null || newPlayer == null)
        {
            return;
        }

        var allConnections = _getAllConnections().ToList();
        
        // Spawn new player for all existing players
        foreach (var connection in allConnections)
        {
            if (connection == newConnection || connection.Player == null)
            {
                continue;
            }

            var viewer = connection.Player;
            
            // Check if new player should be visible to this viewer
            if (IsWithinViewDistance(viewer, newPlayer))
            {
                await SpawnPlayerForClientAsync(connection, newPlayer);
                viewer.AddVisibleEntity(newPlayer.EntityId);
            }
        }

        // Spawn all existing players for the new player
        foreach (var connection in allConnections)
        {
            if (connection == newConnection || connection.Player == null)
            {
                continue;
            }

            var existingPlayer = connection.Player;
            
            // Check if existing player should be visible to new player
            if (IsWithinViewDistance(newPlayer, existingPlayer))
            {
                await SpawnPlayerForClientAsync(newConnection, existingPlayer);
                newPlayer.AddVisibleEntity(existingPlayer.EntityId);
            }
        }

        // Initialize last sent positions and rotations for the new player
        // When new player spawns, record their position/rotation for all existing viewers
        // This is done in SpawnPlayerForClientAsync, but we also need to initialize
        // positions/rotations for when the new player sees existing players
        lock (_positionLock)
        {
            lock (_rotationLock)
            {
                lock (_headYawLock)
                {
                    // Record positions, rotations, and head yaw for existing players as seen by the new player
                    foreach (var connection in allConnections)
                    {
                        if (connection == newConnection || connection.Player == null)
                        {
                            continue;
                        }

                        var existingPlayer = connection.Player;
                        // Record where the new player last saw each existing player
                        _lastSentPositions[(newPlayer.Uuid, existingPlayer.Uuid)] = (existingPlayer.Position.X, existingPlayer.Position.Y, existingPlayer.Position.Z);
                        _lastSentRotations[(newPlayer.Uuid, existingPlayer.Uuid)] = (existingPlayer.Yaw, existingPlayer.Pitch);
                        _lastSentHeadYaw[(newPlayer.Uuid, existingPlayer.Uuid)] = existingPlayer.HeadYaw;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Called when a player moves.
    /// Updates visibility and broadcasts position updates to nearby players.
    /// </summary>
    public async Task OnPlayerMovedAsync(Player movedPlayer)
    {
        if (movedPlayer == null)
        {
            return;
        }

        var allConnections = _getAllConnections().ToList();
        var currentPosition = movedPlayer.Position;

        // Update visibility for this player (check which players should be visible)
        foreach (var connection in allConnections)
        {
            if (connection.Player == null || connection.Player == movedPlayer)
            {
                continue;
            }

            var otherPlayer = connection.Player;
            bool shouldBeVisible = IsWithinViewDistance(movedPlayer, otherPlayer);
            bool isCurrentlyVisible = movedPlayer.IsEntityVisible(otherPlayer.EntityId);

            if (shouldBeVisible && !isCurrentlyVisible)
            {
                // Player entered view distance - spawn them
                await SpawnPlayerForClientAsync(GetConnectionForPlayer(movedPlayer), otherPlayer);
                movedPlayer.AddVisibleEntity(otherPlayer.EntityId);
            }
            else if (!shouldBeVisible && isCurrentlyVisible)
            {
                // Player left view distance - despawn them
                await DespawnPlayerForClientAsync(GetConnectionForPlayer(movedPlayer), otherPlayer.EntityId);
                movedPlayer.RemoveVisibleEntity(otherPlayer.EntityId);
            }
        }

        // Broadcast position update to players who can see this player
        foreach (var connection in allConnections)
        {
            if (connection.Player == null || connection.Player == movedPlayer)
            {
                continue;
            }

            var viewer = connection.Player;
            
            if (viewer.IsEntityVisible(movedPlayer.EntityId))
            {
                // Viewer can see this player - send position update
                await BroadcastPositionUpdateAsync(connection, movedPlayer);
            }
        }

        // Also check if other players should now see this player
        foreach (var connection in allConnections)
        {
            if (connection.Player == null || connection.Player == movedPlayer)
            {
                continue;
            }

            var otherPlayer = connection.Player;
            bool shouldBeVisible = IsWithinViewDistance(otherPlayer, movedPlayer);
            bool isCurrentlyVisible = otherPlayer.IsEntityVisible(movedPlayer.EntityId);

            if (shouldBeVisible && !isCurrentlyVisible)
            {
                // This player entered other player's view distance
                await SpawnPlayerForClientAsync(connection, movedPlayer);
                otherPlayer.AddVisibleEntity(movedPlayer.EntityId);
            }
            else if (!shouldBeVisible && isCurrentlyVisible)
            {
                // This player left other player's view distance
                await DespawnPlayerForClientAsync(connection, movedPlayer.EntityId);
                otherPlayer.RemoveVisibleEntity(movedPlayer.EntityId);
            }
        }
    }

    /// <summary>
    /// Called when a player disconnects.
    /// Despawns the player for all other players and removes them from the tab list.
    /// </summary>
    public async Task OnPlayerDisconnectedAsync(Player disconnectedPlayer)
    {
        if (disconnectedPlayer == null)
        {
            return;
        }

        var allConnections = _getAllConnections().ToList();

        // Send Player Info Remove packet to all remaining players to remove disconnected player from tab list
        var playerUuidsToRemove = new List<Guid> { disconnectedPlayer.Uuid };
        var removePacket = PacketBuilder.BuildPlayerInfoRemovePacket(playerUuidsToRemove);

        foreach (var connection in allConnections)
        {
            if (connection.Player == null || connection.Player == disconnectedPlayer)
            {
                continue;
            }

            try
            {
                await connection.SendPacketAsync(removePacket);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending Player Info Remove to player: {ex.Message}");
            }
        }

        // Despawn this player for all other players
        foreach (var connection in allConnections)
        {
            if (connection.Player == null || connection.Player == disconnectedPlayer)
            {
                continue;
            }

            var viewer = connection.Player;
            
            if (viewer.IsEntityVisible(disconnectedPlayer.EntityId))
            {
                await DespawnPlayerForClientAsync(connection, disconnectedPlayer.EntityId);
                viewer.RemoveVisibleEntity(disconnectedPlayer.EntityId);
            }
        }

        // Clean up last sent positions, rotations, and head yaw for this disconnected player
        // Remove all entries where this player is either the viewer or the entity
        lock (_positionLock)
        {
            lock (_rotationLock)
            {
                lock (_headYawLock)
                {
                    var positionKeysToRemove = _lastSentPositions.Keys
                        .Where(key => key.viewerUuid == disconnectedPlayer.Uuid || key.entityUuid == disconnectedPlayer.Uuid)
                        .ToList();
                    
                    var rotationKeysToRemove = _lastSentRotations.Keys
                        .Where(key => key.viewerUuid == disconnectedPlayer.Uuid || key.entityUuid == disconnectedPlayer.Uuid)
                        .ToList();
                    
                    var headYawKeysToRemove = _lastSentHeadYaw.Keys
                        .Where(key => key.viewerUuid == disconnectedPlayer.Uuid || key.entityUuid == disconnectedPlayer.Uuid)
                        .ToList();
                    
                    foreach (var key in positionKeysToRemove)
                    {
                        _lastSentPositions.Remove(key);
                    }
                    
                    foreach (var key in rotationKeysToRemove)
                    {
                        _lastSentRotations.Remove(key);
                    }
                    
                    foreach (var key in headYawKeysToRemove)
                    {
                        _lastSentHeadYaw.Remove(key);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Sends Player Info Update packets for all players.
    /// Should be called after Synchronize Player Position (step 33/34 in FAQ).
    /// </summary>
    public async Task SendPlayerInfoUpdatesAsync(ClientConnection newConnection, Player newPlayer)
    {
        if (newConnection == null || newPlayer == null)
        {
            return;
        }

        var allConnections = _getAllConnections().ToList();
        
        // Step 33: Send Player Info Update for all existing players to the new player
        var existingPlayers = new List<(Guid, string)>();
        foreach (var connection in allConnections)
        {
            if (connection == newConnection || connection.Player == null)
            {
                continue;
            }

            var existingPlayer = connection.Player;
            existingPlayers.Add((existingPlayer.Uuid, GetPlayerName(existingPlayer)));
        }

        if (existingPlayers.Count > 0)
        {
            try
            {
                var playerInfoPacket = PacketBuilder.BuildPlayerInfoUpdatePacket(existingPlayers);
                await newConnection.SendPacketAsync(playerInfoPacket);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending Player Info Update (existing players) to new player: {ex.Message}");
            }
        }

        // Step 34: Send Player Info Update for the new player to all existing players
        var newPlayerInfo = new List<(Guid, string)> { (newPlayer.Uuid, GetPlayerName(newPlayer)) };
        foreach (var connection in allConnections)
        {
            if (connection == newConnection || connection.Player == null)
            {
                continue;
            }

            try
            {
                var playerInfoPacket = PacketBuilder.BuildPlayerInfoUpdatePacket(newPlayerInfo);
                await connection.SendPacketAsync(playerInfoPacket);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending Player Info Update (new player) to existing player: {ex.Message}");
            }
        }

        // Also send Player Info Update for the new player to themselves (so they appear in their own tab list)
        try
        {
            var selfPlayerInfoPacket = PacketBuilder.BuildPlayerInfoUpdatePacket(newPlayerInfo);
            await newConnection.SendPacketAsync(selfPlayerInfoPacket);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending Player Info Update (self) to new player: {ex.Message}");
        }
    }

    /// <summary>
    /// Broadcasts an entity metadata update to all players who can see the entity.
    /// Used for updating sneaking state, pose, and other metadata changes.
    /// </summary>
    /// <param name="player">The player whose entity metadata is being updated</param>
    /// <param name="entityId">The entity ID to update</param>
    /// <param name="isSneaking">Whether the entity is sneaking</param>
    public async Task BroadcastEntityMetadataUpdateAsync(Player player, int entityId, bool isSneaking)
    {
        if (player == null)
        {
            return;
        }

        var allConnections = _getAllConnections().ToList();
        var metadataPacket = PacketBuilder.BuildSetEntityMetadataPacket(entityId, isSneaking);

        foreach (var connection in allConnections)
        {
            if (connection.Player == null || connection.Player == player)
            {
                continue; // Skip self and connections without players
            }

            var viewer = connection.Player;

            // Only send to players who can see this entity
            if (viewer.IsEntityVisible(entityId))
            {
                try
                {
                    await connection.SendPacketAsync(metadataPacket);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error broadcasting entity metadata update to player: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Spawns a player entity for a client connection.
    /// Note: Player Info Update should be sent separately before this (step 33/34).
    /// </summary>
    private async Task SpawnPlayerForClientAsync(ClientConnection connection, Player playerToSpawn)
    {
        if (connection == null || playerToSpawn == null || connection.Player == null)
        {
            return;
        }

        try
        {
            // Send Spawn Entity packet
            var position = playerToSpawn.Position;
            var spawnPacket = PacketBuilder.BuildSpawnEntityPacket(
                entityId: playerToSpawn.EntityId,
                entityUuid: playerToSpawn.Uuid,
                entityType: _playerEntityTypeId,
                x: position.X,
                y: position.Y,
                z: position.Z,
                velocityX: 0.0,
                velocityY: 0.0,
                velocityZ: 0.0,
                pitch: playerToSpawn.Pitch,
                yaw: playerToSpawn.Yaw,
                headYaw: playerToSpawn.HeadYaw,
                data: 0);

            await connection.SendPacketAsync(spawnPacket);
            
            // Initialize last sent position and rotation for this viewer-entity pair
            // This ensures delta calculations start from the correct baseline
            var viewer = connection.Player;
            lock (_positionLock)
            {
                lock (_rotationLock)
                {
                    lock (_headYawLock)
                    {
                        _lastSentPositions[(viewer.Uuid, playerToSpawn.Uuid)] = (position.X, position.Y, position.Z);
                        _lastSentRotations[(viewer.Uuid, playerToSpawn.Uuid)] = (playerToSpawn.Yaw, playerToSpawn.Pitch);
                        _lastSentHeadYaw[(viewer.Uuid, playerToSpawn.Uuid)] = playerToSpawn.HeadYaw;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error spawning player {playerToSpawn.EntityId} for connection: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the player's name from their connection, or generates a default name from UUID.
    /// </summary>
    private string GetPlayerName(Player player)
    {
        // Try to get name from connection
        var connection = _getAllConnections().FirstOrDefault(c => c.Player?.Uuid == player.Uuid);
        if (connection != null && !string.IsNullOrEmpty(connection.Username))
        {
            return connection.Username;
        }
        
        // Fallback: use first 16 characters of UUID string (without dashes)
        string uuidStr = player.Uuid.ToString("N");
        return uuidStr.Length > 16 ? uuidStr.Substring(0, 16) : uuidStr;
    }

    /// <summary>
    /// Despawns an entity for a client connection.
    /// </summary>
    private async Task DespawnPlayerForClientAsync(ClientConnection connection, int entityId)
    {
        if (connection == null)
        {
            return;
        }

        try
        {
            var packet = PacketBuilder.BuildRemoveEntitiesPacket(new[] { entityId });
            await connection.SendPacketAsync(packet);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error despawning entity {entityId} for connection: {ex.Message}");
        }
    }

    /// <summary>
    /// Broadcasts a position update for a player to a viewer.
    /// Chooses the appropriate packet type based on movement distance.
    /// </summary>
    private async Task BroadcastPositionUpdateAsync(ClientConnection viewerConnection, Player movedPlayer)
    {
        if (viewerConnection == null || movedPlayer == null || viewerConnection.Player == null)
        {
            return;
        }

        try
        {
            var viewer = viewerConnection.Player;
            var currentPos = movedPlayer.Position;
            var positionKey = (viewer.Uuid, movedPlayer.Uuid);
            (double lastX, double lastY, double lastZ) lastPos;

            lock (_positionLock)
            {
                if (!_lastSentPositions.TryGetValue(positionKey, out lastPos))
                {
                    // If no last position recorded, use current position as baseline
                    // This shouldn't happen if SpawnPlayerForClientAsync is called correctly
                    lastPos = (currentPos.X, currentPos.Y, currentPos.Z);
                    _lastSentPositions[positionKey] = lastPos;
                }
            }

            // Calculate delta in fixed-point format (multiply by 4096)
            double deltaX = currentPos.X - lastPos.lastX;
            double deltaY = currentPos.Y - lastPos.lastY;
            double deltaZ = currentPos.Z - lastPos.lastZ;

            // Check if movement is large (>= 8 blocks) - use Teleport Entity
            if (Math.Abs(deltaX) >= 8.0 || Math.Abs(deltaY) >= 8.0 || Math.Abs(deltaZ) >= 8.0)
            {
                var packet = PacketBuilder.BuildTeleportEntityPacket(
                    entityId: movedPlayer.EntityId,
                    x: currentPos.X,
                    y: currentPos.Y,
                    z: currentPos.Z,
                    yaw: movedPlayer.Yaw,
                    pitch: movedPlayer.Pitch,
                    onGround: true); // TODO: Track onGround state

                await viewerConnection.SendPacketAsync(packet);
            }
            else
            {
                // Small movement - use Update Entity Position and Rotation
                // Convert to fixed-point (multiply by 4096, then cast to short)
                short deltaXFixed = (short)Math.Round(deltaX * 4096.0);
                short deltaYFixed = (short)Math.Round(deltaY * 4096.0);
                short deltaZFixed = (short)Math.Round(deltaZ * 4096.0);

                var packet = PacketBuilder.BuildUpdateEntityPositionAndRotationPacket(
                    entityId: movedPlayer.EntityId,
                    deltaX: deltaXFixed,
                    deltaY: deltaYFixed,
                    deltaZ: deltaZFixed,
                    yaw: movedPlayer.Yaw,
                    pitch: movedPlayer.Pitch,
                    onGround: true); // TODO: Track onGround state

                await viewerConnection.SendPacketAsync(packet);
            }

            // Update last sent position and rotation for this viewer-entity pair
            // Note: Head yaw is tracked separately in BroadcastHeadYawUpdateToViewerAsync
            lock (_positionLock)
            {
                lock (_rotationLock)
                {
                    _lastSentPositions[positionKey] = (currentPos.X, currentPos.Y, currentPos.Z);
                    _lastSentRotations[positionKey] = (movedPlayer.Yaw, movedPlayer.Pitch);
                }
            }
            
            // Check if head yaw changed and send separate head rotation update if needed
            // This must be called AFTER updating position/rotation, but BEFORE updating head yaw tracking
            await BroadcastHeadYawUpdateToViewerAsync(viewerConnection, movedPlayer);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error broadcasting position update for player {movedPlayer.EntityId}: {ex.Message}");
        }
    }

    /// <summary>
    /// Broadcasts a rotation-only update for a player to a viewer.
    /// Called when only rotation changes (position unchanged).
    /// </summary>
    public async Task BroadcastRotationUpdateAsync(Player rotatedPlayer)
    {
        if (rotatedPlayer == null)
        {
            return;
        }

        var allConnections = _getAllConnections().ToList();

        // Broadcast rotation update to players who can see this player
        foreach (var connection in allConnections)
        {
            if (connection.Player == null || connection.Player == rotatedPlayer)
            {
                continue;
            }

            var viewer = connection.Player;
            
            if (viewer.IsEntityVisible(rotatedPlayer.EntityId))
            {
                // Viewer can see this player - send rotation update
                await BroadcastRotationUpdateToViewerAsync(connection, rotatedPlayer);
            }
        }
    }

    /// <summary>
    /// Broadcasts a rotation-only update for a player to a specific viewer.
    /// </summary>
    private async Task BroadcastRotationUpdateToViewerAsync(ClientConnection viewerConnection, Player rotatedPlayer)
    {
        if (viewerConnection == null || rotatedPlayer == null || viewerConnection.Player == null)
        {
            return;
        }

        try
        {
            var viewer = viewerConnection.Player;
            var rotationKey = (viewer.Uuid, rotatedPlayer.Uuid);
            (float lastYaw, float lastPitch) lastRot;

            lock (_rotationLock)
            {
                if (!_lastSentRotations.TryGetValue(rotationKey, out lastRot))
                {
                    // If no last rotation recorded, use current rotation as baseline
                    // This shouldn't happen if SpawnPlayerForClientAsync is called correctly
                    lastRot = (rotatedPlayer.Yaw, rotatedPlayer.Pitch);
                    _lastSentRotations[rotationKey] = lastRot;
                }
            }

            // Check if rotation actually changed
            const float rotationEpsilon = 0.01f; // Small threshold to avoid floating point precision issues
            if (Math.Abs(rotatedPlayer.Yaw - lastRot.lastYaw) < rotationEpsilon &&
                Math.Abs(rotatedPlayer.Pitch - lastRot.lastPitch) < rotationEpsilon)
            {
                // Rotation hasn't changed significantly, skip update
                return;
            }

            // Send rotation-only update
            var packet = PacketBuilder.BuildUpdateEntityRotationPacket(
                entityId: rotatedPlayer.EntityId,
                yaw: rotatedPlayer.Yaw,
                pitch: rotatedPlayer.Pitch,
                onGround: true); // TODO: Track onGround state

            await viewerConnection.SendPacketAsync(packet);

            // Update last sent rotation for this viewer-entity pair
            // Note: Head yaw is tracked separately in BroadcastHeadYawUpdateToViewerAsync
            lock (_rotationLock)
            {
                _lastSentRotations[rotationKey] = (rotatedPlayer.Yaw, rotatedPlayer.Pitch);
            }
            
            // Check if head yaw changed and send separate head rotation update if needed
            // This must be called AFTER updating rotation, but BEFORE updating head yaw tracking
            await BroadcastHeadYawUpdateToViewerAsync(viewerConnection, rotatedPlayer);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error broadcasting rotation update for player {rotatedPlayer.EntityId}: {ex.Message}");
        }
    }

    /// <summary>
    /// Broadcasts a head yaw update for a player to all viewers.
    /// Called when head yaw changes (can be independent of body rotation).
    /// </summary>
    public async Task BroadcastHeadYawUpdateAsync(Player rotatedPlayer)
    {
        if (rotatedPlayer == null)
        {
            return;
        }

        var allConnections = _getAllConnections().ToList();

        // Broadcast head yaw update to players who can see this player
        foreach (var connection in allConnections)
        {
            if (connection.Player == null || connection.Player == rotatedPlayer)
            {
                continue;
            }

            var viewer = connection.Player;
            
            if (viewer.IsEntityVisible(rotatedPlayer.EntityId))
            {
                // Viewer can see this player - send head yaw update
                await BroadcastHeadYawUpdateToViewerAsync(connection, rotatedPlayer);
            }
        }
    }

    /// <summary>
    /// Broadcasts a head yaw update for a player to a specific viewer.
    /// </summary>
    private async Task BroadcastHeadYawUpdateToViewerAsync(ClientConnection viewerConnection, Player rotatedPlayer)
    {
        if (viewerConnection == null || rotatedPlayer == null || viewerConnection.Player == null)
        {
            return;
        }

        try
        {
            var viewer = viewerConnection.Player;
            var headYawKey = (viewer.Uuid, rotatedPlayer.Uuid);
            float lastHeadYaw;

            lock (_headYawLock)
            {
                if (!_lastSentHeadYaw.TryGetValue(headYawKey, out lastHeadYaw))
                {
                    // If no last head yaw recorded, use current head yaw as baseline
                    // This shouldn't happen if SpawnPlayerForClientAsync is called correctly
                    lastHeadYaw = rotatedPlayer.HeadYaw;
                    _lastSentHeadYaw[headYawKey] = lastHeadYaw;
                }
            }

            // Check if head yaw actually changed
            const float headYawEpsilon = 0.01f; // Small threshold to avoid floating point precision issues
            if (Math.Abs(rotatedPlayer.HeadYaw - lastHeadYaw) < headYawEpsilon)
            {
                // Head yaw hasn't changed significantly, skip update
                return;
            }

            // Send head yaw-only update
            var packet = PacketBuilder.BuildRotateHeadPacket(
                entityId: rotatedPlayer.EntityId,
                headYaw: rotatedPlayer.HeadYaw);

            await viewerConnection.SendPacketAsync(packet);

            // Update last sent head yaw for this viewer-entity pair
            lock (_headYawLock)
            {
                _lastSentHeadYaw[headYawKey] = rotatedPlayer.HeadYaw;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error broadcasting head yaw update for player {rotatedPlayer.EntityId}: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks if target player is within view distance of viewer player.
    /// </summary>
    private bool IsWithinViewDistance(Player viewer, Player target)
    {
        if (viewer == null || target == null)
        {
            return false;
        }

        var viewerPos = viewer.Position;
        var targetPos = target.Position;

        // Calculate Euclidean distance
        double dx = targetPos.X - viewerPos.X;
        double dy = targetPos.Y - viewerPos.Y;
        double dz = targetPos.Z - viewerPos.Z;
        double distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);

        return distance <= _viewDistanceBlocks;
    }

    /// <summary>
    /// Gets the ClientConnection for a given player.
    /// </summary>
    private ClientConnection? GetConnectionForPlayer(Player player)
    {
        if (player == null)
        {
            return null;
        }

        return _getAllConnections().FirstOrDefault(c => c.Player?.Uuid == player.Uuid);
    }
}

