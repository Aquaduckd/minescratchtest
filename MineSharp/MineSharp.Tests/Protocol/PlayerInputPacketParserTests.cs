using MineSharp.Core;
using MineSharp.Core.Protocol;
using MineSharp.Core.Protocol.PacketTypes;
using Xunit;

namespace MineSharp.Tests.Protocol;

public class PlayerInputPacketParserTests
{
    [Fact]
    public void ParsePlayerInputPacket_SneakFlag_Set()
    {
        // Arrange
        var writer = new ProtocolWriter();
        writer.WriteVarInt(0x2A); // Player Input packet ID
        writer.WriteByte(0x20); // Sneak flag (0x20)

        // Build packet with length prefix
        var packetData = writer.ToArray();
        var finalWriter = new ProtocolWriter();
        finalWriter.WriteVarInt(packetData.Length);
        finalWriter.WriteBytes(packetData);
        var packet = finalWriter.ToArray();

        // Act
        var (packetId, parsedPacket) = PacketParser.ParsePacket(packet, ConnectionState.Play);

        // Assert
        Assert.Equal(0x2A, packetId);
        Assert.NotNull(parsedPacket);
        Assert.IsType<PlayerInputPacket>(parsedPacket);
        
        var playerInput = (PlayerInputPacket)parsedPacket;
        Assert.Equal(0x20, playerInput.Flags);
        Assert.True(playerInput.IsSneak);
        Assert.False(playerInput.IsForward);
        Assert.False(playerInput.IsBackward);
        Assert.False(playerInput.IsJump);
        Assert.False(playerInput.IsSprint);
    }

    [Fact]
    public void ParsePlayerInputPacket_MultipleFlags_Set()
    {
        // Arrange
        var writer = new ProtocolWriter();
        writer.WriteVarInt(0x2A); // Player Input packet ID
        writer.WriteByte(0x21); // Forward (0x01) + Sneak (0x20) = 0x21

        // Build packet with length prefix
        var packetData = writer.ToArray();
        var finalWriter = new ProtocolWriter();
        finalWriter.WriteVarInt(packetData.Length);
        finalWriter.WriteBytes(packetData);
        var packet = finalWriter.ToArray();

        // Act
        var (packetId, parsedPacket) = PacketParser.ParsePacket(packet, ConnectionState.Play);

        // Assert
        Assert.Equal(0x2A, packetId);
        Assert.NotNull(parsedPacket);
        Assert.IsType<PlayerInputPacket>(parsedPacket);
        
        var playerInput = (PlayerInputPacket)parsedPacket;
        Assert.Equal(0x21, playerInput.Flags);
        Assert.True(playerInput.IsForward);
        Assert.True(playerInput.IsSneak);
        Assert.False(playerInput.IsBackward);
    }

    [Fact]
    public void ParsePlayerInputPacket_NoFlags_NoneSet()
    {
        // Arrange
        var writer = new ProtocolWriter();
        writer.WriteVarInt(0x2A); // Player Input packet ID
        writer.WriteByte(0x00); // No flags

        // Build packet with length prefix
        var packetData = writer.ToArray();
        var finalWriter = new ProtocolWriter();
        finalWriter.WriteVarInt(packetData.Length);
        finalWriter.WriteBytes(packetData);
        var packet = finalWriter.ToArray();

        // Act
        var (packetId, parsedPacket) = PacketParser.ParsePacket(packet, ConnectionState.Play);

        // Assert
        Assert.Equal(0x2A, packetId);
        Assert.NotNull(parsedPacket);
        Assert.IsType<PlayerInputPacket>(parsedPacket);
        
        var playerInput = (PlayerInputPacket)parsedPacket;
        Assert.Equal(0x00, playerInput.Flags);
        Assert.False(playerInput.IsSneak);
        Assert.False(playerInput.IsForward);
        Assert.False(playerInput.IsSprint);
    }

    [Fact]
    public void ParsePlayerInputPacket_AllFlags_Set()
    {
        // Arrange
        var writer = new ProtocolWriter();
        writer.WriteVarInt(0x2A); // Player Input packet ID
        writer.WriteByte(0x7F); // All flags: Forward+Backward+Left+Right+Jump+Sneak+Sprint

        // Build packet with length prefix
        var packetData = writer.ToArray();
        var finalWriter = new ProtocolWriter();
        finalWriter.WriteVarInt(packetData.Length);
        finalWriter.WriteBytes(packetData);
        var packet = finalWriter.ToArray();

        // Act
        var (packetId, parsedPacket) = PacketParser.ParsePacket(packet, ConnectionState.Play);

        // Assert
        Assert.Equal(0x2A, packetId);
        Assert.NotNull(parsedPacket);
        Assert.IsType<PlayerInputPacket>(parsedPacket);
        
        var playerInput = (PlayerInputPacket)parsedPacket;
        Assert.True(playerInput.IsForward);
        Assert.True(playerInput.IsBackward);
        Assert.True(playerInput.IsLeft);
        Assert.True(playerInput.IsRight);
        Assert.True(playerInput.IsJump);
        Assert.True(playerInput.IsSneak);
        Assert.True(playerInput.IsSprint);
    }
}

