using MineSharp.Network;
using MineSharp.Data;
using MineSharp.World.Generation;
using System.IO;

namespace MineSharp.Server;

/// <summary>
/// Main server orchestration class.
/// </summary>
public class Server
{
    private readonly TcpServer _tcpServer;
    private readonly MineSharp.World.World _world;
    private readonly RegistryManager _registryManager;
    private readonly LootTableManager _lootTableManager;
    private readonly ServerConfiguration _configuration;

    public Server(ServerConfiguration configuration)
    {
        _configuration = configuration;
        _registryManager = new RegistryManager();
        _lootTableManager = new LootTableManager();
        
        // Initialize terrain generator
        var generator = InitializeTerrainGenerator();
        _world = new MineSharp.World.World(configuration.ViewDistance, generator);
        
        // Create packet handler with registry manager and world
        var packetHandler = CreatePacketHandler();
        _tcpServer = new TcpServer(configuration.Port, packetHandler);
    }
    
    private ITerrainGenerator InitializeTerrainGenerator()
    {
        var registry = new TerrainGeneratorRegistry();
        
        // Get generator ID from configuration (default to "noise")
        var generatorId = _configuration.TerrainGeneratorId ?? "noise";
        
        // Validate generator ID
        if (!registry.IsRegistered(generatorId))
        {
            Console.WriteLine($"Warning: Unknown generator ID '{generatorId}'. Falling back to 'noise'.");
            generatorId = "noise";
        }
        
        // Get generator instance
        var generator = registry.GetGenerator(generatorId);
        
        // Configure generator if configuration is provided
        if (_configuration.TerrainGeneratorConfig != null)
        {
            generator.Configure(_configuration.TerrainGeneratorConfig);
            Console.WriteLine($"Configured terrain generator '{generatorId}' with custom settings.");
        }
        else
        {
            Console.WriteLine($"Using terrain generator: {generator.DisplayName} (ID: {generator.GeneratorId})");
        }
        
        return generator;
    }

    private PacketHandler CreatePacketHandler()
    {
        var handshakingHandler = new Network.Handlers.HandshakingHandler();
        var loginHandler = new Network.Handlers.LoginHandler();
        var configurationHandler = new Network.Handlers.ConfigurationHandler(_registryManager);
        
        // Create function to get all connections from TcpServer
        // Note: This creates a closure that captures _tcpServer, which will be set before StartAsync
        Func<System.Collections.Generic.IEnumerable<Network.ClientConnection>> getAllConnections = () => _tcpServer.GetAllConnections();
        var playHandler = new Network.Handlers.PlayHandler(_world, getAllConnections, _registryManager);
        
        return new Network.PacketHandler(handshakingHandler, loginHandler, configurationHandler, playHandler);
    }

    public async Task StartAsync()
    {
        Console.WriteLine("Starting MineSharp server...");
        
        // Load data
        LoadData();
        
        // Start TCP server
        await _tcpServer.StartAsync();
        
        Console.WriteLine("Server started successfully!");
    }

    public void Stop()
    {
        Console.WriteLine("Stopping server...");
        _tcpServer.Stop();
        Console.WriteLine("Server stopped");
    }

    private void LoadData()
    {
        Console.WriteLine("Loading server data...");
        
        // Load registry data
        var dataPath = Path.GetFullPath(_configuration.DataPath);
        if (!Directory.Exists(dataPath))
        {
            Console.WriteLine($"Warning: Data path does not exist: {dataPath}");
            Console.WriteLine("Server will use fallback registry data.");
        }
        else
        {
            _registryManager.LoadRegistries(dataPath);
            Console.WriteLine($"Loaded registry data from: {dataPath}");
        }
    }

    private void StartWorldUpdateLoop()
    {
        // TODO: Implement world update loop (20 TPS)
        // Will be implemented when we add world state management
    }
}

