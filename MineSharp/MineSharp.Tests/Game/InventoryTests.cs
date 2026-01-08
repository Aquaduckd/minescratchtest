using MineSharp.Game;
using System;
using Xunit;

namespace MineSharp.Tests.Game;

public class InventoryTests
{
    [Fact]
    public void Constructor_Should_Create_Empty_Inventory()
    {
        var inventory = new Inventory();
        
        Assert.Equal(0, inventory.InventoryStateId);
        Assert.Equal(0, inventory.SelectedHotbarSlot);
        Assert.Equal(Inventory.HOTBAR_START_SLOT, inventory.SelectedHotbarSlotIndex);
        Assert.Null(inventory.CursorItem);
    }

    [Fact]
    public void SetSlot_Should_Set_Slot_Contents()
    {
        var inventory = new Inventory();
        
        inventory.SetSlot(36, 10, 64);
        
        var slot = inventory.GetSlot(36);
        Assert.NotNull(slot);
        Assert.Equal(10, slot!.ItemId);
        Assert.Equal(64, slot.Count);
    }

    [Fact]
    public void SetSlot_With_Zero_ItemId_Should_Clear_Slot()
    {
        var inventory = new Inventory();
        inventory.SetSlot(36, 10, 64);
        
        inventory.SetSlot(36, 0, 0);
        
        Assert.Null(inventory.GetSlot(36));
    }

    [Fact]
    public void SetSlot_With_ItemStack_Should_Set_Slot()
    {
        var inventory = new Inventory();
        var itemStack = new ItemStack(10, 64);
        
        inventory.SetSlot(36, itemStack);
        
        var slot = inventory.GetSlot(36);
        Assert.NotNull(slot);
        Assert.Equal(10, slot!.ItemId);
        Assert.Equal(64, slot.Count);
    }

    [Fact]
    public void SetSlot_With_Null_Should_Clear_Slot()
    {
        var inventory = new Inventory();
        inventory.SetSlot(36, 10, 64);
        
        inventory.SetSlot(36, null);
        
        Assert.Null(inventory.GetSlot(36));
    }

    [Fact]
    public void SetSlot_Should_Throw_For_Invalid_Slot()
    {
        var inventory = new Inventory();
        
        Assert.Throws<ArgumentOutOfRangeException>(() => inventory.SetSlot(-1, 10, 64));
        Assert.Throws<ArgumentOutOfRangeException>(() => inventory.SetSlot(45, 10, 64));
    }

    [Fact]
    public void GetSlot_Should_Return_Null_For_Empty_Slot()
    {
        var inventory = new Inventory();
        
        Assert.Null(inventory.GetSlot(36));
    }

