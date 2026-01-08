using MineSharp.Core;
using MineSharp.Core.DataTypes;
using MineSharp.Core.Protocol;
using MineSharp.Core.Protocol.PacketTypes;
using System.Collections.Generic;
using Xunit;

namespace MineSharp.Tests.Protocol;

public class InventoryPacketParserTests
{
    [Fact]
    public void ParseSlotData_Empty_Should_Return_Empty_Slot()
    {
        // Test empty slot data via SetCreativeModeSlot packet
        // Set Creative Mode Slot (0x37) with empty slot:
        // - Short: Slot (0)
        // - SlotData: Empty (not present)
        var packetData = new List<byte>();
        packetData.AddRange(WriteVarInt(0)); // Placeholder
        packetData.AddRange(WriteVarInt(0x37)); // Packet ID
        packetData.AddRange(WriteShort(0)); // Slot
        packetData.Add(0x00); // SlotData: Empty (not present)
        
        // Fix packet length
        int bodyLength = packetData.Count - 1;
        var lengthBytes = WriteVarInt(bodyLength);
        packetData.RemoveRange(0, 1);
        packetData.InsertRange(0, lengthBytes);
        
        var (packetId, packet) = PacketParser.ParsePacket(packetData.ToArray(), ConnectionState.Play);
        
        Assert.Equal(0x37, packetId);
        Assert.NotNull(packet);
        Assert.IsType<SetCreativeModeSlotPacket>(packet);
        
        var creativeSlot = (SetCreativeModeSlotPacket)packet!;
        Assert.True(creativeSlot.SlotData.IsEmpty);
    }

    [Fact]
    public void ParseSlotData_With_Item_Should_Return_Slot_With_Item()
    {
        // Test slot data with item via SetCreativeModeSlot packet
        // Set Creative Mode Slot (0x37) with item:
        // - Short: Slot (5)
        // - SlotData: Item ID 10, Count 64, No NBT
        var packetData = new List<byte>();
        packetData.AddRange(WriteVarInt(0)); // Placeholder
        packetData.AddRange(WriteVarInt(0x37)); // Packet ID
        packetData.AddRange(WriteShort(5)); // Slot
        packetData.Add(0x01); // SlotData: Present = true
        packetData.AddRange(WriteVarInt(10)); // Item ID
        packetData.Add(64); // Count
        packetData.Add(0x00); // No NBT
        
        // Fix packet length
        int bodyLength = packetData.Count - 1;
        var lengthBytes = WriteVarInt(bodyLength);
        packetData.RemoveRange(0, 1);
        packetData.InsertRange(0, lengthBytes);
        
        var (packetId, packet) = PacketParser.ParsePacket(packetData.ToArray(), ConnectionState.Play);
        
        Assert.Equal(0x37, packetId);
        Assert.NotNull(packet);
        Assert.IsType<SetCreativeModeSlotPacket>(packet);
        
        var creativeSlot = (SetCreativeModeSlotPacket)packet!;
        Assert.False(creativeSlot.SlotData.IsEmpty);
        Assert.Equal(10, creativeSlot.SlotData.ItemId);
        Assert.Equal(64, creativeSlot.SlotData.ItemCount);
    }

    [Fact]
    public void ParseClickContainerButton_Should_Parse_Correctly()
    {
        // Click Container Button (0x10):
        // - Byte: Window ID (5)
        // - Byte: Button ID (2)
        var packetData = new List<byte>();
        packetData.AddRange(WriteVarInt(0)); // Placeholder for packet length
        packetData.AddRange(WriteVarInt(0x10)); // Packet ID
        packetData.Add(5); // Window ID
        packetData.Add(2); // Button ID
        
        // Fix packet length (body length = total - placeholder length - 1 byte for placeholder)
        int placeholderLength = 1; // One byte for 0x00
        int bodyLength = packetData.Count - placeholderLength;
        var lengthBytes = WriteVarInt(bodyLength);
        packetData.RemoveRange(0, placeholderLength); // Remove placeholder
        packetData.InsertRange(0, lengthBytes);
        
        var (packetId, packet) = PacketParser.ParsePacket(packetData.ToArray(), ConnectionState.Play);
        
        Assert.Equal(0x10, packetId);
        Assert.NotNull(packet);
        Assert.IsType<ClickContainerButtonPacket>(packet);
        
        var clickButton = (ClickContainerButtonPacket)packet!;
        Assert.Equal(5, clickButton.WindowId);
        Assert.Equal(2, clickButton.ButtonId);
    }

