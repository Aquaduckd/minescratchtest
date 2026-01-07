using MineSharp.Core.Protocol;
using Xunit;

namespace MineSharp.Tests.Protocol;

public class FixedBitSetTests
{
    [Fact]
    public void WriteFixedBitSet_SingleBit_EncodesCorrectly()
    {
        // Arrange
        var writer = new ProtocolWriter();
        int bits = 0x01; // Bit 0 set
        int numBits = 6; // 6 bits = 1 byte

        // Act
        writer.WriteFixedBitSet(bits, numBits);
        byte[] result = writer.ToArray();

        // Assert
        Assert.Single(result);
        Assert.Equal(0x01, result[0]); // Bit 0 should be set
    }

    [Fact]
    public void WriteFixedBitSet_MultipleBits_EncodesCorrectly()
    {
        // Arrange
        var writer = new ProtocolWriter();
        int bits = 0x01 | 0x04; // Bits 0 and 2 set (Add Player + Update Game Mode)
        int numBits = 6; // 6 bits = 1 byte

        // Act
        writer.WriteFixedBitSet(bits, numBits);
        byte[] result = writer.ToArray();

        // Assert
        Assert.Single(result);
        Assert.Equal(0x05, result[0]); // Bits 0 and 2 set = 0x01 | 0x04 = 0x05
    }

    [Fact]
    public void WriteFixedBitSet_AllBits_EncodesCorrectly()
    {
        // Arrange
        var writer = new ProtocolWriter();
        int bits = 0x3F; // All 6 bits set (0x01 | 0x02 | 0x04 | 0x08 | 0x10 | 0x20)
        int numBits = 6; // 6 bits = 1 byte

        // Act
        writer.WriteFixedBitSet(bits, numBits);
        byte[] result = writer.ToArray();

        // Assert
        Assert.Single(result);
        Assert.Equal(0x3F, result[0]);
    }

    [Fact]
    public void WriteFixedBitSet_NoBits_EncodesCorrectly()
    {
        // Arrange
        var writer = new ProtocolWriter();
        int bits = 0x00; // No bits set
        int numBits = 6; // 6 bits = 1 byte

        // Act
        writer.WriteFixedBitSet(bits, numBits);
        byte[] result = writer.ToArray();

        // Assert
        Assert.Single(result);
        Assert.Equal(0x00, result[0]);
    }

    [Fact]
    public void WriteFixedBitSet_MultipleBytes_EncodesCorrectly()
    {
        // Arrange
        var writer = new ProtocolWriter();
        int bits = 0x01 | 0x100; // Bit 0 and bit 8 set
        int numBits = 10; // 10 bits = ceil(10/8) = 2 bytes

        // Act
        writer.WriteFixedBitSet(bits, numBits);
        byte[] result = writer.ToArray();

        // Assert
        Assert.Equal(2, result.Length);
        Assert.Equal(0x01, result[0]); // First byte: bit 0 set
        Assert.Equal(0x01, result[1]); // Second byte: bit 0 set (which is bit 8 overall)
    }
}

