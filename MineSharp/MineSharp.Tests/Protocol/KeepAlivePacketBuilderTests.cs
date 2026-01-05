using MineSharp.Core.Protocol;
using Xunit;

namespace MineSharp.Tests.Protocol;

public class KeepAlivePacketBuilderTests
{
    [Fact]
    public void BuildKeepAlivePacket_ValidId_BuildsCorrectly()
    {
        // Arrange
        long keepAliveId = 1767646451661L;

        // Act
        var packet = PacketBuilder.BuildKeepAlivePacket(keepAliveId);

        // Assert
        Assert.NotNull(packet);
        Assert.True(packet.Length > 0);

        // Parse the packet to verify structure
        var reader = new ProtocolReader(packet);
        var packetLength = reader.ReadVarInt();
        var packetId = reader.ReadVarInt();
        var readKeepAliveId = reader.ReadLong();

        Assert.Equal(0x2B, packetId); // Keep Alive packet ID (clientbound)
        Assert.Equal(keepAliveId, readKeepAliveId);
        Assert.Equal(0, reader.Remaining); // Should have consumed all bytes
    }

    [Fact]
    public void BuildKeepAlivePacket_ZeroId_BuildsCorrectly()
    {
        // Arrange
        long keepAliveId = 0L;

        // Act
        var packet = PacketBuilder.BuildKeepAlivePacket(keepAliveId);

        // Assert
        var reader = new ProtocolReader(packet);
        reader.ReadVarInt(); // Skip length
        var packetId = reader.ReadVarInt();
        var readKeepAliveId = reader.ReadLong();

        Assert.Equal(0x2B, packetId);
        Assert.Equal(keepAliveId, readKeepAliveId);
    }

    [Fact]
    public void BuildKeepAlivePacket_TimestampId_BuildsCorrectly()
    {
        // Arrange - Use timestamp in milliseconds (common pattern)
        long keepAliveId = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Act
        var packet = PacketBuilder.BuildKeepAlivePacket(keepAliveId);

        // Assert
        var reader = new ProtocolReader(packet);
        reader.ReadVarInt(); // Skip length
        var packetId = reader.ReadVarInt();
        var readKeepAliveId = reader.ReadLong();

        Assert.Equal(0x2B, packetId);
        Assert.Equal(keepAliveId, readKeepAliveId);
    }

    [Fact]
    public void BuildKeepAlivePacket_LargeId_BuildsCorrectly()
    {
        // Arrange
        long keepAliveId = long.MaxValue;

        // Act
        var packet = PacketBuilder.BuildKeepAlivePacket(keepAliveId);

        // Assert
        var reader = new ProtocolReader(packet);
        reader.ReadVarInt(); // Skip length
        var packetId = reader.ReadVarInt();
        var readKeepAliveId = reader.ReadLong();

        Assert.Equal(0x2B, packetId);
        Assert.Equal(keepAliveId, readKeepAliveId);
    }

    [Fact]
    public void BuildKeepAlivePacket_NegativeId_BuildsCorrectly()
    {
        // Arrange
        long keepAliveId = -1L;

        // Act
        var packet = PacketBuilder.BuildKeepAlivePacket(keepAliveId);

        // Assert
        var reader = new ProtocolReader(packet);
        reader.ReadVarInt(); // Skip length
        var packetId = reader.ReadVarInt();
        var readKeepAliveId = reader.ReadLong();

        Assert.Equal(0x2B, packetId);
        Assert.Equal(keepAliveId, readKeepAliveId);
    }

    [Fact]
    public void BuildKeepAlivePacket_RoundTrip_CanBeParsed()
    {
        // Arrange
        long keepAliveId = 1234567890123L;

        // Act
        var packet = PacketBuilder.BuildKeepAlivePacket(keepAliveId);

        // Assert - verify we can parse it back
        var reader = new ProtocolReader(packet);
        var packetLength = reader.ReadVarInt();
        var packetId = reader.ReadVarInt();
        var readKeepAliveId = reader.ReadLong();

        Assert.Equal(0x2B, packetId);
        Assert.Equal(keepAliveId, readKeepAliveId);
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void BuildKeepAlivePacket_MatchesExpectedFromPacketLog()
    {
        // Arrange - From packet log: keep_alive_id: 1767646451661
        // Packet log hex: 092b0000019b8ff057cd
        // This is: length=9, packet_id=0x2B, keep_alive_id=0x0000019b8ff057cd
        long keepAliveId = 1767646451661L;

        // Act
        var packet = PacketBuilder.BuildKeepAlivePacket(keepAliveId);

        // Assert - Verify packet structure matches expected format
        var reader = new ProtocolReader(packet);
        var packetLength = reader.ReadVarInt();
        var packetId = reader.ReadVarInt();
        var readKeepAliveId = reader.ReadLong();

        Assert.Equal(0x2B, packetId);
        Assert.Equal(keepAliveId, readKeepAliveId);
        
        // Verify packet length matches packet log (9 bytes for packet data)
        Assert.Equal(9, packetLength); // 1 byte VarInt (0x2B) + 8 bytes Long
        
        // Verify total packet length (including length prefix)
        // Length prefix (9) = 1 byte VarInt, so total should be 10 bytes
        Assert.Equal(10, packet.Length);
    }
}

