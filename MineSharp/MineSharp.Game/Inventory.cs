namespace MineSharp.Game;

/// <summary>
/// Represents a player's inventory.
/// Slot numbering:
/// - 0: Crafting output
/// - 1-4: 2x2 crafting grid
/// - 5-8: Armor slots (5=boots, 6=leggings, 7=chestplate, 8=helmet)
/// - 9-35: Main inventory (27 slots)
/// - 36-44: Hotbar (9 slots)
/// </summary>
public class Inventory
{
    // Slot constants
    public const int CRAFTING_OUTPUT_SLOT = 0;
    public const int CRAFTING_START_SLOT = 1;
    public const int CRAFTING_END_SLOT = 4;
    public const int ARMOR_START_SLOT = 5;
    public const int ARMOR_END_SLOT = 8;
    public const int MAIN_INVENTORY_START_SLOT = 9;
    public const int MAIN_INVENTORY_END_SLOT = 35;
    public const int HOTBAR_START_SLOT = 36;
    public const int HOTBAR_END_SLOT = 44;
    public const int TOTAL_SLOTS = 45;

    // Slot ranges
    public const int ARMOR_BOOTS_SLOT = 5;
    public const int ARMOR_LEGGINGS_SLOT = 6;
    public const int ARMOR_CHESTPLATE_SLOT = 7;
    public const int ARMOR_HELMET_SLOT = 8;

    private readonly ItemStack?[] _slots;  // slot_index -> ItemStack (null = empty)
    private int _inventoryStateId;
    private int _selectedHotbarSlot;  // 0-8 (relative to hotbar, maps to slot 36-44)
    private ItemStack? _cursorItem;
    private readonly object _lock = new object();

    public Inventory()
    {
        _slots = new ItemStack?[TOTAL_SLOTS];
        _inventoryStateId = 0;
        _selectedHotbarSlot = 0;  // Default to first hotbar slot (slot 36)
        _cursorItem = null;
    }

    /// <summary>
    /// Gets the current inventory state ID.
    /// </summary>
    public int InventoryStateId
    {
        get
        {
            lock (_lock)
            {
                return _inventoryStateId;
            }
        }
    }

    /// <summary>
    /// Gets the selected hotbar slot (0-8, relative to hotbar).
    /// Maps to slot 36-44 in the inventory.
    /// </summary>
    public int SelectedHotbarSlot
    {
        get
        {
            lock (_lock)
            {
                return _selectedHotbarSlot;
            }
        }
    }

    /// <summary>
    /// Gets the actual inventory slot index for the selected hotbar slot.
    /// </summary>
    public int SelectedHotbarSlotIndex => HOTBAR_START_SLOT + SelectedHotbarSlot;

    /// <summary>
    /// Gets the cursor item (item currently held by cursor).
    /// </summary>
    public ItemStack? CursorItem
    {
        get
        {
            lock (_lock)
            {
                return _cursorItem;
            }
        }
    }

    /// <summary>
    /// Sets the contents of a slot.
    /// </summary>
    /// <param name="slotIndex">Slot index (0-44).</param>
    /// <param name="itemId">Item ID (0 for empty).</param>
    /// <param name="count">Stack size (ignored if itemId is 0).</param>
    public void SetSlot(int slotIndex, int itemId, int count)
    {
        if (!IsValidSlot(slotIndex))
            throw new ArgumentOutOfRangeException(nameof(slotIndex), $"Slot index must be between 0 and {TOTAL_SLOTS - 1}");

        lock (_lock)
        {
            if (itemId == 0 || count <= 0)
            {
                _slots[slotIndex] = null;
            }
            else
            {
                _slots[slotIndex] = new ItemStack(itemId, (byte)Math.Min(count, 127));
            }
            
            IncrementStateId();
        }
    }

    /// <summary>
    /// Sets the contents of a slot using an ItemStack.
    /// </summary>
    /// <param name="slotIndex">Slot index (0-44).</param>
    /// <param name="itemStack">ItemStack to set (null for empty).</param>
    public void SetSlot(int slotIndex, ItemStack? itemStack)
    {
        if (!IsValidSlot(slotIndex))
            throw new ArgumentOutOfRangeException(nameof(slotIndex), $"Slot index must be between 0 and {TOTAL_SLOTS - 1}");

        lock (_lock)
        {
            _slots[slotIndex] = itemStack;
            IncrementStateId();
        }
    }

    /// <summary>
    /// Gets the contents of a slot.
    /// </summary>
    /// <param name="slotIndex">Slot index (0-44).</param>
    /// <returns>ItemStack in the slot, or null if empty.</returns>
    public ItemStack? GetSlot(int slotIndex)
    {
        if (!IsValidSlot(slotIndex))
            throw new ArgumentOutOfRangeException(nameof(slotIndex), $"Slot index must be between 0 and {TOTAL_SLOTS - 1}");

        lock (_lock)
        {
            return _slots[slotIndex];
        }
    }

