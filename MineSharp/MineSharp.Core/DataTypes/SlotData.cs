namespace MineSharp.Core.DataTypes;

/// <summary>
/// Represents a slot in an inventory (can be empty or contain an item).
/// This is the protocol representation used in packets.
/// </summary>
public class SlotData
{
    /// <summary>
    /// Whether the slot has an item.
    /// </summary>
    public bool Present { get; set; }

    /// <summary>
    /// Item/block ID (only valid if Present is true).
    /// </summary>
    public int ItemId { get; set; }

    /// <summary>
    /// Stack size (1-64 or up to 127 for certain items).
    /// Only valid if Present is true.
    /// </summary>
    public byte ItemCount { get; set; }

    /// <summary>
    /// Optional NBT data (only valid if Present is true).
    /// Currently stored as raw bytes, can be parsed later if needed.
    /// </summary>
    public byte[]? Nbt { get; set; } // Kept for compatibility, but modern protocol uses components

    private SlotData()
    {
        Present = false;
        ItemId = 0;
        ItemCount = 0;
        Nbt = null;
    }

    /// <summary>
    /// Creates an empty slot.
    /// </summary>
    public static SlotData Empty => new SlotData();

    /// <summary>
    /// Creates a slot with an item.
    /// </summary>
    public SlotData(int itemId, byte itemCount, byte[]? nbt = null)
    {
        Present = true;
        ItemId = itemId;
        ItemCount = itemCount;
        Nbt = nbt;
    }

    /// <summary>
    /// Checks if the slot is empty.
    /// </summary>
    public bool IsEmpty => !Present;
}

