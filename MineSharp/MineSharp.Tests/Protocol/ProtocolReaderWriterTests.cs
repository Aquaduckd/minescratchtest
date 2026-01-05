using MineSharp.Core.Protocol;
using System;
using System.Text;
using Xunit;

namespace MineSharp.Tests.Protocol;

public class ProtocolReaderWriterTests
{
    [Fact]
    public void ReadString_ValidString_ReturnsCorrectValue()
    {
        var testCases = new[]
        {
            ("localhost", new byte[] { 0x09, 0x6C, 0x6F, 0x63, 0x61, 0x6C, 0x68, 0x6F, 0x73, 0x74 }),
            ("test", new byte[] { 0x04, 0x74, 0x65, 0x73, 0x74 }),
            ("", new byte[] { 0x00 })
        };

        foreach (var (expected, data) in testCases)
        {
            var reader = new ProtocolReader(data);
            var result = reader.ReadString();
            Assert.Equal(expected, result);
        }
    }

    [Fact]
    public void WriteString_RoundTrip_MatchesOriginal()
    {
        var testStrings = new[] { "localhost", "test", "ClemenPine", "minecraft:overworld", "" };

        foreach (var original in testStrings)
        {
            var writer = new ProtocolWriter();
            writer.WriteString(original);
            var encoded = writer.ToArray();

            var reader = new ProtocolReader(encoded);
            var decoded = reader.ReadString();

            Assert.Equal(original, decoded);
        }
    }

    [Fact]
    public void ReadUuid_ValidUuid_ReturnsCorrectValue()
    {
        // Test UUID from packet log: 670fb6ce-0b55-448f-a9a9-ed3f1da93508
        var uuidBytes = new byte[]
        {
            0x67, 0x0F, 0xB6, 0xCE, 0x0B, 0x55, 0x44, 0x8F,
            0xA9, 0xA9, 0xED, 0x3F, 0x1D, 0xA9, 0x35, 0x08
        };
        var expectedUuid = Guid.Parse("670fb6ce-0b55-448f-a9a9-ed3f1da93508");

        var reader = new ProtocolReader(uuidBytes);
        var result = reader.ReadUuid();

        Assert.Equal(expectedUuid, result);
    }

    [Fact]
    public void WriteUuid_RoundTrip_MatchesOriginal()
    {
        var testUuids = new[]
        {
            Guid.Parse("670fb6ce-0b55-448f-a9a9-ed3f1da93508"),
            Guid.NewGuid(),
            Guid.Empty
        };

        foreach (var original in testUuids)
        {
            var writer = new ProtocolWriter();
            writer.WriteUuid(original);
            var encoded = writer.ToArray();

            var reader = new ProtocolReader(encoded);
            var decoded = reader.ReadUuid();

            Assert.Equal(original, decoded);
        }
    }

    [Fact]
    public void ReadInt_ValidData_ReturnsCorrectValue()
    {
        // Test big-endian int
        var data = new byte[] { 0x12, 0x34, 0x56, 0x78 };
        var reader = new ProtocolReader(data);
        var result = reader.ReadInt();

        // 0x12345678 = 305419896
        Assert.Equal(0x12345678, result);
    }

    [Fact]
    public void WriteInt_RoundTrip_MatchesOriginal()
    {
        var testValues = new[] { 0, 1, -1, 2147483647, -2147483648, 0x12345678 };

        foreach (var original in testValues)
        {
            var writer = new ProtocolWriter();
            writer.WriteInt(original);
            var encoded = writer.ToArray();

            var reader = new ProtocolReader(encoded);
            var decoded = reader.ReadInt();

            Assert.Equal(original, decoded);
        }
    }

    [Fact]
    public void ReadLong_ValidData_ReturnsCorrectValue()
    {
        // Test big-endian long
        var data = new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0 };
        var reader = new ProtocolReader(data);
        var result = reader.ReadLong();

