using MineSharp.Core.Protocol;
using Xunit;

namespace MineSharp.Tests.Protocol;

public class SetCenterChunkPacketBuilderTests
{
    [Fact]
    public void BuildSetCenterChunkPacket_ValidCoordinates_BuildsCorrectly()
    {
        // Arrange
        int chunkX = 0;
        int chunkZ = 0;

        // Act
        var packet = PacketBuilder.BuildSetCenterChunkPacket(chunkX, chunkZ);

        // Assert
        Assert.NotNull(packet);
        Assert.True(packet.Length > 0);

        // Parse the packet to verify structure
        var reader = new ProtocolReader(packet);
        var packetLength = reader.ReadVarInt();
        var packetId = reader.ReadVarInt();
        var readChunkX = reader.ReadVarInt();
        var readChunkZ = reader.ReadVarInt();

        Assert.Equal(0x5C, packetId); // Set Center Chunk packet ID
        Assert.Equal(chunkX, readChunkX);
        Assert.Equal(chunkZ, readChunkZ);
        Assert.Equal(0, reader.Remaining); // Should have consumed all bytes
    }

    [Fact]
    public void BuildSetCenterChunkPacket_NegativeCoordinates_BuildsCorrectly()
    {
        // Arrange
        int chunkX = -2;
        int chunkZ = -1;

        // Act
        var packet = PacketBuilder.BuildSetCenterChunkPacket(chunkX, chunkZ);

        // Assert
        var reader = new ProtocolReader(packet);
        reader.ReadVarInt(); // Skip length
        var packetId = reader.ReadVarInt();
        var readChunkX = reader.ReadVarInt();
        var readChunkZ = reader.ReadVarInt();

        Assert.Equal(0x5C, packetId);
        Assert.Equal(chunkX, readChunkX);
        Assert.Equal(chunkZ, readChunkZ);
    }

    [Fact]
    public void BuildSetCenterChunkPacket_LargeCoordinates_BuildsCorrectly()
    {
        // Arrange
        int chunkX = 1000;
        int chunkZ = -1000;

        // Act
        var packet = PacketBuilder.BuildSetCenterChunkPacket(chunkX, chunkZ);

        // Assert
        var reader = new ProtocolReader(packet);
        reader.ReadVarInt(); // Skip length
        var packetId = reader.ReadVarInt();
        var readChunkX = reader.ReadVarInt();
        var readChunkZ = reader.ReadVarInt();

        Assert.Equal(0x5C, packetId);
        Assert.Equal(chunkX, readChunkX);
        Assert.Equal(chunkZ, readChunkZ);
    }

    [Fact]
    public void BuildSetCenterChunkPacket_RoundTrip_CanBeParsed()
    {
        // Arrange
        int chunkX = 5;
        int chunkZ = -3;

        // Act
        var packet = PacketBuilder.BuildSetCenterChunkPacket(chunkX, chunkZ);

        // Assert - verify we can parse it back
        var reader = new ProtocolReader(packet);
        var packetLength = reader.ReadVarInt();
        var packetId = reader.ReadVarInt();
        var readChunkX = reader.ReadVarInt();
        var readChunkZ = reader.ReadVarInt();

        Assert.Equal(0x5C, packetId);
        Assert.Equal(chunkX, readChunkX);
        Assert.Equal(chunkZ, readChunkZ);
        Assert.Equal(0, reader.Remaining);
    }
}