    /// <summary>
    /// Gets the item currently held in the selected hotbar slot.
    /// </summary>
    /// <returns>ItemStack in the selected hotbar slot, or null if empty.</returns>
    public ItemStack? GetHeldItem()
    {
        lock (_lock)
        {
            int slotIndex = HOTBAR_START_SLOT + _selectedHotbarSlot;
            return _slots[slotIndex];
        }
    }

    /// <summary>
    /// Sets the selected hotbar slot.
    /// </summary>
    /// <param name="slot">Hotbar slot (0-8, relative to hotbar).</param>
    public void SetSelectedHotbarSlot(int slot)
    {
        if (slot < 0 || slot > 8)
            throw new ArgumentOutOfRangeException(nameof(slot), "Hotbar slot must be between 0 and 8");

        lock (_lock)
        {
            _selectedHotbarSlot = slot;
            IncrementStateId();
        }
    }

    /// <summary>
    /// Sets the cursor item (item currently held by cursor).
    /// </summary>
    /// <param name="itemId">Item ID (0 for empty).</param>
    /// <param name="count">Stack size (ignored if itemId is 0).</param>
    public void SetCursorItem(int itemId, int count)
    {
        lock (_lock)
        {
            if (itemId == 0 || count <= 0)
            {
                _cursorItem = null;
            }
            else
            {
                _cursorItem = new ItemStack(itemId, (byte)Math.Min(count, 127));
            }
        }
    }

    /// <summary>
    /// Sets the cursor item using an ItemStack.
    /// </summary>
    /// <param name="itemStack">ItemStack to set (null for empty).</param>
    public void SetCursorItem(ItemStack? itemStack)
    {
        lock (_lock)
        {
            _cursorItem = itemStack;
        }
    }

    /// <summary>
    /// Clears the cursor item.
    /// </summary>
    public void ClearCursorItem()
    {
        lock (_lock)
        {
            _cursorItem = null;
        }
    }

    /// <summary>
    /// Increments the inventory state ID.
    /// Called automatically on inventory changes.
    /// </summary>
    public void IncrementStateId()
    {
        lock (_lock)
        {
            _inventoryStateId++;
            if (_inventoryStateId < 0)
                _inventoryStateId = 0; // Wrap around if overflow
        }
    }

    /// <summary>
    /// Checks if a slot index is valid.
    /// </summary>
    public static bool IsValidSlot(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < TOTAL_SLOTS;
    }

    /// <summary>
    /// Checks if a slot is in the crafting area (slots 0-4).
    /// </summary>
    public static bool IsCraftingSlot(int slotIndex)
    {
        return slotIndex >= CRAFTING_OUTPUT_SLOT && slotIndex <= CRAFTING_END_SLOT;
    }

    /// <summary>
    /// Checks if a slot is an armor slot (slots 5-8).
    /// </summary>
    public static bool IsArmorSlot(int slotIndex)
    {
        return slotIndex >= ARMOR_START_SLOT && slotIndex <= ARMOR_END_SLOT;
    }

    /// <summary>
    /// Checks if a slot is in the main inventory (slots 9-35).
    /// </summary>
    public static bool IsMainInventorySlot(int slotIndex)
    {
        return slotIndex >= MAIN_INVENTORY_START_SLOT && slotIndex <= MAIN_INVENTORY_END_SLOT;
    }

    /// <summary>
    /// Checks if a slot is in the hotbar (slots 36-44).
    /// </summary>
    public static bool IsHotbarSlot(int slotIndex)
    {
        return slotIndex >= HOTBAR_START_SLOT && slotIndex <= HOTBAR_END_SLOT;
    }

    /// <summary>
    /// Converts a hotbar slot index (0-8) to the actual inventory slot index (36-44).
    /// </summary>
    public static int HotbarSlotToIndex(int hotbarSlot)
    {
        if (hotbarSlot < 0 || hotbarSlot > 8)
            throw new ArgumentOutOfRangeException(nameof(hotbarSlot), "Hotbar slot must be between 0 and 8");
        return HOTBAR_START_SLOT + hotbarSlot;
    }

    /// <summary>
    /// Converts an inventory slot index (36-44) to a hotbar slot index (0-8).
    /// </summary>
    public static int IndexToHotbarSlot(int slotIndex)
    {
        if (!IsHotbarSlot(slotIndex))
            throw new ArgumentException($"Slot {slotIndex} is not a hotbar slot", nameof(slotIndex));
        return slotIndex - HOTBAR_START_SLOT;
    }
}

