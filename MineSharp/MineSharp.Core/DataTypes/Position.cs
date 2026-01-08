namespace MineSharp.Core.DataTypes;

/// <summary>
/// Represents a 3D position in the Minecraft world.
/// </summary>
public struct Position
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }

    public Position(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public static Position FromLong(long value)
    {
        // Minecraft Position encoding:
        // X: bits 38-63 (26 bits, signed)
        // Z: bits 12-37 (26 bits, signed)
        // Y: bits 0-11 (12 bits, signed)
        
        // Extract raw values
        long xRaw = (value >> 38) & 0x3FFFFFF; // 26 bits
        long zRaw = (value >> 12) & 0x3FFFFFF; // 26 bits
        long yRaw = value & 0xFFF; // 12 bits
        
        // Sign extend from 26 bits to 32 bits for X and Z
        int x = (int)xRaw;
        if ((x & 0x2000000) != 0) // Bit 25 (26th bit, sign bit for 26-bit signed)
            x |= unchecked((int)0xFC000000); // Sign extend to 32 bits
        
        int z = (int)zRaw;
        if ((z & 0x2000000) != 0) // Bit 25 (26th bit, sign bit for 26-bit signed)
            z |= unchecked((int)0xFC000000); // Sign extend to 32 bits
        
        // Sign extend from 12 bits to 32 bits for Y
        int y = (int)yRaw;
        if ((y & 0x800) != 0) // Bit 11 (12th bit, sign bit for 12-bit signed)
            y |= unchecked((int)0xFFFFF000); // Sign extend to 32 bits
        
        return new Position(x, y, z);
    }

    public long ToLong()
    {
        // Minecraft Position encoding:
        // X: bits 38-63 (26 bits, signed)
        // Z: bits 12-37 (26 bits, signed)
        // Y: bits 0-11 (12 bits, signed)
        
        // Convert to unsigned for bit manipulation
        long x = (uint)X;
        long z = (uint)Z;
        long y = (uint)Y;
        
        // Mask to ensure we only use the required bits
        x &= 0x3FFFFFF; // 26 bits
        z &= 0x3FFFFFF; // 26 bits
        y &= 0xFFF;     // 12 bits
        
        // Combine: X (26 bits) << 38 | Z (26 bits) << 12 | Y (12 bits)
        return (x << 38) | (z << 12) | y;
    }
}

