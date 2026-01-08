using MineSharp.Core.DataTypes;

namespace MineSharp.Core.Protocol.PacketTypes;

/// <summary>
/// Use Item On packet structure (0x3F, serverbound).
/// Sent when a player right-clicks on a block (placing a block).
/// </summary>
public class UseItemOnPacket
{
    public Position Location { get; set; }
    public int Face { get; set; }  // Face of the block (0-5: bottom, top, north, south, west, east)
    public int Hand { get; set; }  // 0=Main Hand, 1=Off Hand
    public float CursorPositionX { get; set; }  // Cursor position on block face (0.0 to 1.0)
    public float CursorPositionY { get; set; }
    public float CursorPositionZ { get; set; }
    public bool InsideBlock { get; set; }  // Whether the click was inside the block bounds
    public int Sequence { get; set; }  // Block change sequence number
}




