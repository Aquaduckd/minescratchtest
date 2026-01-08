using MineSharp.Core.DataTypes;

namespace MineSharp.Game;

/// <summary>
/// Represents a stack of items in the game.
/// This is the game logic representation used internally.
/// </summary>
public class ItemStack
{
    /// <summary>
    /// Item/block ID.
    /// </summary>
    public int ItemId { get; private set; }
    
    /// <summary>
    /// Stack size (1-64 or up to 127 for certain items).
    /// </summary>
    public byte Count { get; private set; }
    
    /// <summary>
    /// Optional NBT data.
    /// Currently stored as raw bytes, can be parsed later if needed.
    /// </summary>
    public byte[]? Nbt { get; private set; }
    
    /// <summary>
    /// Item damage/durability (if applicable).
    /// </summary>
    public int? Damage { get; private set; }

    /// <summary>
    /// Creates an empty item stack.
    /// </summary>
    public ItemStack()
    {
        ItemId = 0;
        Count = 0;
        Nbt = null;
        Damage = null;
    }

    /// <summary>
    /// Creates an item stack with an item.
    /// </summary>
    public ItemStack(int itemId, byte count, byte[]? nbt = null, int? damage = null)
    {
        ItemId = itemId;
        Count = count;
        Nbt = nbt;
        Damage = damage;
    }

    /// <summary>
    /// Checks if the stack is empty.
    /// </summary>
    public bool IsEmpty => ItemId == 0 || Count == 0;

    /// <summary>
    /// Checks if this stack can be stacked with another stack.
    /// </summary>
    public bool CanStackWith(ItemStack? other)
    {
        if (other == null || other.IsEmpty || this.IsEmpty)
            return false;
        
        if (ItemId != other.ItemId)
            return false;
        
        // TODO: Compare NBT data for exact match (if needed)
        // For now, allow stacking if item IDs match
        
        return true;
    }

    /// <summary>
    /// Splits this stack into two stacks.
    /// </summary>
    /// <param name="amount">Amount to split off (will be clamped to available count).</param>
    /// <returns>A new stack with the split amount, or null if no split possible.</returns>
    public ItemStack? Split(int amount)
    {
        if (IsEmpty || amount <= 0)
            return null;
        
        // Can't split entire stack (must leave at least 1 item)
        if (Count <= 1)
            return null;
        
        // Clamp to maximum split amount (one less than current count)
        int maxSplit = Count - 1;
        int splitAmount = Math.Min(amount, maxSplit);
        
        if (splitAmount <= 0)
            return null;
        
        Count -= (byte)splitAmount;
        return new ItemStack(ItemId, (byte)splitAmount, Nbt, Damage);
    }

    /// <summary>
    /// Tries to combine another stack into this stack.
    /// </summary>
    /// <param name="other">The stack to combine with.</param>
    /// <returns>True if any items were combined, false otherwise.</returns>
    public bool TryCombine(ItemStack? other)
    {
        if (other == null || other.IsEmpty || !CanStackWith(other))
            return false;
        
        const int maxStackSize = 64; // TODO: Get max stack size from item data
        
        int availableSpace = maxStackSize - Count;
        if (availableSpace <= 0)
            return false;
        
        int combineAmount = Math.Min(availableSpace, other.Count);
        Count += (byte)combineAmount;
        other.Count -= (byte)combineAmount;
        
        if (other.Count == 0)
        {
            other.ItemId = 0;
        }
        
        return combineAmount > 0;
    }

    /// <summary>
    /// Creates an ItemStack from a SlotData.
    /// </summary>
    public static ItemStack FromSlotData(SlotData slotData)
    {
        if (slotData == null || !slotData.Present)
            return new ItemStack();
        
        return new ItemStack(slotData.ItemId, slotData.ItemCount, slotData.Nbt);
    }

    /// <summary>
    /// Converts this ItemStack to SlotData.
    /// </summary>
    public SlotData ToSlotData()
    {
        if (IsEmpty)
            return SlotData.Empty;
        
        return new SlotData(ItemId, Count, Nbt);
    }
}




