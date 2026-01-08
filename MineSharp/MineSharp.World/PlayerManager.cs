using MineSharp.Game;
using System.Collections.Concurrent;

namespace MineSharp.World;

/// <summary>
/// Manages player state in a thread-safe manner.
/// </summary>
public class PlayerManager
{
    private readonly ConcurrentDictionary<Guid, Player> _players;

    public PlayerManager()
    {
        _players = new ConcurrentDictionary<Guid, Player>();
    }

    public void AddPlayer(Player player)
    {
        bool added = _players.TryAdd(player.Uuid, player);
        if (!added)
        {
            // Player already exists - this is expected when reconnecting
            // We'll reuse the existing player instead
        }
    }

    public void RemovePlayer(Guid uuid)
    {
        _players.TryRemove(uuid, out _);
    }

    public Player? GetPlayer(Guid uuid)
    {
        bool found = _players.TryGetValue(uuid, out var player);
        return found ? player : null;
    }

    public List<Player> GetAllPlayers()
    {
        return _players.Values.ToList();
    }
}

