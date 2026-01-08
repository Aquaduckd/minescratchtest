namespace MineSharp.Core.Protocol.PacketTypes;

/// <summary>
/// Close Container packet structure (0x12, serverbound).
/// Sent when a player closes an inventory window.
/// </summary>
public class CloseContainerPacket
{
    /// <summary>
    /// Window ID (0 = player inventory).
    /// </summary>
    public byte WindowId { get; set; }
}

