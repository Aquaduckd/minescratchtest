namespace MineSharp.Network.Handlers;

/// <summary>
/// Manages active block breaking sessions for players.
/// Thread-safe session management with support for starting, canceling, and tracking sessions.
/// </summary>
public class BlockBreakingSessionManager
{
    private readonly Dictionary<Guid, BlockBreakingSession> _activeSessions = new();
    private readonly object _lock = new();

    /// <summary>
    /// Starts a new breaking session for a player.
    /// If a session already exists for this player, it will be cancelled first.
    /// </summary>
    /// <param name="playerUuid">Player UUID</param>
    /// <param name="entityId">Player entity ID</param>
    /// <param name="blockX">Block X coordinate</param>
    /// <param name="blockY">Block Y coordinate</param>
    /// <param name="blockZ">Block Z coordinate</param>
    /// <param name="totalTicks">Total ticks needed to break the block</param>
    /// <param name="blockName">Block identifier (e.g., "minecraft:stone")</param>
    /// <param name="toolName">Tool identifier (e.g., "minecraft:iron_pickaxe")</param>
    /// <param name="toolSpeed">Tool speed multiplier</param>
    /// <param name="blockHardness">Block hardness value</param>
    /// <returns>The new breaking session</returns>
    public BlockBreakingSession StartSession(
        Guid playerUuid,
        int entityId,
        int blockX,
        int blockY,
        int blockZ,
        int totalTicks,
        string blockName,
        string toolName,
        double toolSpeed,
        double blockHardness)
    {
        lock (_lock)
        {
            // Cancel existing session if one exists
            if (_activeSessions.TryGetValue(playerUuid, out var existingSession))
            {
                existingSession.CancellationToken.Cancel();
                _activeSessions.Remove(playerUuid);
            }

            // Create new session
            var session = new BlockBreakingSession(
                playerUuid,
                entityId,
                blockX,
                blockY,
                blockZ,
                totalTicks,
                blockName,
                toolName,
                toolSpeed,
                blockHardness);

            _activeSessions[playerUuid] = session;
            return session;
        }
    }

    /// <summary>
    /// Cancels an active breaking session for a player.
    /// </summary>
    /// <param name="playerUuid">Player UUID</param>
    /// <returns>True if a session was cancelled, false if no session existed</returns>
    public bool CancelSession(Guid playerUuid)
    {
        lock (_lock)
        {
            if (_activeSessions.TryGetValue(playerUuid, out var session))
            {
                session.CancellationToken.Cancel();
                _activeSessions.Remove(playerUuid);
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Gets the active breaking session for a player.
    /// </summary>
    /// <param name="playerUuid">Player UUID</param>
    /// <returns>The active session, or null if no session exists</returns>
    public BlockBreakingSession? GetSession(Guid playerUuid)
    {
        lock (_lock)
        {
            return _activeSessions.TryGetValue(playerUuid, out var session) ? session : null;
        }
    }

    /// <summary>
    /// Checks if a player has an active breaking session.
    /// </summary>
    /// <param name="playerUuid">Player UUID</param>
    /// <returns>True if a session exists, false otherwise</returns>
    public bool HasSession(Guid playerUuid)
    {
        lock (_lock)
        {
            return _activeSessions.ContainsKey(playerUuid);
        }
    }

    /// <summary>
    /// Gets all active breaking sessions.
    /// Returns a copy of the sessions dictionary (thread-safe).
    /// </summary>
    /// <returns>Dictionary of player UUIDs to their breaking sessions</returns>
    public Dictionary<Guid, BlockBreakingSession> GetAllSessions()
    {
        lock (_lock)
        {
            return new Dictionary<Guid, BlockBreakingSession>(_activeSessions);
        }
    }

    /// <summary>
    /// Removes a completed or cancelled session.
    /// Should be called after a session is finished to clean up resources.
    /// </summary>
    /// <param name="playerUuid">Player UUID</param>
    public void RemoveSession(Guid playerUuid)
    {
        lock (_lock)
        {
            _activeSessions.Remove(playerUuid);
        }
    }

    /// <summary>
    /// Cleans up all completed or cancelled sessions.
    /// Removes sessions that have been cancelled or completed.
    /// </summary>
    public void CleanupCompletedSessions()
    {
        lock (_lock)
        {
            var toRemove = new List<Guid>();
            foreach (var kvp in _activeSessions)
            {
                var session = kvp.Value;
                if (session.CancellationToken.Token.IsCancellationRequested || session.IsComplete)
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var uuid in toRemove)
            {
                _activeSessions.Remove(uuid);
            }
        }
    }

    /// <summary>
    /// Clears all active sessions (for shutdown or reset).
    /// </summary>
    public void ClearAllSessions()
    {
        lock (_lock)
        {
            foreach (var session in _activeSessions.Values)
            {
                session.CancellationToken.Cancel();
            }
            _activeSessions.Clear();
        }
    }
}

