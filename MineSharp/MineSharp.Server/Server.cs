using MineSharp.Network;
using MineSharp.Data;
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
        _world = new MineSharp.World.World(configuration.ViewDistance, configuration.UseTerrainGeneration);
        
        // Create packet handler with registry manager and world
        var packetHandler = CreatePacketHandler();
        _tcpServer = new TcpServer(configuration.Port, packetHandler);
    }

    private PacketHandler CreatePacketHandler()
    {
        var handshakingHandler = new Network.Handlers.HandshakingHandler();
        var loginHandler = new Network.Handlers.LoginHandler();
        var configurationHandler = new Network.Handlers.ConfigurationHandler(_registryManager);
        var playHandler = new Network.Handlers.PlayHandler(_world);
        
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

