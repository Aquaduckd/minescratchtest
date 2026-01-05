using MineSharp.Game;

namespace MineSharp.World;

/// <summary>
/// Encapsulates all world state including players, entities, and world logic.
/// Represents the internal state of the game as the server sees it.
/// </summary>
public class World
{
    private readonly PlayerManager _playerManager;
    private readonly EntityManager _entityManager;
    private readonly ChunkManager _chunkManager;
    private readonly BlockManager _blockManager;

    public PlayerManager PlayerManager => _playerManager;
    public EntityManager EntityManager => _entityManager;
    public ChunkManager ChunkManager => _chunkManager;
    public BlockManager BlockManager => _blockManager;

    public World(int viewDistance = 10, bool useTerrainGeneration = false)
    {
        _playerManager = new PlayerManager();
        _entityManager = new EntityManager();
        _chunkManager = new ChunkManager(viewDistance);
        _blockManager = new BlockManager(useTerrainGeneration);
    }

    public Player? GetPlayer(Guid uuid)
    {
        // TODO: Implement player retrieval
        throw new NotImplementedException();
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

    public List<Player> GetAllPlayers()
    {
        // TODO: Implement get all players
        throw new NotImplementedException();
    }

    public void Tick(TimeSpan deltaTime)
    {
        // TODO: Implement world tick (updates entities, etc.)
        throw new NotImplementedException();
    }
}

