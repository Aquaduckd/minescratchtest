namespace MineSharp.Core.Protocol.PacketTypes;

/// <summary>
/// Set Player Position and Rotation packet structure (0x1E, serverbound).
/// </summary>
public class SetPlayerPositionAndRotationPacket
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public float Yaw { get; set; }
    public float Pitch { get; set; }
}

