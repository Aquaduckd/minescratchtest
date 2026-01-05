using MineSharp.Core.Protocol;
using System;
using Xunit;

namespace MineSharp.Tests.Protocol;

public class VarIntTests
{
    [Fact]
    public void ReadVarInt_Zero_ReturnsZero()
    {
        // Arrange
        var data = new byte[] { 0x00 };
        var reader = new ProtocolReader(data);

        // Act
        var result = reader.ReadVarInt();

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void ReadVarInt_SingleByte_ReturnsCorrectValue()
    {
        // Test values: 0-127 (single byte)
        var testCases = new[]
        {
            (0, new byte[] { 0x00 }),
            (1, new byte[] { 0x01 }),
            (127, new byte[] { 0x7F })
        };

        foreach (var (expected, data) in testCases)
        {
            var reader = new ProtocolReader(data);
            var result = reader.ReadVarInt();
            Assert.Equal(expected, result);
        }
    }

    [Fact]
    public void ReadVarInt_MultiByte_ReturnsCorrectValue()
    {
        // Test values: 128-2097151 (multi-byte)
        var testCases = new[]
        {
            (128, new byte[] { 0x80, 0x01 }),
            (255, new byte[] { 0xFF, 0x01 }),
            (300, new byte[] { 0xAC, 0x02 }),
            (2147483647, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x07 })
        };

        foreach (var (expected, data) in testCases)
        {
            var reader = new ProtocolReader(data);
            var result = reader.ReadVarInt();
            Assert.Equal(expected, result);
        }
    }

    [Fact]
    public void ReadVarInt_Negative_ReturnsCorrectValue()
    {
        // Test negative values (two's complement)
        var testCases = new[]
        {
            (-1, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x0F }),
            (-2147483648, new byte[] { 0x80, 0x80, 0x80, 0x80, 0x08 })
        };

        foreach (var (expected, data) in testCases)
        {
            var reader = new ProtocolReader(data);
            var result = reader.ReadVarInt();
            Assert.Equal(expected, result);
        }
    }

    [Fact]
    public void WriteVarInt_RoundTrip_MatchesOriginal()
    {
        // Test encoding then decoding returns original value
        var testValues = new[] { 0, 1, 127, 128, 255, 300, 2147483647, -1, -2147483648 };

        foreach (var original in testValues)
        {
            var writer = new ProtocolWriter();
            writer.WriteVarInt(original);
            var encoded = writer.ToArray();

            var reader = new ProtocolReader(encoded);
            var decoded = reader.ReadVarInt();

            Assert.Equal(original, decoded);
        }
    }

    [Fact]
    public void ReadVarInt_InvalidData_ThrowsException()
    {
        // Test incomplete VarInt
        var incomplete = new byte[] { 0x80 };
        var reader = new ProtocolReader(incomplete);
        Assert.Throws<InvalidOperationException>(() => reader.ReadVarInt());
    }

    [Fact]
    public void ReadVarLong_Zero_ReturnsZero()
    {
        // Arrange
        var data = new byte[] { 0x00 };
        var reader = new ProtocolReader(data);

        // Act
        var result = reader.ReadVarLong();

        // Assert
        Assert.Equal(0L, result);
    }

    [Fact]
    public void WriteVarLong_RoundTrip_MatchesOriginal()
    {
        // Test encoding then decoding returns original value
        var testValues = new long[] { 0, 1, 127, 128, 255, 300, 2147483647, -1, -2147483648, long.MaxValue, long.MinValue };

        foreach (var original in testValues)
        {
            var writer = new ProtocolWriter();
            writer.WriteVarLong(original);
            var encoded = writer.ToArray();

            var reader = new ProtocolReader(encoded);
            var decoded = reader.ReadVarLong();

            Assert.Equal(original, decoded);
        }
    }
}

