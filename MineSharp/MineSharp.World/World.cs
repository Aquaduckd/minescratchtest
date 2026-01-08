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
    private readonly TimeManager _timeManager;

    public PlayerManager PlayerManager => _playerManager;
    public EntityManager EntityManager => _entityManager;
    public ChunkManager ChunkManager => _chunkManager;
    public BlockManager BlockManager => _blockManager;
    public TimeManager TimeManager => _timeManager;

    public World(int viewDistance = 10, ITerrainGenerator? generator = null)
    {
        _playerManager = new PlayerManager();
        _entityManager = new EntityManager();
        _chunkManager = new ChunkManager(viewDistance);
        
        // Use provided generator, or default to flat world generator
        generator ??= new Generation.Generators.FlatWorldGenerator();
        _blockManager = new BlockManager(generator);
        
        // Initialize time manager with default noon time
        _timeManager = new TimeManager(initialTimeOfDay: 6000, timeIncreasing: true);
    }

    // Legacy constructor - kept for backward compatibility
    public World(int viewDistance, bool useTerrainGeneration)
    {
        _playerManager = new PlayerManager();
        _entityManager = new EntityManager();
        _chunkManager = new ChunkManager(viewDistance);
        _blockManager = new BlockManager(useTerrainGeneration);
        
        // Initialize time manager with default noon time
        _timeManager = new TimeManager(initialTimeOfDay: 6000, timeIncreasing: true);
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
        // TODO: Implement world tick logic
        // - Call _timeManager.Tick() to advance time
        // - Update entities
        // - Update block ticks
        // For now, just advance time
        _timeManager.Tick();
    }
}

