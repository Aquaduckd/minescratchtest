namespace MineSharp.Core.Protocol;

/// <summary>
/// Packet direction in the Minecraft protocol.
/// </summary>
public enum PacketDirection
{
    Clientbound = 0,  // Server -> Client
    Serverbound = 1   // Client -> Server
}

