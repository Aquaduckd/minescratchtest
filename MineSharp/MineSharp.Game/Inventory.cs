namespace MineSharp.Game;

/// <summary>
/// Represents a player's inventory.
/// </summary>
public class Inventory
{
    private readonly Dictionary<int, int> _items;  // item_id -> count
    private readonly Dictionary<int, (int itemId, int count)> _slots;  // slot_index -> (item_id, count)
    private int _inventoryStateId;
    private int _selectedHotbarSlot;
    private (int itemId, int count)? _cursorItem;

    public Inventory()
    {
        _items = new Dictionary<int, int>();
        _slots = new Dictionary<int, (int, int)>();
        _inventoryStateId = 0;
        _selectedHotbarSlot = 0;
        _cursorItem = null;
    }

    public int InventoryStateId => _inventoryStateId;
    public int SelectedHotbarSlot => _selectedHotbarSlot;
    public (int itemId, int count)? CursorItem => _cursorItem;

    public void SetSlot(int slotIndex, int itemId, int count)
    {
        // TODO: Implement slot setting
        throw new NotImplementedException();
    }

    public (int itemId, int count)? GetSlot(int slotIndex)
    {
        // TODO: Implement slot getting
        throw new NotImplementedException();
    }

    public void SetSelectedHotbarSlot(int slot)
    {
        // TODO: Implement selected hotbar slot setting
        throw new NotImplementedException();
    }

    public void SetCursorItem(int itemId, int count)
    {
        // TODO: Implement cursor item setting
        throw new NotImplementedException();
    }

    public void ClearCursorItem()
    {
        // TODO: Implement cursor item clearing
        throw new NotImplementedException();
    }

    public void IncrementStateId()
    {
        // TODO: Implement state ID increment
        throw new NotImplementedException();
    }
}

