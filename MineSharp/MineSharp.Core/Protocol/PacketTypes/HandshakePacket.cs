namespace MineSharp.Core.Protocol.PacketTypes;

/// <summary>
/// Handshake packet structure.
/// </summary>
public class HandshakePacket
{
    public int ProtocolVersion { get; set; }
    public string ServerAddress { get; set; } = string.Empty;
    public ushort ServerPort { get; set; }
    public int Intent { get; set; }  // 1=Status, 2=Login, 3=Transfer
}

