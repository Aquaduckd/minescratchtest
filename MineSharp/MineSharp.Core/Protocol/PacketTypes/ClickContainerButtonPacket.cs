namespace MineSharp.Core.Protocol.PacketTypes;

/// <summary>
/// Click Container Button packet structure (0x10, serverbound).
/// Sent when a player clicks a button in a container (e.g., recipe book, enchantment table).
/// </summary>
public class ClickContainerButtonPacket
{
    /// <summary>
    /// Window ID (0 = player inventory).
    /// </summary>
    public byte WindowId { get; set; }
    
    /// <summary>
    /// Button ID (varies by container type).
    /// </summary>
    public byte ButtonId { get; set; }
}