        Assert.Equal(0x123456789ABCDEF0L, result);
    }

    [Fact]
    public void WriteLong_RoundTrip_MatchesOriginal()
    {
        var testValues = new long[] { 0, 1, -1, long.MaxValue, long.MinValue, 0x123456789ABCDEF0L };

        foreach (var original in testValues)
        {
            var writer = new ProtocolWriter();
            writer.WriteLong(original);
            var encoded = writer.ToArray();

            var reader = new ProtocolReader(encoded);
            var decoded = reader.ReadLong();

            Assert.Equal(original, decoded);
        }
    }

    [Fact]
    public void ReadFloat_ValidData_ReturnsCorrectValue()
    {
        // Test float value 1.0
        var data = new byte[] { 0x3F, 0x80, 0x00, 0x00 };
        var reader = new ProtocolReader(data);
        var result = reader.ReadFloat();

        Assert.Equal(1.0f, result);
    }

    [Fact]
    public void WriteFloat_RoundTrip_MatchesOriginal()
    {
        var testValues = new float[] { 0.0f, 1.0f, -1.0f, 3.14159f, float.MaxValue, float.MinValue };

        foreach (var original in testValues)
        {
            var writer = new ProtocolWriter();
            writer.WriteFloat(original);
            var encoded = writer.ToArray();

            var reader = new ProtocolReader(encoded);
            var decoded = reader.ReadFloat();

            Assert.Equal(original, decoded);
        }
    }

    [Fact]
    public void ReadDouble_ValidData_ReturnsCorrectValue()
    {
        // Test double value 1.0
        var data = new byte[] { 0x3F, 0xF0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        var reader = new ProtocolReader(data);
        var result = reader.ReadDouble();

        Assert.Equal(1.0, result);
    }

    [Fact]
    public void WriteDouble_RoundTrip_MatchesOriginal()
    {
        var testValues = new double[] { 0.0, 1.0, -1.0, 3.14159, double.MaxValue, double.MinValue };

        foreach (var original in testValues)
        {
            var writer = new ProtocolWriter();
            writer.WriteDouble(original);
            var encoded = writer.ToArray();

            var reader = new ProtocolReader(encoded);
            var decoded = reader.ReadDouble();

            Assert.Equal(original, decoded);
        }
    }

    [Fact]
    public void ReadBool_ValidData_ReturnsCorrectValue()
    {
        var reader1 = new ProtocolReader(new byte[] { 0x00 });
        Assert.False(reader1.ReadBool());

        var reader2 = new ProtocolReader(new byte[] { 0x01 });
        Assert.True(reader2.ReadBool());
    }

    [Fact]
    public void WriteBool_RoundTrip_MatchesOriginal()
    {
        var writer1 = new ProtocolWriter();
        writer1.WriteBool(false);
        var encoded1 = writer1.ToArray();
        var reader1 = new ProtocolReader(encoded1);
        Assert.False(reader1.ReadBool());

        var writer2 = new ProtocolWriter();
        writer2.WriteBool(true);
        var encoded2 = writer2.ToArray();
        var reader2 = new ProtocolReader(encoded2);
        Assert.True(reader2.ReadBool());
    }

    [Fact]
    public void ReadUnsignedShort_ValidData_ReturnsCorrectValue()
    {
        var data = new byte[] { 0x12, 0x34 };
        var reader = new ProtocolReader(data);
        var result = reader.ReadUnsignedShort();

        Assert.Equal(0x1234, result);
    }

    [Fact]
    public void WriteUnsignedShort_RoundTrip_MatchesOriginal()
    {
        var testValues = new ushort[] { 0, 1, 255, 256, ushort.MaxValue };

        foreach (var original in testValues)
        {
            var writer = new ProtocolWriter();
            writer.WriteUnsignedShort(original);
            var encoded = writer.ToArray();

            var reader = new ProtocolReader(encoded);
            var decoded = reader.ReadUnsignedShort();

            Assert.Equal(original, decoded);
        }
    }
}

