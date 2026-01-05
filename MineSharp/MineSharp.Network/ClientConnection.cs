using MineSharp.Core;
using MineSharp.Core.Protocol;
using MineSharp.Game;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace MineSharp.Network;

/// <summary>
/// Handles a single client connection.
/// </summary>
public class ClientConnection
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private ConnectionState _state;
    private readonly Guid _connectionId;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private Player? _player;
    private readonly List<byte> _receiveBuffer = new();
    private readonly PacketHandler _packetHandler;

    public ConnectionState State
    {
        get => _state;
        private set => _state = value;
    }

    public Guid ConnectionId { get; }
    public Player? Player
    {
        get => _player;
        set => _player = value;
    }

    public ClientConnection(TcpClient client, PacketHandler packetHandler)
    {
        _client = client;
        // Disable Nagle's algorithm (TCP_NODELAY) as per protocol FAQ
        _client.NoDelay = true;
        _stream = client.GetStream();
        _state = ConnectionState.Handshaking;
        _connectionId = Guid.NewGuid();
        _cancellationTokenSource = new CancellationTokenSource();
        _packetHandler = packetHandler;
    }

    public async Task HandleConnectionAsync()
    {
        try
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                // Read packet
                var packet = await ReadPacketAsync();
                if (packet == null || packet.Length == 0)
                {
                    // Connection closed
                    break;
                }

                // Parse packet
                var (packetId, parsedPacket) = PacketParser.ParsePacket(packet, _state);
                
                // Route to appropriate handler
                await _packetHandler.HandlePacketAsync(this, packetId, parsedPacket, packet);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Connection error: {ex.Message}");
        }
        finally
        {
            Disconnect();
        }
    }

    public async Task<byte[]?> ReadPacketAsync()
    {
        const int maxPacketSize = 2097151; // 2MB - 1
        const int bufferSize = 4096;

        // Read data into buffer until we have a complete packet
        while (true)
        {
            // Check if we have enough data to read packet length (at least 1 byte)
            if (_receiveBuffer.Count < 1)
            {
                // Read more data
                var buffer = new byte[bufferSize];
                var bytesRead = await _stream.ReadAsync(buffer, 0, bufferSize, _cancellationTokenSource.Token);
                if (bytesRead == 0)
                {
                    // Connection closed
                    return null;
                }

                _receiveBuffer.AddRange(buffer.Take(bytesRead));
            }

            // Try to read packet length (VarInt)
            // We need to peek at the VarInt without consuming it yet
            var lengthReader = new ProtocolReader(_receiveBuffer.ToArray());
            int packetLength;
            int lengthBytes;

            try
            {
                packetLength = lengthReader.ReadVarInt();
                lengthBytes = lengthReader.Offset;
            }
            catch (InvalidOperationException)
            {
                // Not enough data for VarInt yet, read more
                continue;
            }

            // Validate packet length
            if (packetLength < 0 || packetLength > maxPacketSize)
            {
                // Invalid packet length, skip one byte and try again
                _receiveBuffer.RemoveAt(0);
                continue;
            }

            // Check if we have the full packet
            var totalPacketSize = lengthBytes + packetLength;
            if (_receiveBuffer.Count < totalPacketSize)
            {
                // Not enough data yet, read more
                continue;
            }

            // Extract the complete packet (including length prefix for parser)
            var packet = _receiveBuffer.Take(totalPacketSize).ToArray();
            _receiveBuffer.RemoveRange(0, totalPacketSize);

            return packet;
        }
    }

    public async Task SendPacketAsync(byte[] packet)
    {
        try
        {
            // NetworkStream.WriteAsync is guaranteed to write all bytes or throw
            // However, per protocol FAQ, we ensure all bytes are sent by using WriteAsync
            // and flushing. The NoDelay setting helps ensure immediate transmission.
            await _stream.WriteAsync(packet, 0, packet.Length, _cancellationTokenSource.Token);
            await _stream.FlushAsync(_cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending packet: {ex.Message}");
            throw;
        }
    }

    public void SetState(ConnectionState newState)
    {
        _state = newState;
    }

    public void Disconnect()
    {
        try
        {
            _cancellationTokenSource.Cancel();
            _stream?.Close();
            _client?.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error disconnecting: {ex.Message}");
        }
    }
}