    [Fact]
    public void GetSlot_Should_Throw_For_Invalid_Slot()
    {
        var inventory = new Inventory();
        
        Assert.Throws<ArgumentOutOfRangeException>(() => inventory.GetSlot(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => inventory.GetSlot(45));
    }

    [Fact]
    public void SetSelectedHotbarSlot_Should_Change_Selected_Slot()
    {
        var inventory = new Inventory();
        
        inventory.SetSelectedHotbarSlot(5);
        
        Assert.Equal(5, inventory.SelectedHotbarSlot);
        Assert.Equal(Inventory.HOTBAR_START_SLOT + 5, inventory.SelectedHotbarSlotIndex);
    }

    [Fact]
    public void SetSelectedHotbarSlot_Should_Throw_For_Invalid_Slot()
    {
        var inventory = new Inventory();
        
        Assert.Throws<ArgumentOutOfRangeException>(() => inventory.SetSelectedHotbarSlot(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => inventory.SetSelectedHotbarSlot(9));
    }

    [Fact]
    public void GetHeldItem_Should_Return_Item_In_Selected_Hotbar_Slot()
    {
        var inventory = new Inventory();
        inventory.SetSlot(36, 10, 64); // Hotbar slot 0
        inventory.SetSelectedHotbarSlot(0);
        
        var held = inventory.GetHeldItem();
        
        Assert.NotNull(held);
        Assert.Equal(10, held!.ItemId);
        Assert.Equal(64, held.Count);
    }

    [Fact]
    public void GetHeldItem_Should_Return_Null_If_Selected_Slot_Is_Empty()
    {
        var inventory = new Inventory();
        inventory.SetSelectedHotbarSlot(0);
        
        Assert.Null(inventory.GetHeldItem());
    }

    [Fact]
    public void GetHeldItem_Should_Return_Correct_Item_After_Slot_Change()
    {
        var inventory = new Inventory();
        inventory.SetSlot(36, 10, 64); // Hotbar slot 0
        inventory.SetSlot(37, 20, 32); // Hotbar slot 1
        inventory.SetSelectedHotbarSlot(0);
        
        var held1 = inventory.GetHeldItem();
        Assert.NotNull(held1);
        Assert.Equal(10, held1!.ItemId);
        
        inventory.SetSelectedHotbarSlot(1);
        var held2 = inventory.GetHeldItem();
        Assert.NotNull(held2);
        Assert.Equal(20, held2!.ItemId);
    }

    [Fact]
    public void SetCursorItem_Should_Set_Cursor_Item()
    {
        var inventory = new Inventory();
        
        inventory.SetCursorItem(10, 64);
        
        var cursor = inventory.CursorItem;
        Assert.NotNull(cursor);
        Assert.Equal(10, cursor!.ItemId);
        Assert.Equal(64, cursor.Count);
    }

    [Fact]
    public void SetCursorItem_With_Zero_Should_Clear_Cursor()
    {
        var inventory = new Inventory();
        inventory.SetCursorItem(10, 64);
        
        inventory.SetCursorItem(0, 0);
        
        Assert.Null(inventory.CursorItem);
    }

    [Fact]
    public void SetCursorItem_With_ItemStack_Should_Set_Cursor()
    {
        var inventory = new Inventory();
        var itemStack = new ItemStack(10, 64);
        
        inventory.SetCursorItem(itemStack);
        
        Assert.Equal(itemStack, inventory.CursorItem);
    }

    [Fact]
    public void ClearCursorItem_Should_Clear_Cursor()
    {
        var inventory = new Inventory();
        inventory.SetCursorItem(10, 64);
        
        inventory.ClearCursorItem();
        
        Assert.Null(inventory.CursorItem);
    }

    [Fact]
    public void IncrementStateId_Should_Increment_State_Id()
    {
        var inventory = new Inventory();
        var initialId = inventory.InventoryStateId;
        
        inventory.IncrementStateId();
        
        Assert.Equal(initialId + 1, inventory.InventoryStateId);
    }

    [Fact]
    public void SetSlot_Should_Increment_State_Id()
    {
        var inventory = new Inventory();
        var initialId = inventory.InventoryStateId;
        
        inventory.SetSlot(36, 10, 64);
        
        Assert.Equal(initialId + 1, inventory.InventoryStateId);
    }

    [Fact]
    public void SetSelectedHotbarSlot_Should_Increment_State_Id()
    {
        var inventory = new Inventory();
        var initialId = inventory.InventoryStateId;
        
        inventory.SetSelectedHotbarSlot(5);
        
        Assert.Equal(initialId + 1, inventory.InventoryStateId);
    }

    [Fact]
    public void IncrementStateId_Should_Increment_Properly()
    {
        var inventory = new Inventory();
        // Just verify it increments normally
        inventory.IncrementStateId();
        Assert.True(inventory.InventoryStateId > 0);
    }

    [Theory]
    [InlineData(0, true)]  // Crafting output
    [InlineData(2, true)]  // Crafting grid
    [InlineData(4, true)]  // Crafting grid
    [InlineData(5, false)] // Armor (not crafting)
    [InlineData(36, false)] // Hotbar (not crafting)
    public void IsCraftingSlot_Should_Return_Correct_Value(int slot, bool expected)
    {
        Assert.Equal(expected, Inventory.IsCraftingSlot(slot));
    }

    [Theory]
    [InlineData(5, true)]   // Boots
    [InlineData(6, true)]   // Leggings
    [InlineData(7, true)]   // Chestplate
    [InlineData(8, true)]   // Helmet
    [InlineData(4, false)]  // Crafting (not armor)
    [InlineData(36, false)] // Hotbar (not armor)
    public void IsArmorSlot_Should_Return_Correct_Value(int slot, bool expected)
    {
        Assert.Equal(expected, Inventory.IsArmorSlot(slot));
    }

    [Theory]
    [InlineData(9, true)]   // Start of main inventory
    [InlineData(35, true)]  // End of main inventory
    [InlineData(22, true)]  // Middle of main inventory
    [InlineData(8, false)]  // Armor (not main)
    [InlineData(36, false)] // Hotbar (not main)
    public void IsMainInventorySlot_Should_Return_Correct_Value(int slot, bool expected)
    {
        Assert.Equal(expected, Inventory.IsMainInventorySlot(slot));
    }

    [Theory]
    [InlineData(36, true)]  // Start of hotbar
    [InlineData(44, true)]  // End of hotbar
    [InlineData(40, true)]  // Middle of hotbar
    [InlineData(35, false)] // Main inventory (not hotbar)
    [InlineData(0, false)]  // Crafting (not hotbar)
    public void IsHotbarSlot_Should_Return_Correct_Value(int slot, bool expected)
    {
        Assert.Equal(expected, Inventory.IsHotbarSlot(slot));
    }

    [Theory]
    [InlineData(0, 36)]
    [InlineData(8, 44)]
    [InlineData(4, 40)]
    public void HotbarSlotToIndex_Should_Convert_Correctly(int hotbarSlot, int expectedIndex)
    {
        Assert.Equal(expectedIndex, Inventory.HotbarSlotToIndex(hotbarSlot));
    }

    [Theory]
    [InlineData(36, 0)]
    [InlineData(44, 8)]
    [InlineData(40, 4)]
    public void IndexToHotbarSlot_Should_Convert_Correctly(int index, int expectedHotbarSlot)
    {
        Assert.Equal(expectedHotbarSlot, Inventory.IndexToHotbarSlot(index));
    }

    [Fact]
    public void IndexToHotbarSlot_Should_Throw_For_Non_Hotbar_Slot()
    {
        Assert.Throws<ArgumentException>(() => Inventory.IndexToHotbarSlot(35));
        Assert.Throws<ArgumentException>(() => Inventory.IndexToHotbarSlot(0));
    }

    [Fact]
    public void HotbarSlotToIndex_Should_Throw_For_Invalid_Slot()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Inventory.HotbarSlotToIndex(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Inventory.HotbarSlotToIndex(9));
    }

    [Fact]
    public void IsValidSlot_Should_Return_True_For_Valid_Slots()
    {
        Assert.True(Inventory.IsValidSlot(0));
        Assert.True(Inventory.IsValidSlot(44));
        Assert.True(Inventory.IsValidSlot(22));
    }

    [Fact]
    public void IsValidSlot_Should_Return_False_For_Invalid_Slots()
    {
        Assert.False(Inventory.IsValidSlot(-1));
        Assert.False(Inventory.IsValidSlot(45));
        Assert.False(Inventory.IsValidSlot(100));
    }

    [Fact]
    public void SelectedHotbarSlotIndex_Should_Map_To_Correct_Slot()
    {
        var inventory = new Inventory();
        inventory.SetSelectedHotbarSlot(3);
        
        Assert.Equal(39, inventory.SelectedHotbarSlotIndex); // 36 + 3
    }

    [Fact]
    public void Multiple_Operations_Should_Maintain_Consistency()
    {
        var inventory = new Inventory();
        
        // Set multiple slots
        inventory.SetSlot(36, 10, 64);
        inventory.SetSlot(37, 20, 32);
        inventory.SetSelectedHotbarSlot(1);
        inventory.SetCursorItem(30, 16);
        
        // Verify all operations
        Assert.Equal(20, inventory.GetSlot(37)!.ItemId);
        Assert.Equal(20, inventory.GetHeldItem()!.ItemId);
        Assert.Equal(30, inventory.CursorItem!.ItemId);
        Assert.Equal(1, inventory.SelectedHotbarSlot);
    }
}




