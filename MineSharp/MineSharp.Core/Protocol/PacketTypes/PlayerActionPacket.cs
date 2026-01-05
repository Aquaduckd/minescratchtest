using MineSharp.Core.DataTypes;

namespace MineSharp.Core.Protocol.PacketTypes;

/// <summary>
/// Player Action packet structure (0x28, serverbound).
/// </summary>
public class PlayerActionPacket
{
    public int Status { get; set; }  // 0=Started digging, 1=Cancelled, 2=Finished digging
    public Position Location { get; set; }
    public byte Face { get; set; }  // Face being hit (0-5)
    public int Sequence { get; set; }  // Block change sequence number
}

