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
        // TODO: Implement position encoding/decoding
        throw new NotImplementedException();
    }

    public long ToLong()
    {
        // TODO: Implement position encoding/decoding
        throw new NotImplementedException();
    }
}

