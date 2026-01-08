using MineSharp.Core;
using MineSharp.Core.Protocol;
using MineSharp.Core.Protocol.PacketTypes;
using MineSharp.Network.Handlers;

namespace MineSharp.Network;

/// <summary>
/// Routes packets to appropriate handlers based on connection state.
/// </summary>
public class PacketHandler
{
    private readonly HandshakingHandler _handshakingHandler;
    private readonly LoginHandler _loginHandler;
    private readonly ConfigurationHandler _configurationHandler;
    private readonly PlayHandler _playHandler;

    public PlayHandler PlayHandler => _playHandler;

    public PacketHandler(
        HandshakingHandler handshakingHandler,
        LoginHandler loginHandler,
        ConfigurationHandler configurationHandler,
        PlayHandler playHandler)
    {
        _handshakingHandler = handshakingHandler;
        _loginHandler = loginHandler;
        _configurationHandler = configurationHandler;
        _playHandler = playHandler;
    }

    public async Task HandlePacketAsync(ClientConnection connection, int packetId, object? packet, byte[] rawPacket)
    {
        var state = connection.State;
        
        if (state == ConnectionState.Handshaking)
        {
            if (packetId == 0 && packet is HandshakePacket handshake)
            {
                await _handshakingHandler.HandleHandshakeAsync(connection, handshake);
            }
        }
        else if (state == ConnectionState.Login)
        {
            if (packetId == 0 && packet is LoginStartPacket loginStart)
            {
                await _loginHandler.HandleLoginStartAsync(connection, loginStart);
            }
            else if (packetId == 3) // Login Acknowledged
            {
                connection.SetState(ConnectionState.Configuration);
                Console.WriteLine("  → State transition: LOGIN → CONFIGURATION");
                
                // Send configuration packets
                await _configurationHandler.SendKnownPacksAsync(connection);
                await _configurationHandler.SendAllRegistryDataAsync(connection);
                await _configurationHandler.SendFinishConfigurationAsync(connection);
            }
        }
        else if (state == ConnectionState.Configuration)
        {
            if (packetId == 0 && packet is ClientInformationPacket clientInfo)
            {
                await _configurationHandler.HandleClientInformationAsync(connection, clientInfo);
            }
            else if (packetId == 3) // Acknowledge Finish Configuration
            {
                connection.SetState(ConnectionState.Play);
                Console.WriteLine("  → State transition: CONFIGURATION → PLAY");
                
                // Send initial PLAY state packets
                await _playHandler.SendInitialPlayPacketsAsync(connection);
                
                // Start keep alive thread (send every 10 seconds)
                connection.StartKeepAlive(_playHandler, intervalSeconds: 10);
                Console.WriteLine("  │  ✓ Keep Alive thread started (interval: 10s)");
            }
        }
        else if (state == ConnectionState.Play)
        {
            if (packetId == 0x1B && packet is KeepAlivePacket keepAlive) // Serverbound Keep Alive
            {
                await _playHandler.HandleKeepAliveAsync(connection, keepAlive);
            }
            else if (packetId == 0x1D && packet is SetPlayerPositionPacket positionPacket) // Set Player Position
            {
                await _playHandler.HandleSetPlayerPositionAsync(connection, positionPacket);
            }
            else if (packetId == 0x1E && packet is SetPlayerPositionAndRotationPacket positionRotationPacket) // Set Player Position and Rotation
            {
                await _playHandler.HandleSetPlayerPositionAndRotationAsync(connection, positionRotationPacket);
            }
            else if (packetId == 0x1F && packet is SetPlayerRotationPacket rotationPacket) // Set Player Rotation
            {
                await _playHandler.HandleSetPlayerRotationAsync(connection, rotationPacket);
            }
            else if (packetId == 0x28 && packet is PlayerActionPacket playerActionPacket) // Player Action
            {
                await _playHandler.HandlePlayerActionAsync(connection, playerActionPacket);
            }
            else if (packetId == 0x3F && packet is UseItemOnPacket useItemOnPacket) // Use Item On
            {
                await _playHandler.HandleUseItemOnAsync(connection, useItemOnPacket);
            }
            else if (packetId == 0x10 && packet is ClickContainerButtonPacket clickButtonPacket) // Click Container Button
            {
                await _playHandler.HandleClickContainerButtonAsync(connection, clickButtonPacket);
            }
            else if (packetId == 0x11 && packet is ClickContainerPacket clickContainerPacket) // Click Container
            {
                await _playHandler.HandleClickContainerAsync(connection, clickContainerPacket);
            }
            else if (packetId == 0x12 && packet is CloseContainerPacket closeContainerPacket) // Close Container (serverbound)
            {
                await _playHandler.HandleCloseContainerAsync(connection, closeContainerPacket);
            }
            else if (packetId == 0x34 && packet is SetHeldItemPacket setHeldItemPacket) // Set Held Item
            {
                await _playHandler.HandleSetHeldItemAsync(connection, setHeldItemPacket);
            }
            else if (packetId == 0x37 && packet is SetCreativeModeSlotPacket creativeSlotPacket) // Set Creative Mode Slot
            {
                await _playHandler.HandleSetCreativeModeSlotAsync(connection, creativeSlotPacket);
            }
            else if (packetId == 0x3C && packet is SwingArmPacket swingArmPacket) // Swing Arm
            {
                await _playHandler.HandleSwingArmAsync(connection, swingArmPacket);
            }
            else
            {
                // TODO: Handle other PLAY state packets
                // Suppress logging for packet 0x0C (likely keep-alive or similar frequent packet)
                if (packetId != 0x0C)
                {
                    Console.WriteLine($"  → PLAY state packet (ID: 0x{packetId:X2}) - not yet implemented");
                }
            }
        }
    }
}

