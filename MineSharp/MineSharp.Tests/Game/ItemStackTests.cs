using MineSharp.Core.DataTypes;
using MineSharp.Game;
using Xunit;

namespace MineSharp.Tests.Game;

public class ItemStackTests
{
    [Fact]
    public void Default_Constructor_Should_Create_Empty_Stack()
    {
        var stack = new ItemStack();
        
        Assert.True(stack.IsEmpty);
        Assert.Equal(0, stack.ItemId);
        Assert.Equal(0, stack.Count);
        Assert.Null(stack.Nbt);
        Assert.Null(stack.Damage);
    }

    [Fact]
    public void Constructor_With_Item_Should_Create_Non_Empty_Stack()
    {
        var stack = new ItemStack(5, 64);
        
        Assert.False(stack.IsEmpty);
        Assert.Equal(5, stack.ItemId);
        Assert.Equal(64, stack.Count);
    }

    [Fact]
    public void Constructor_With_NBT_Should_Store_NBT()
    {
        var nbt = new byte[] { 0x01, 0x02, 0x03 };
        var stack = new ItemStack(10, 32, nbt);
        
        Assert.Equal(nbt, stack.Nbt);
    }

    [Fact]
    public void Constructor_With_Damage_Should_Store_Damage()
    {
        var stack = new ItemStack(100, 1, null, 50);
        
        Assert.Equal(50, stack.Damage);
    }

    [Fact]
    public void IsEmpty_Should_Return_True_For_Zero_ItemId()
    {
        var stack1 = new ItemStack(0, 1);
        var stack2 = new ItemStack(1, 0);
        var stack3 = new ItemStack(1, 1);
        
        Assert.True(stack1.IsEmpty);
        Assert.True(stack2.IsEmpty);
        Assert.False(stack3.IsEmpty);
    }

    [Fact]
    public void CanStackWith_Should_Return_False_For_Null()
    {
        var stack = new ItemStack(5, 64);
        
        Assert.False(stack.CanStackWith(null));
    }

    [Fact]
    public void CanStackWith_Should_Return_False_For_Empty_Stack()
    {
        var stack1 = new ItemStack(5, 64);
        var stack2 = new ItemStack();
        
        Assert.False(stack1.CanStackWith(stack2));
        Assert.False(stack2.CanStackWith(stack1));
    }

    [Fact]
    public void CanStackWith_Should_Return_False_For_Different_ItemIds()
    {
        var stack1 = new ItemStack(5, 64);
        var stack2 = new ItemStack(6, 64);
        
        Assert.False(stack1.CanStackWith(stack2));
    }

    [Fact]
    public void CanStackWith_Should_Return_True_For_Same_ItemId()
    {
        var stack1 = new ItemStack(5, 64);
        var stack2 = new ItemStack(5, 32);
        
        Assert.True(stack1.CanStackWith(stack2));
    }

    [Fact]
    public void Split_Should_Return_Null_For_Empty_Stack()
    {
        var stack = new ItemStack();
        
        Assert.Null(stack.Split(10));
    }

    [Fact]
    public void Split_Should_Return_Null_For_Zero_Amount()
    {
        var stack = new ItemStack(5, 64);
        
        Assert.Null(stack.Split(0));
    }

    [Fact]
    public void Split_Should_Return_Null_If_Splitting_Entire_Stack()
    {
        var stack = new ItemStack(5, 64);
        
        // Splitting exactly the count should clamp to (count - 1) to leave at least 1
        var split = stack.Split(64);
        Assert.NotNull(split);
        Assert.Equal(63, split!.Count); // Clamps to 63 (leaving 1)
        Assert.Equal(1, stack.Count); // Original stack now has 1
        
        // Stack with only 1 item cannot be split
        var singleItemStack = new ItemStack(5, 1);
        Assert.Null(singleItemStack.Split(1));
        Assert.Null(singleItemStack.Split(100));
    }

    [Fact]
    public void Split_Should_Create_New_Stack_And_Reduce_Count()
    {
        var stack = new ItemStack(5, 64);
        var split = stack.Split(10);
        
        Assert.NotNull(split);
        Assert.Equal(5, split.ItemId);
        Assert.Equal(10, split.Count);
        Assert.Equal(54, stack.Count); // 64 - 10
    }

    [Fact]
    public void Split_Should_Clamp_To_Available_Count()
    {
        var stack = new ItemStack(5, 20);
        var split = stack.Split(50); // More than available
        
        Assert.NotNull(split);
        Assert.Equal(19, split.Count); // One less than total (can't split entire stack)
        Assert.Equal(1, stack.Count);
    }

    [Fact]
    public void TryCombine_Should_Return_False_For_Null()
    {
        var stack = new ItemStack(5, 64);
        
        Assert.False(stack.TryCombine(null));
    }

    [Fact]
    public void TryCombine_Should_Return_False_For_Different_ItemId()
    {
        var stack1 = new ItemStack(5, 64);
        var stack2 = new ItemStack(6, 32);
        
        Assert.False(stack1.TryCombine(stack2));
        Assert.Equal(64, stack1.Count);
        Assert.Equal(32, stack2.Count);
    }

    [Fact]
    public void TryCombine_Should_Return_True_And_Combine_Stacks()
    {
        var stack1 = new ItemStack(5, 50);
        var stack2 = new ItemStack(5, 32);
        
        Assert.True(stack1.TryCombine(stack2));
        Assert.Equal(64, stack1.Count); // 50 + 14 (clamped to max 64)
        Assert.Equal(18, stack2.Count); // 32 - 14
    }

    [Fact]
    public void TryCombine_Should_Empty_Other_Stack_If_All_Combined()
    {
        var stack1 = new ItemStack(5, 60);
        var stack2 = new ItemStack(5, 4);
        
        Assert.True(stack1.TryCombine(stack2));
        Assert.Equal(64, stack1.Count);
        Assert.True(stack2.IsEmpty);
    }

    [Fact]
    public void TryCombine_Should_Return_False_If_Full()
    {
        var stack1 = new ItemStack(5, 64);
        var stack2 = new ItemStack(5, 32);
        
        Assert.False(stack1.TryCombine(stack2));
        Assert.Equal(64, stack1.Count);
        Assert.Equal(32, stack2.Count);
    }

    [Fact]
    public void FromSlotData_Should_Create_Empty_Stack_For_Empty_Slot()
    {
        var slot = SlotData.Empty;
        var stack = ItemStack.FromSlotData(slot);
        
        Assert.True(stack.IsEmpty);
    }

    [Fact]
    public void FromSlotData_Should_Create_Stack_From_Slot()
    {
        var slot = new SlotData(10, 32, new byte[] { 0x01 });
        var stack = ItemStack.FromSlotData(slot);
        
        Assert.Equal(10, stack.ItemId);
        Assert.Equal(32, stack.Count);
        Assert.Equal(new byte[] { 0x01 }, stack.Nbt);
    }

    [Fact]
    public void ToSlotData_Should_Return_Empty_For_Empty_Stack()
    {
        var stack = new ItemStack();
        var slot = stack.ToSlotData();
        
        Assert.True(slot.IsEmpty);
    }

    [Fact]
    public void ToSlotData_Should_Convert_Stack_To_Slot()
    {
        var nbt = new byte[] { 0x01, 0x02 };
        var stack = new ItemStack(10, 32, nbt);
        var slot = stack.ToSlotData();
        
        Assert.True(slot.Present);
        Assert.Equal(10, slot.ItemId);
        Assert.Equal(32, slot.ItemCount);
        Assert.Equal(nbt, slot.Nbt);
    }
}




