using MineSharp.Core.DataTypes;

namespace MineSharp.Core.Protocol.PacketTypes;

/// <summary>
/// Click Container packet structure (0x11, serverbound).
/// Sent when a player clicks a slot in a container.
/// </summary>
public class ClickContainerPacket
{
    /// <summary>
    /// Window ID (0 = player inventory, non-zero = other containers like creative menu).
    /// </summary>
    public byte WindowId { get; set; }
    
    /// <summary>
    /// Inventory state ID for synchronization.
    /// </summary>
    public int StateId { get; set; }
    
    /// <summary>
    /// Slot index that was clicked (-1 = outside window).
    /// </summary>
    public short Slot { get; set; }
    
    /// <summary>
    /// Button that was clicked (0 = left, 1 = right, etc.).
    /// </summary>
    public byte Button { get; set; }
    
    /// <summary>
    /// Click mode (0 = Click, 1 = Shift+Click, 2 = Number Key, 3 = Middle Click, 4 = Drop, 5 = Drag, 6 = Double Click).
    /// </summary>
    public int Mode { get; set; }
    
    /// <summary>
    /// List of slot changes (slot index, slot data).
    /// </summary>
    public List<(short Slot, SlotData SlotData)> Slots { get; set; }
    
    /// <summary>
    /// Item currently carried by cursor.
    /// </summary>
    public SlotData CarriedItem { get; set; }
    
    public ClickContainerPacket()
    {
        Slots = new List<(short, SlotData)>();
        CarriedItem = SlotData.Empty;
    }
}

