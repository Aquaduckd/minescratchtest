namespace MineSharp.Core.Protocol.PacketTypes;

/// <summary>
/// Client Information packet structure (Configuration state).
/// </summary>
public class ClientInformationPacket
{
    public string Locale { get; set; } = string.Empty;
    public sbyte ViewDistance { get; set; }
    public int ChatMode { get; set; }
    public bool ChatColors { get; set; }
    public byte DisplayedSkinParts { get; set; }
    public int MainHand { get; set; }
    public bool EnableTextFiltering { get; set; }
    public bool AllowServerListings { get; set; }
}