    [Fact]
    public void ParseCloseContainer_Should_Parse_Correctly()
    {
        // Close Container (0x12):
        // - Byte: Window ID (3)
        var packetData = new List<byte>();
        packetData.AddRange(WriteVarInt(0)); // Placeholder for length
        packetData.AddRange(WriteVarInt(0x12)); // Packet ID
        packetData.Add(3); // Window ID
        
        // Fix packet length
        int bodyLength = packetData.Count - 1; // Subtract placeholder
        var lengthBytes = WriteVarInt(bodyLength);
        packetData.RemoveRange(0, 1); // Remove placeholder
        packetData.InsertRange(0, lengthBytes);
        
        var (packetId, packet) = PacketParser.ParsePacket(packetData.ToArray(), ConnectionState.Play);
        
        Assert.Equal(0x12, packetId);
        Assert.NotNull(packet);
        Assert.IsType<CloseContainerPacket>(packet);
        
        var closeContainer = (CloseContainerPacket)packet!;
        Assert.Equal(3, closeContainer.WindowId);
    }

    [Fact]
    public void ParseSetHeldItem_Should_Parse_Correctly()
    {
        // Set Held Item (0x34):
        // - Short: Slot (5)
        var packetData = new List<byte>();
        packetData.AddRange(WriteVarInt(0)); // Placeholder
        packetData.AddRange(WriteVarInt(0x34)); // Packet ID
        packetData.AddRange(WriteShort(5)); // Slot
        
        // Fix packet length
        int bodyLength = packetData.Count - 1;
        var lengthBytes = WriteVarInt(bodyLength);
        packetData.RemoveRange(0, 1);
        packetData.InsertRange(0, lengthBytes);
        
        var (packetId, packet) = PacketParser.ParsePacket(packetData.ToArray(), ConnectionState.Play);
        
        Assert.Equal(0x34, packetId);
        Assert.NotNull(packet);
        Assert.IsType<SetHeldItemPacket>(packet);
        
        var setHeldItem = (SetHeldItemPacket)packet!;
        Assert.Equal(5, setHeldItem.Slot);
    }

    [Fact]
    public void ParseSetCreativeModeSlot_Should_Parse_Correctly()
    {
        // Set Creative Mode Slot (0x37):
        // - Short: Slot (10)
        // - SlotData: Empty slot
        var packetData = new List<byte>();
        packetData.AddRange(WriteVarInt(0)); // Placeholder
        packetData.AddRange(WriteVarInt(0x37)); // Packet ID
        packetData.AddRange(WriteShort(10)); // Slot
        packetData.Add(0x00); // SlotData: Empty (not present)
        
        // Fix packet length
        int bodyLength = packetData.Count - 1;
        var lengthBytes = WriteVarInt(bodyLength);
        packetData.RemoveRange(0, 1);
        packetData.InsertRange(0, lengthBytes);
        
        var (packetId, packet) = PacketParser.ParsePacket(packetData.ToArray(), ConnectionState.Play);
        
        Assert.Equal(0x37, packetId);
        Assert.NotNull(packet);
        Assert.IsType<SetCreativeModeSlotPacket>(packet);
        
        var creativeSlot = (SetCreativeModeSlotPacket)packet!;
        Assert.Equal(10, creativeSlot.Slot);
        Assert.True(creativeSlot.SlotData.IsEmpty);
    }

