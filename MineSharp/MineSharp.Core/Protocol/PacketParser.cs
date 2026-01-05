using MineSharp.Core.Protocol.PacketTypes;
using System.Collections.Generic;

namespace MineSharp.Core.Protocol;

/// <summary>
/// Parses incoming packets from raw bytes.
/// </summary>
public class PacketParser
{
    public static (int packetId, object? packet) ParsePacket(byte[] data, ConnectionState state)
    {
        var reader = new ProtocolReader(data);
        
        // Read packet length and ID
        var packetLength = reader.ReadVarInt();
        var packetId = reader.ReadVarInt();
        
        // Parse based on state and packet ID
        if (state == ConnectionState.Handshaking)
        {
            if (packetId == 0) // Handshake
            {
                return (packetId, ParseHandshake(reader));
            }
        }
        else if (state == ConnectionState.Login)
        {
            if (packetId == 0) // Login Start
            {
                return (packetId, ParseLoginStart(reader));
            }
            else if (packetId == 3) // Login Acknowledged
            {
                return (packetId, null); // Empty packet
            }
        }
        else if (state == ConnectionState.Configuration)
        {
            if (packetId == 0) // Client Information
            {
                return (packetId, ParseClientInformation(reader));
            }
            else if (packetId == 2) // Plugin Message (Configuration)
            {
                var channel = reader.ReadString();
                var remainingLength = reader.Remaining;
                var pluginData = remainingLength > 0 ? reader.ReadBytes(remainingLength) : Array.Empty<byte>();
                return (packetId, new Dictionary<string, object> { { "channel", channel }, { "data", pluginData } });
            }
            else if (packetId == 3) // Acknowledge Finish Configuration
            {
                return (packetId, null); // Empty packet
            }
            else if (packetId == 0x07) // Serverbound Known Packs
            {
                return (packetId, ParseKnownPacks(reader));
            }
        }
        else if (state == ConnectionState.Play)
        {
            if (packetId == 0x1B) // Serverbound Keep Alive
            {
                return (packetId, ParseKeepAlive(reader));
            }
            else if (packetId == 0x1D) // Set Player Position
            {
                return (packetId, ParseSetPlayerPosition(reader));
            }
            else if (packetId == 0x1E) // Set Player Position and Rotation
            {
                return (packetId, ParseSetPlayerPositionAndRotation(reader));
            }
        }
        
        // Unknown packet
        return (packetId, null);
    }
    
    private static HandshakePacket ParseHandshake(ProtocolReader reader)
    {
        var protocolVersion = reader.ReadVarInt();
        var serverAddress = reader.ReadString(255);
        var serverPort = reader.ReadUnsignedShort();
        var intent = reader.ReadVarInt();
        
        return new HandshakePacket
        {
            ProtocolVersion = protocolVersion,
            ServerAddress = serverAddress,
            ServerPort = serverPort,
            Intent = intent
        };
    }
    
    private static LoginStartPacket ParseLoginStart(ProtocolReader reader)
    {
        var username = reader.ReadString(16);
        var playerUuid = reader.ReadUuid();
        
        return new LoginStartPacket
        {
            Username = username,
            PlayerUuid = playerUuid
        };
    }
    
    private static ClientInformationPacket ParseClientInformation(ProtocolReader reader)
    {
        var locale = reader.ReadString(16);
        var viewDistance = (sbyte)reader.ReadByte();
        var chatMode = reader.ReadVarInt();
        var chatColors = reader.ReadBool();
        var displayedSkinParts = reader.ReadByte();
        var mainHand = reader.ReadVarInt();
        var enableTextFiltering = reader.ReadBool();
        var allowServerListings = reader.ReadBool();
        
        return new ClientInformationPacket
        {
            Locale = locale,
            ViewDistance = viewDistance,
            ChatMode = chatMode,
            ChatColors = chatColors,
            DisplayedSkinParts = displayedSkinParts,
            MainHand = mainHand,
            EnableTextFiltering = enableTextFiltering,
            AllowServerListings = allowServerListings
        };
    }
    
    private static List<object> ParseKnownPacks(ProtocolReader reader)
    {
        var count = reader.ReadVarInt();
        var packs = new List<object>();
        
        for (int i = 0; i < count; i++)
        {
            var @namespace = reader.ReadString();
            var packId = reader.ReadString();
            var version = reader.ReadString();
            packs.Add(new List<string> { @namespace, packId, version });
        }
        
        return packs;
    }
    
    private static KeepAlivePacket ParseKeepAlive(ProtocolReader reader)
    {
        var keepAliveId = reader.ReadLong();
        
        return new KeepAlivePacket
        {
            KeepAliveId = keepAliveId
        };
    }
    
    private static SetPlayerPositionPacket ParseSetPlayerPosition(ProtocolReader reader)
    {
        var x = reader.ReadDouble();
        var y = reader.ReadDouble();
        var z = reader.ReadDouble();
        
        return new SetPlayerPositionPacket
        {
            X = x,
            Y = y,
            Z = z
        };
    }
    
    private static SetPlayerPositionAndRotationPacket ParseSetPlayerPositionAndRotation(ProtocolReader reader)
    {
        var x = reader.ReadDouble();
        var y = reader.ReadDouble();
        var z = reader.ReadDouble();
        var yaw = reader.ReadFloat();
        var pitch = reader.ReadFloat();
        
        return new SetPlayerPositionAndRotationPacket
        {
            X = x,
            Y = y,
            Z = z,
            Yaw = yaw,
            Pitch = pitch
        };
    }
}

