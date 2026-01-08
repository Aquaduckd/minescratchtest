using MineSharp.Core.DataTypes;
using Xunit;

namespace MineSharp.Tests.Core.DataTypes;

public class PositionTests
{
    [Fact]
    public void FromLong_DecodesPositionCorrectly()
    {
        // Test case: Position (0, 64, 0)
        // X = 0, Z = 0, Y = 64
        // Encoded: (0 << 38) | (0 << 12) | 64 = 64
        long encoded = 64;
        
        var position = Position.FromLong(encoded);
        
        Assert.Equal(0, position.X);
        Assert.Equal(64, position.Y);
        Assert.Equal(0, position.Z);
    }
    
    [Fact]
    public void FromLong_DecodesNegativeY()
    {
        // Test case: Position (0, -64, 0)
        // X = 0, Z = 0, Y = -64
        // -64 in 12-bit signed: 0xFC0 (bits 0-11, with sign bit set)
        // Encoded: (0 << 38) | (0 << 12) | 0xFC0 = 0xFC0
        long encoded = 0xFC0;
        
        var position = Position.FromLong(encoded);
        
        Assert.Equal(0, position.X);
        Assert.Equal(-64, position.Y);
        Assert.Equal(0, position.Z);
    }
    
    [Fact]
    public void FromLong_DecodesPositiveCoordinates()
    {
        // Test case: Position (100, 64, 200)
        // X = 100, Z = 200, Y = 64
        long encoded = ((long)100 << 38) | ((long)200 << 12) | 64;
        
        var position = Position.FromLong(encoded);
        
        Assert.Equal(100, position.X);
        Assert.Equal(64, position.Y);
        Assert.Equal(200, position.Z);
    }
    
    [Fact]
    public void FromLong_DecodesNegativeCoordinates()
    {
        // Test case: Position (-100, -64, -200)
        // Need to encode as two's complement in the bit fields
        // X = -100: 26-bit signed representation
        // Z = -200: 26-bit signed representation  
        // Y = -64: 12-bit signed representation
        int x26 = -100 & 0x3FFFFFF; // Keep 26 bits
        int z26 = -200 & 0x3FFFFFF;
        int y12 = -64 & 0xFFF;
        
        long encoded = ((long)x26 << 38) | ((long)z26 << 12) | y12;
        
        var position = Position.FromLong(encoded);
        
        Assert.Equal(-100, position.X);
        Assert.Equal(-64, position.Y);
        Assert.Equal(-200, position.Z);
    }

    [Fact]
    public void ToLong_EncodesPositionCorrectly()
    {
        // Test round-trip: encode then decode
        var position = new Position(0, 64, 0);
        long encoded = position.ToLong();
        var decoded = Position.FromLong(encoded);
        
        Assert.Equal(position.X, decoded.X);
        Assert.Equal(position.Y, decoded.Y);
        Assert.Equal(position.Z, decoded.Z);
    }

    [Fact]
    public void ToLong_EncodesNegativeCoordinates()
    {
        // Test round-trip with negative coordinates
        var position = new Position(-100, -64, -200);
        long encoded = position.ToLong();
        var decoded = Position.FromLong(encoded);
        
        Assert.Equal(position.X, decoded.X);
        Assert.Equal(position.Y, decoded.Y);
        Assert.Equal(position.Z, decoded.Z);
    }
}