    [Fact]
    public void ParseSetCreativeModeSlot_With_Item_Should_Parse_Correctly()
    {
        // Set Creative Mode Slot (0x37) with item:
        // - Short: Slot (5)
        // - SlotData: Item ID 10, Count 64, No NBT
        var packetData = new List<byte>();
        packetData.AddRange(WriteVarInt(0)); // Placeholder
        packetData.AddRange(WriteVarInt(0x37)); // Packet ID
        packetData.AddRange(WriteShort(5)); // Slot
        packetData.Add(0x01); // SlotData: Present = true
        packetData.AddRange(WriteVarInt(10)); // Item ID
        packetData.Add(64); // Count
        packetData.Add(0x00); // No NBT
        
        // Fix packet length
        int bodyLength = packetData.Count - 1;
        var lengthBytes = WriteVarInt(bodyLength);
        packetData.RemoveRange(0, 1);
        packetData.InsertRange(0, lengthBytes);
        
        var (packetId, packet) = PacketParser.ParsePacket(packetData.ToArray(), ConnectionState.Play);
        
        Assert.Equal(0x37, packetId);
        Assert.NotNull(packet);
        Assert.IsType<SetCreativeModeSlotPacket>(packet);
        
        var creativeSlot = (SetCreativeModeSlotPacket)packet!;
        Assert.Equal(5, creativeSlot.Slot);
        Assert.True(creativeSlot.SlotData.Present);
        Assert.Equal(10, creativeSlot.SlotData.ItemId);
        Assert.Equal(64, creativeSlot.SlotData.ItemCount);
    }

    [Fact]
    public void ParseClickContainer_Should_Parse_Correctly()
    {
        // Click Container (0x11) - simple case:
        // - Byte: Window ID (0)
        // - VarInt: State ID (1)
        // - Short: Slot (36)
        // - Byte: Button (0)
        // - VarInt: Mode (0)
        // - VarInt: Slots count (0)
        // - SlotData: Carried item (empty)
        var packetData = new List<byte>();
        packetData.AddRange(WriteVarInt(0)); // Placeholder
        packetData.AddRange(WriteVarInt(0x11)); // Packet ID
        packetData.Add(0); // Window ID
        packetData.AddRange(WriteVarInt(1)); // State ID
        packetData.AddRange(WriteShort(36)); // Slot
        packetData.Add(0); // Button
        packetData.AddRange(WriteVarInt(0)); // Mode
        packetData.AddRange(WriteVarInt(0)); // Slots count (0)
        packetData.Add(0x00); // Carried item: Empty
        
        // Fix packet length
        int bodyLength = packetData.Count - 1;
        var lengthBytes = WriteVarInt(bodyLength);
        packetData.RemoveRange(0, 1);
        packetData.InsertRange(0, lengthBytes);
        
        var (packetId, packet) = PacketParser.ParsePacket(packetData.ToArray(), ConnectionState.Play);
        
        Assert.Equal(0x11, packetId);
        Assert.NotNull(packet);
        Assert.IsType<ClickContainerPacket>(packet);
        
        var clickContainer = (ClickContainerPacket)packet!;
        Assert.Equal(0, clickContainer.WindowId);
        Assert.Equal(1, clickContainer.StateId);
        Assert.Equal(36, clickContainer.Slot);
        Assert.Equal(0, clickContainer.Button);
        Assert.Equal(0, clickContainer.Mode);
        Assert.Empty(clickContainer.Slots);
        Assert.True(clickContainer.CarriedItem.IsEmpty);
    }

    // Helper methods for writing protocol data
    private static byte[] WriteVarInt(int value)
    {
        var bytes = new List<byte>();
        uint uValue = unchecked((uint)value);
        while (true)
        {
            byte b = (byte)(uValue & 0x7F);
            uValue >>= 7;
            if (uValue != 0)
            {
                b |= 0x80;
            }
            bytes.Add(b);
            if (uValue == 0)
            {
                break;
            }
        }
        return bytes.ToArray();
    }

    private static byte[] WriteShort(short value)
    {
        return new byte[]
        {
            (byte)((value >> 8) & 0xFF),
            (byte)(value & 0xFF)
        };
    }
}




