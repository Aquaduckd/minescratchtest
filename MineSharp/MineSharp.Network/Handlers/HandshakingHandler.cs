using MineSharp.Core;
using MineSharp.Core.Protocol.PacketTypes;

namespace MineSharp.Network.Handlers;

/// <summary>
/// Handles packets in the Handshaking state.
/// </summary>
public class HandshakingHandler
{
    public async Task HandleHandshakeAsync(ClientConnection connection, HandshakePacket packet)
    {
        Console.WriteLine($"Handshake received:");
        Console.WriteLine($"  Protocol Version: {packet.ProtocolVersion}");
        Console.WriteLine($"  Server Address: {packet.ServerAddress}");
        Console.WriteLine($"  Server Port: {packet.ServerPort}");
        Console.WriteLine($"  Intent: {packet.Intent} ({(packet.Intent == 1 ? "Status" : packet.Intent == 2 ? "Login" : "Transfer")})");
        
        // Update state based on intent
        if (packet.Intent == 2) // Login
        {
            connection.SetState(ConnectionState.Login);
            Console.WriteLine($"  → State transition: HANDSHAKING → LOGIN");
        }
        else if (packet.Intent == 1) // Status
        {
            connection.SetState(ConnectionState.Status);
            Console.WriteLine($"  → State transition: HANDSHAKING → STATUS");
        }
    }
}

