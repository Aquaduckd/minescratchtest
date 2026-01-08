using MineSharp.Core.DataTypes;

namespace MineSharp.Core.Protocol.PacketTypes;

/// <summary>
/// Set Creative Mode Slot packet structure (0x37, serverbound).
/// Sent when a player in creative mode changes their inventory (including creative menu clicks).
/// </summary>
public class SetCreativeModeSlotPacket
{
    /// <summary>
    /// Slot index (-1 = cursor slot, 0-45 = inventory slots).
    /// </summary>
    public short Slot { get; set; }
    
    /// <summary>
    /// Item to set in the slot.
    /// </summary>
    public SlotData SlotData { get; set; }
    
    public SetCreativeModeSlotPacket()
    {
        SlotData = SlotData.Empty;
    }
}

