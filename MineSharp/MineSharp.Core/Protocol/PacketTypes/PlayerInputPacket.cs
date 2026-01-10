namespace MineSharp.Core.Protocol.PacketTypes;

/// <summary>
/// Player Input packet (0x2A, serverbound).
/// Sent whenever the player presses or releases certain keys (WASD, Shift, Space, Ctrl).
/// Used for minecart controls, player inputs in predicates, and sneaking.
/// </summary>
public class PlayerInputPacket
{
    /// <summary>
    /// Bit mask flags: 0x01=Forward, 0x02=Backward, 0x04=Left, 0x08=Right, 0x10=Jump, 0x20=Sneak, 0x40=Sprint
    /// </summary>
    public byte Flags { get; set; }
    
    /// <summary>
    /// Checks if the Forward flag (0x01) is set.
    /// </summary>
    public bool IsForward => (Flags & 0x01) != 0;
    
    /// <summary>
    /// Checks if the Backward flag (0x02) is set.
    /// </summary>
    public bool IsBackward => (Flags & 0x02) != 0;
    
    /// <summary>
    /// Checks if the Left flag (0x04) is set.
    /// </summary>
    public bool IsLeft => (Flags & 0x04) != 0;
    
    /// <summary>
    /// Checks if the Right flag (0x08) is set.
    /// </summary>
    public bool IsRight => (Flags & 0x08) != 0;
    
    /// <summary>
    /// Checks if the Jump flag (0x10) is set.
    /// </summary>
    public bool IsJump => (Flags & 0x10) != 0;
    
    /// <summary>
    /// Checks if the Sneak flag (0x20) is set.
    /// </summary>
    public bool IsSneak => (Flags & 0x20) != 0;
    
    /// <summary>
    /// Checks if the Sprint flag (0x40) is set.
    /// </summary>
    public bool IsSprint => (Flags & 0x40) != 0;
}

