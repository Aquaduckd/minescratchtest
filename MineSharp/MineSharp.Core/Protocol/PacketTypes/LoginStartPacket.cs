namespace MineSharp.Core.Protocol.PacketTypes;

/// <summary>
/// Login Start packet structure.
/// </summary>
public class LoginStartPacket
{
    public string Username { get; set; } = string.Empty;
    public Guid PlayerUuid { get; set; }
}

