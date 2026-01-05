using MineSharp.Core;
using MineSharp.Core.Protocol;
using MineSharp.Core.Protocol.PacketTypes;
using System;
using Xunit;

namespace MineSharp.Tests.Protocol;

public class KeepAlivePacketParserTests
{
    [Fact]
    public void ParseKeepAlivePacket_FromPacketLog_MatchesExpected()
    {
        // Arrange - From packet log: keep_alive_id: 1767646451661
        // Packet hex: 091b0000019b8ff057cd
        // This is: length=9, packet_id=0x1B, keep_alive_id=0x0000019b8ff057cd
        var packetHex = "091b0000019b8ff057cd";
        var packetBytes = ConvertHexStringToBytes(packetHex);
        var expectedKeepAliveId = 1767646451661L;

        // Act
        var (packetId, packet) = PacketParser.ParsePacket(packetBytes, ConnectionState.Play);

        // Assert
        Assert.Equal(0x1B, packetId);
        Assert.NotNull(packet);
        var keepAlive = Assert.IsType<KeepAlivePacket>(packet);
        Assert.Equal(expectedKeepAliveId, keepAlive.KeepAliveId);
    }

    [Fact]
    public void ParseKeepAlivePacket_ValidId_ParsesCorrectly()
    {
        // Arrange
        long keepAliveId = 1234567890123L;
        var packet = BuildKeepAlivePacket(0x1B, keepAliveId);

        // Act
        var (packetId, parsedPacket) = PacketParser.ParsePacket(packet, ConnectionState.Play);

        // Assert
        Assert.Equal(0x1B, packetId);
        Assert.NotNull(parsedPacket);
        var keepAlive = Assert.IsType<KeepAlivePacket>(parsedPacket);
        Assert.Equal(keepAliveId, keepAlive.KeepAliveId);
    }

    [Fact]
    public void ParseKeepAlivePacket_ZeroId_ParsesCorrectly()
    {
        // Arrange
        long keepAliveId = 0L;
        var packet = BuildKeepAlivePacket(0x1B, keepAliveId);

        // Act
        var (packetId, parsedPacket) = PacketParser.ParsePacket(packet, ConnectionState.Play);

        // Assert
        Assert.Equal(0x1B, packetId);
        var keepAlive = Assert.IsType<KeepAlivePacket>(parsedPacket);
        Assert.Equal(keepAliveId, keepAlive.KeepAliveId);
    }

    [Fact]
    public void ParseKeepAlivePacket_TimestampId_ParsesCorrectly()
    {
        // Arrange - Use timestamp in milliseconds
        long keepAliveId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var packet = BuildKeepAlivePacket(0x1B, keepAliveId);

        // Act
        var (packetId, parsedPacket) = PacketParser.ParsePacket(packet, ConnectionState.Play);

        // Assert
        Assert.Equal(0x1B, packetId);
        var keepAlive = Assert.IsType<KeepAlivePacket>(parsedPacket);
        Assert.Equal(keepAliveId, keepAlive.KeepAliveId);
    }

    [Fact]
    public void ParseKeepAlivePacket_LargeId_ParsesCorrectly()
    {
        // Arrange
        long keepAliveId = long.MaxValue;
        var packet = BuildKeepAlivePacket(0x1B, keepAliveId);

        // Act
        var (packetId, parsedPacket) = PacketParser.ParsePacket(packet, ConnectionState.Play);

        // Assert
        Assert.Equal(0x1B, packetId);
        var keepAlive = Assert.IsType<KeepAlivePacket>(parsedPacket);
        Assert.Equal(keepAliveId, keepAlive.KeepAliveId);
    }

    [Fact]
    public void ParseKeepAlivePacket_NegativeId_ParsesCorrectly()
    {
        // Arrange
        long keepAliveId = -1L;
        var packet = BuildKeepAlivePacket(0x1B, keepAliveId);

        // Act
        var (packetId, parsedPacket) = PacketParser.ParsePacket(packet, ConnectionState.Play);

        // Assert
        Assert.Equal(0x1B, packetId);
        var keepAlive = Assert.IsType<KeepAlivePacket>(parsedPacket);
        Assert.Equal(keepAliveId, keepAlive.KeepAliveId);
    }

    [Fact]
    public void ParseKeepAlivePacket_RoundTrip_CanBeParsed()
    {
        // Arrange
        long originalKeepAliveId = 9876543210987L;

        // Build packet manually
        var packet = BuildKeepAlivePacket(0x1B, originalKeepAliveId);

        // Act - Parse it back
        var (packetId, parsedPacket) = PacketParser.ParsePacket(packet, ConnectionState.Play);

        // Assert
        Assert.Equal(0x1B, packetId);
        var keepAlive = Assert.IsType<KeepAlivePacket>(parsedPacket);
        Assert.Equal(originalKeepAliveId, keepAlive.KeepAliveId);
    }

    [Fact]
    public void ParseKeepAlivePacket_WrongState_ReturnsNull()
    {
        // Arrange - Keep Alive is only valid in PLAY state
        long keepAliveId = 1234567890123L;
        var packet = BuildKeepAlivePacket(0x1B, keepAliveId);

        // Act - Try parsing in wrong state
        var (packetId, packet1) = PacketParser.ParsePacket(packet, ConnectionState.Configuration);
        var (packetId2, packet2) = PacketParser.ParsePacket(packet, ConnectionState.Login);
        var (packetId3, packet3) = PacketParser.ParsePacket(packet, ConnectionState.Handshaking);

        // Assert - Should return null for wrong states
        Assert.Null(packet1);
        Assert.Null(packet2);
        Assert.Null(packet3);
    }

    private static byte[] BuildKeepAlivePacket(int packetId, long keepAliveId)
    {
        var writer = new ProtocolWriter();
        writer.WriteVarInt(packetId);
        writer.WriteLong(keepAliveId);
        
        var packetData = writer.ToArray();
        var finalWriter = new ProtocolWriter();
        finalWriter.WriteVarInt(packetData.Length);
        finalWriter.WriteBytes(packetData);
        
        return finalWriter.ToArray();
    }

    private static byte[] ConvertHexStringToBytes(string hex)
    {
        var bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        return bytes;
    }
}

