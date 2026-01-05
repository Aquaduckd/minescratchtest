namespace MineSharp.Core.Protocol.PacketTypes;

/// <summary>
/// Set Player Rotation packet structure (0x1F, serverbound).
/// </summary>
public class SetPlayerRotationPacket
{
    public float Yaw { get; set; }
    public float Pitch { get; set; }
}

