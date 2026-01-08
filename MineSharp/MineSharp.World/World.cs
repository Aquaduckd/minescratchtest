using MineSharp.Game;
using MineSharp.World.Generation;

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

    public World(int viewDistance = 10, ITerrainGenerator? generator = null)
    {
        _playerManager = new PlayerManager();
        _entityManager = new EntityManager();
        _chunkManager = new ChunkManager(viewDistance);
        
        // Use provided generator, or default to flat world generator
        generator ??= new Generation.Generators.FlatWorldGenerator();
        _blockManager = new BlockManager(generator);
    }

    // Legacy constructor - kept for backward compatibility
    public World(int viewDistance, bool useTerrainGeneration)
    {
        _playerManager = new PlayerManager();
        _entityManager = new EntityManager();
        _chunkManager = new ChunkManager(viewDistance);
        _blockManager = new BlockManager(useTerrainGeneration);
    }

    public Player? GetPlayer(Guid uuid)
    {
        return _playerManager.GetPlayer(uuid);
    }

    public void AddPlayer(Player player)
    {
        _playerManager.AddPlayer(player);
    }

    public void RemovePlayer(Guid uuid)
    {
        _playerManager.RemovePlayer(uuid);
    }

    public List<Player> GetAllPlayers()
    {
        return _playerManager.GetAllPlayers();
    }

    public void Tick(TimeSpan deltaTime)
    {
        // TODO: Implement world tick (updates entities, etc.)
        throw new NotImplementedException();
    }
}

