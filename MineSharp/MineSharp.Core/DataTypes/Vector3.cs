namespace MineSharp.Core.DataTypes;

/// <summary>
/// Represents a 3D vector with floating-point coordinates.
/// </summary>
public struct Vector3
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }

    public Vector3(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }
}

