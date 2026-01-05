namespace MineSharp.Core.Protocol.PacketTypes;

/// <summary>
/// Keep Alive packet structure (0x1B serverbound, 0x2B clientbound).
/// </summary>
public class KeepAlivePacket
{
    public long KeepAliveId { get; set; }
}

