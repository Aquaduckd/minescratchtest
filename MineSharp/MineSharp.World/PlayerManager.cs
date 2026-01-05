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
        _players.TryAdd(player.Uuid, player);
    }

    public void RemovePlayer(Guid uuid)
    {
        _players.TryRemove(uuid, out _);
    }

    public Player? GetPlayer(Guid uuid)
    {
        _players.TryGetValue(uuid, out var player);
        return player;
    }

    public List<Player> GetAllPlayers()
    {
        return _players.Values.ToList();
    }
}

