using MineSharp.Core;
using MineSharp.Network.Handlers;
using System.Net;
using System.Net.Sockets;

namespace MineSharp.Network;

/// <summary>
/// Main TCP server that accepts client connections.
/// </summary>
public class TcpServer
{
    private readonly int _port;
    private TcpListener? _listener;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly List<ClientConnection> _connections = new();
    private readonly PacketHandler _packetHandler;

    public TcpServer(int port = 25565, PacketHandler? packetHandler = null)
    {
        _port = port;
        _packetHandler = packetHandler ?? throw new ArgumentNullException(nameof(packetHandler), "PacketHandler is required");
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();
        
        Console.WriteLine($"Minecraft server started on port {_port}");
        
        // Start accepting connections
        _ = Task.Run(() => AcceptConnectionsAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
    }

    public void Stop()
    {
        _cancellationTokenSource?.Cancel();
        _listener?.Stop();
        
        // Disconnect all clients
        lock (_connections)
        {
            foreach (var connection in _connections)
            {
                connection.Disconnect();
            }
            _connections.Clear();
        }
        
        Console.WriteLine("Server stopped");
    }

    /// <summary>
    /// Gets all active client connections (thread-safe).
    /// </summary>
    public List<ClientConnection> GetAllConnections()
    {
        lock (_connections)
        {
            return new List<ClientConnection>(_connections);
        }
    }

    private async Task AcceptConnectionsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var client = await _listener!.AcceptTcpClientAsync();
                var connection = new ClientConnection(client, _packetHandler);
                
                lock (_connections)
                {
                    _connections.Add(connection);
                }
                
                Console.WriteLine($"New connection from {client.Client.RemoteEndPoint}");
                
                // Handle connection in background task
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await connection.HandleConnectionAsync();
                    }
                    finally
                    {
                        // Notify PlayHandler of disconnection if player exists
                        if (connection.Player != null)
                        {
                            try
                            {
                                await _packetHandler.PlayHandler.OnPlayerDisconnectedAsync(connection.Player, connection.Username);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error notifying player disconnection: {ex.Message}");
                            }
                        }
                        
                        lock (_connections)
                        {
                            _connections.Remove(connection);
                        }
                        Console.WriteLine($"Connection closed: {client.Client.RemoteEndPoint}");
                    }
                }, cancellationToken);
            }
            catch (ObjectDisposedException)
            {
                // Listener was disposed (server stopped)
                break;
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine($"Error accepting connection: {ex.Message}");
                }
            }
        }
    }
}

