using MineSharp.Core;
using MineSharp.Core.Protocol;
using MineSharp.Core.Protocol.PacketTypes;

namespace MineSharp.Network.Handlers;

/// <summary>
/// Handles packets in the Login state.
/// </summary>
public class LoginHandler
{
    public async Task HandleLoginStartAsync(ClientConnection connection, LoginStartPacket packet)
    {
        Console.WriteLine($"Login Start received:");
        Console.WriteLine($"  Username: {packet.Username}");
        Console.WriteLine($"  Player UUID: {packet.PlayerUuid}");
        
        // Send Login Success response
        await SendLoginSuccessAsync(connection, packet.PlayerUuid, packet.Username);
        
        // Transition to CONFIGURATION state (will happen after client sends Login Acknowledged)
        Console.WriteLine($"  → Waiting for Login Acknowledged...");
    }

    public async Task SendLoginSuccessAsync(ClientConnection connection, Guid uuid, string username)
    {
        Console.WriteLine($"  → Sending Login Success response...");
        
        try
        {
            var loginSuccess = PacketBuilder.BuildLoginSuccessPacket(uuid, username, new List<object>());
            await connection.SendPacketAsync(loginSuccess);
            Console.WriteLine($"  ✓ Login Success sent ({loginSuccess.Length} bytes)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ Error sending Login Success: {ex.Message}");
            throw;
        }
    }
}

