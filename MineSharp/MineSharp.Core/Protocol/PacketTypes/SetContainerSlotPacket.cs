using MineSharp.Core.DataTypes;

namespace MineSharp.Core.Protocol.PacketTypes;

/// <summary>
/// Set Container Slot packet structure (0x14, clientbound).
/// Sent when the server updates a single slot in a container.
/// </summary>
public class SetContainerSlotPacket
{
    /// <summary>
    /// Window ID (0 = player inventory, always open).
    /// -1 = cursor slot only.
    /// </summary>
    public byte WindowId { get; set; }
    
    /// <summary>
    /// Inventory state ID for synchronization.
    /// </summary>
    public int StateId { get; set; }
    
    /// <summary>
    /// Slot index (-1 = not in window, cursor slot only).
    /// </summary>
    public short Slot { get; set; }
    
    /// <summary>
    /// New slot contents.
    /// </summary>
    public SlotData SlotData { get; set; }

    public SetContainerSlotPacket()
    {
        SlotData = SlotData.Empty;
    }
}




