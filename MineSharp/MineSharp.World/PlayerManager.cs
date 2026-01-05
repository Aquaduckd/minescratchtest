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
        // TODO: Implement player addition
        throw new NotImplementedException();
    }

    public void RemovePlayer(Guid uuid)
    {
        // TODO: Implement player removal
        throw new NotImplementedException();
    }

    public Player? GetPlayer(Guid uuid)
    {
        // TODO: Implement player retrieval
        throw new NotImplementedException();
    }

    public List<Player> GetAllPlayers()
    {
        // TODO: Implement get all players
        throw new NotImplementedException();
    }
}

