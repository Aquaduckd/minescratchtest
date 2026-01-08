namespace MineSharp.Core.Protocol.PacketTypes;

/// <summary>
/// Set Held Item packet structure (0x34, serverbound).
/// Sent when a player changes their selected hotbar slot.
/// </summary>
public class SetHeldItemPacket
{
    /// <summary>
    /// Hotbar slot index (0-8).
    /// </summary>
    public short Slot { get; set; }
}

