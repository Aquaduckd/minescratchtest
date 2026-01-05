namespace MineSharp.Core.Protocol.PacketTypes;

/// <summary>
/// Set Player Position packet structure (0x1D, serverbound).
/// </summary>
public class SetPlayerPositionPacket
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}

