using MineSharp.Core;
using MineSharp.Core.Protocol;
using MineSharp.Core.Protocol.PacketTypes;
using System;
using System.Collections.Generic;
using Xunit;

namespace MineSharp.Tests.Protocol;

public class ConfigurationPacketParserTests
{
    [Fact]
    public void ParseKnownPacks_EmptyList_ParsesCorrectly()
    {
        // Arrange - Build packet with empty known packs list
        var writer = new ProtocolWriter();
        writer.WriteVarInt(0); // Count = 0
        
        var packetData = writer.ToArray();
        var fullPacket = BuildFullPacket(0x07, packetData); // Serverbound Known Packs

        // Act
        var (packetId, packet) = PacketParser.ParsePacket(fullPacket, ConnectionState.Configuration);

        // Assert
        Assert.Equal(0x07, packetId);
        Assert.NotNull(packet);
        var packs = Assert.IsType<List<object>>(packet);
        Assert.Empty(packs);
    }

    [Fact]
    public void ParseKnownPacks_SinglePack_ParsesCorrectly()
    {
        // Arrange
        var writer = new ProtocolWriter();
        writer.WriteVarInt(1); // Count = 1
        writer.WriteString("minecraft");
        writer.WriteString("core");
        writer.WriteString("1.0.0");
        
        var packetData = writer.ToArray();
        var fullPacket = BuildFullPacket(0x07, packetData);

        // Act
        var (packetId, packet) = PacketParser.ParsePacket(fullPacket, ConnectionState.Configuration);

        // Assert
        Assert.Equal(0x07, packetId);
        Assert.NotNull(packet);
        var packs = Assert.IsType<List<object>>(packet);
        Assert.Single(packs);
        
        var pack = Assert.IsType<List<string>>(packs[0]);
        Assert.Equal(3, pack.Count);
        Assert.Equal("minecraft", pack[0]);
        Assert.Equal("core", pack[1]);
        Assert.Equal("1.0.0", pack[2]);
    }

    [Fact]
    public void ParseKnownPacks_MultiplePacks_ParsesCorrectly()
    {
        // Arrange
        var writer = new ProtocolWriter();
        writer.WriteVarInt(2); // Count = 2
        // Pack 1
        writer.WriteString("minecraft");
        writer.WriteString("core");
        writer.WriteString("1.0.0");
        // Pack 2
        writer.WriteString("custom");
        writer.WriteString("modpack");
        writer.WriteString("2.5.1");
        
        var packetData = writer.ToArray();
        var fullPacket = BuildFullPacket(0x07, packetData);

        // Act
        var (packetId, packet) = PacketParser.ParsePacket(fullPacket, ConnectionState.Configuration);

        // Assert
        Assert.Equal(0x07, packetId);
        var packs = Assert.IsType<List<object>>(packet);
        Assert.Equal(2, packs.Count);
        
        var pack1 = Assert.IsType<List<string>>(packs[0]);
        Assert.Equal("minecraft", pack1[0]);
        Assert.Equal("core", pack1[1]);
        Assert.Equal("1.0.0", pack1[2]);
        
        var pack2 = Assert.IsType<List<string>>(packs[1]);
        Assert.Equal("custom", pack2[0]);
        Assert.Equal("modpack", pack2[1]);
        Assert.Equal("2.5.1", pack2[2]);
    }

    [Fact]
    public void ParsePluginMessage_ConfigurationState_ParsesCorrectly()
    {
        // Arrange - Plugin Message (packet ID 2) in Configuration state
        var channel = "minecraft:brand";
        var data = new byte[] { 0x01, 0x02, 0x03 };
        
        var writer = new ProtocolWriter();
        writer.WriteString(channel);
        writer.WriteBytes(data);
        
        var packetData = writer.ToArray();
        var fullPacket = BuildFullPacket(2, packetData);

        // Act
        var (packetId, packet) = PacketParser.ParsePacket(fullPacket, ConnectionState.Configuration);

        // Assert
        Assert.Equal(2, packetId);
        Assert.NotNull(packet);
        var dict = Assert.IsType<Dictionary<string, object>>(packet);
        Assert.Equal(channel, dict["channel"]);
        var packetDataBytes = Assert.IsType<byte[]>(dict["data"]);
        Assert.Equal(data, packetDataBytes);
    }

    [Fact]
    public void ParsePluginMessage_EmptyData_ParsesCorrectly()
    {
        // Arrange
        var channel = "minecraft:brand";
        var writer = new ProtocolWriter();
        writer.WriteString(channel);
        // No data bytes
        
        var packetData = writer.ToArray();
        var fullPacket = BuildFullPacket(2, packetData);

        // Act
        var (packetId, packet) = PacketParser.ParsePacket(fullPacket, ConnectionState.Configuration);

        // Assert
        Assert.Equal(2, packetId);
        var dict = Assert.IsType<Dictionary<string, object>>(packet);
        Assert.Equal(channel, dict["channel"]);
        var packetDataBytes = Assert.IsType<byte[]>(dict["data"]);
        Assert.Empty(packetDataBytes);
    }

    [Fact]
    public void ParseAcknowledgeFinishConfiguration_EmptyPacket_ParsesCorrectly()
    {
        // Arrange - Acknowledge Finish Configuration (packet ID 3) is an empty packet
        var fullPacket = BuildFullPacket(3, Array.Empty<byte>());

        // Act
        var (packetId, packet) = PacketParser.ParsePacket(fullPacket, ConnectionState.Configuration);

        // Assert
        Assert.Equal(3, packetId);
        Assert.Null(packet); // Empty packet returns null
    }

    private static byte[] BuildFullPacket(int packetId, byte[] packetData)
    {
        var writer = new ProtocolWriter();
        writer.WriteVarInt(packetData.Length);
        writer.WriteVarInt(packetId);
        writer.WriteBytes(packetData);
        return writer.ToArray();
    }
}

