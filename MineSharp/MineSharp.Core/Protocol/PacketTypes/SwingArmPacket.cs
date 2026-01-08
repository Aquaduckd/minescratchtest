namespace MineSharp.Core.Protocol.PacketTypes;

/// <summary>
/// Swing Arm packet structure (0x3C, serverbound).
/// Sent when the player's arm swings.
/// </summary>
public class SwingArmPacket
{
    /// <summary>
    /// Hand used for the animation.
    /// 0 = Main hand, 1 = Off hand
    /// </summary>
    public int Hand { get; set; }
}

