using MineSharp.Core.Protocol.PacketTypes;
using MineSharp.Core.DataTypes;
using System.Collections.Generic;

namespace MineSharp.Core.Protocol;

/// <summary>
/// Parses incoming packets from raw bytes.
/// </summary>
public class PacketParser
{
    public static (int packetId, object? packet) ParsePacket(byte[] data, ConnectionState state)
    {
        var reader = new ProtocolReader(data);
        
        // Read packet length and ID
        var packetLength = reader.ReadVarInt();
        var packetId = reader.ReadVarInt();
        
        // Parse based on state and packet ID
        if (state == ConnectionState.Handshaking)
        {
            if (packetId == 0) // Handshake
            {
                return (packetId, ParseHandshake(reader));
            }
        }
        else if (state == ConnectionState.Login)
        {
            if (packetId == 0) // Login Start
            {
                return (packetId, ParseLoginStart(reader));
            }
            else if (packetId == 3) // Login Acknowledged
            {
                return (packetId, null); // Empty packet
            }
        }
        else if (state == ConnectionState.Configuration)
        {
            if (packetId == 0) // Client Information
            {
                return (packetId, ParseClientInformation(reader));
            }
            else if (packetId == 2) // Plugin Message (Configuration)
            {
                var channel = reader.ReadString();
                var remainingLength = reader.Remaining;
                var pluginData = remainingLength > 0 ? reader.ReadBytes(remainingLength) : Array.Empty<byte>();
                return (packetId, new Dictionary<string, object> { { "channel", channel }, { "data", pluginData } });
            }
            else if (packetId == 3) // Acknowledge Finish Configuration
            {
                return (packetId, null); // Empty packet
            }
            else if (packetId == 0x07) // Serverbound Known Packs
            {
                return (packetId, ParseKnownPacks(reader));
            }
        }
        else if (state == ConnectionState.Play)
        {
            if (packetId == 0x1B) // Serverbound Keep Alive
            {
                return (packetId, ParseKeepAlive(reader));
            }
            else if (packetId == 0x1D) // Set Player Position
            {
                return (packetId, ParseSetPlayerPosition(reader));
            }
            else if (packetId == 0x1E) // Set Player Position and Rotation
            {
                return (packetId, ParseSetPlayerPositionAndRotation(reader));
            }
            else if (packetId == 0x1F) // Set Player Rotation
            {
                return (packetId, ParseSetPlayerRotation(reader));
            }
            else if (packetId == 0x28) // Player Action
            {
                return (packetId, ParsePlayerAction(reader));
            }
            else if (packetId == 0x3F) // Use Item On
            {
                return (packetId, ParseUseItemOn(reader));
            }
            else if (packetId == 0x10) // Click Container Button
            {
                return (packetId, ParseClickContainerButton(reader));
            }
            else if (packetId == 0x11) // Click Container
            {
                return (packetId, ParseClickContainer(reader));
            }
            else if (packetId == 0x12) // Close Container (serverbound)
            {
                return (packetId, ParseCloseContainer(reader));
            }
            else if (packetId == 0x34) // Set Held Item
            {
                return (packetId, ParseSetHeldItem(reader));
            }
            else if (packetId == 0x37) // Set Creative Mode Slot
            {
                var packet = ParseSetCreativeModeSlot(reader);
                return (packetId, packet);
            }
            else if (packetId == 0x3C) // Swing Arm
            {
                return (packetId, ParseSwingArm(reader));
            }
        }
        
        // Unknown packet
        return (packetId, null);
    }
    
    private static HandshakePacket ParseHandshake(ProtocolReader reader)
    {
        var protocolVersion = reader.ReadVarInt();
        var serverAddress = reader.ReadString(255);
        var serverPort = reader.ReadUnsignedShort();
        var intent = reader.ReadVarInt();
        
        return new HandshakePacket
        {
            ProtocolVersion = protocolVersion,
            ServerAddress = serverAddress,
            ServerPort = serverPort,
            Intent = intent
        };
    }
    
    private static LoginStartPacket ParseLoginStart(ProtocolReader reader)
    {
        var username = reader.ReadString(16);
        var playerUuid = reader.ReadUuid();
        
        return new LoginStartPacket
        {
            Username = username,
            PlayerUuid = playerUuid
        };
    }
    
    private static ClientInformationPacket ParseClientInformation(ProtocolReader reader)
    {
        var locale = reader.ReadString(16);
        var viewDistance = (sbyte)reader.ReadByte();
        var chatMode = reader.ReadVarInt();
        var chatColors = reader.ReadBool();
        var displayedSkinParts = reader.ReadByte();
        var mainHand = reader.ReadVarInt();
        var enableTextFiltering = reader.ReadBool();
        var allowServerListings = reader.ReadBool();
        
        return new ClientInformationPacket
        {
            Locale = locale,
            ViewDistance = viewDistance,
            ChatMode = chatMode,
            ChatColors = chatColors,
            DisplayedSkinParts = displayedSkinParts,
            MainHand = mainHand,
            EnableTextFiltering = enableTextFiltering,
            AllowServerListings = allowServerListings
        };
    }
    
    private static List<object> ParseKnownPacks(ProtocolReader reader)
    {
        var count = reader.ReadVarInt();
        var packs = new List<object>();
        
        for (int i = 0; i < count; i++)
        {
            var @namespace = reader.ReadString();
            var packId = reader.ReadString();
            var version = reader.ReadString();
            packs.Add(new List<string> { @namespace, packId, version });
        }
        
        return packs;
    }
    
    private static KeepAlivePacket ParseKeepAlive(ProtocolReader reader)
    {
        var keepAliveId = reader.ReadLong();
        
        Console.WriteLine($"  │  → Parsed KeepAlivePacket (0x1B): ID {keepAliveId}");
        
        return new KeepAlivePacket
        {
            KeepAliveId = keepAliveId
        };
    }
    
    private static SetPlayerPositionPacket ParseSetPlayerPosition(ProtocolReader reader)
    {
        var x = reader.ReadDouble();
        var y = reader.ReadDouble();
        var z = reader.ReadDouble();
        
        Console.WriteLine($"  │  → Parsed SetPlayerPositionPacket (0x1D): ({x:F2}, {y:F2}, {z:F2})");
        
        return new SetPlayerPositionPacket
        {
            X = x,
            Y = y,
            Z = z
        };
    }
    
    private static SetPlayerPositionAndRotationPacket ParseSetPlayerPositionAndRotation(ProtocolReader reader)
    {
        var x = reader.ReadDouble();
        var y = reader.ReadDouble();
        var z = reader.ReadDouble();
        var yaw = reader.ReadFloat();
        var pitch = reader.ReadFloat();
        
        Console.WriteLine($"  │  → Parsed SetPlayerPositionAndRotationPacket (0x1E): ({x:F2}, {y:F2}, {z:F2}), Yaw: {yaw:F2}, Pitch: {pitch:F2}");
        
        return new SetPlayerPositionAndRotationPacket
        {
            X = x,
            Y = y,
            Z = z,
            Yaw = yaw,
            Pitch = pitch
        };
    }
    
    private static SetPlayerRotationPacket ParseSetPlayerRotation(ProtocolReader reader)
    {
        var yaw = reader.ReadFloat();
        var pitch = reader.ReadFloat();
        
        Console.WriteLine($"  │  → Parsed SetPlayerRotationPacket (0x1F): Yaw: {yaw:F2}, Pitch: {pitch:F2}");
        
        return new SetPlayerRotationPacket
        {
            Yaw = yaw,
            Pitch = pitch
        };
    }
    
    private static PlayerActionPacket ParsePlayerAction(ProtocolReader reader)
    {
        var status = reader.ReadVarInt();
        var locationLong = reader.ReadLong();
        var location = Position.FromLong(locationLong);
        var face = reader.ReadByte();
        var sequence = reader.ReadVarInt();
        
        string statusName = status switch
        {
            0 => "Started digging",
            1 => "Cancelled digging",
            2 => "Finished digging",
            _ => $"Unknown ({status})"
        };
        
        Console.WriteLine($"  │  → Parsed PlayerActionPacket (0x28): {statusName} at ({location.X}, {location.Y}, {location.Z}), Face: {face}, Sequence: {sequence}");
        
        return new PlayerActionPacket
        {
            Status = status,
            Location = location,
            Face = face,
            Sequence = sequence
        };
    }
    
    private static UseItemOnPacket ParseUseItemOn(ProtocolReader reader)
    {
        // Use Item On packet structure (0x3F):
        // 1. Hand (VarInt)
        // 2. Location (Block Position - Long)
        // 3. Face (VarInt)
        // 4. Cursor Position X (Float)
        // 5. Cursor Position Y (Float)
        // 6. Cursor Position Z (Float)
        // 7. Inside Block (Bool)
        // 8. Sequence (VarInt)
        
        var hand = reader.ReadVarInt();
        var locationLong = reader.ReadLong();
        var location = Position.FromLong(locationLong);
        var face = reader.ReadVarInt();
        var cursorX = reader.ReadFloat();
        var cursorY = reader.ReadFloat();
        var cursorZ = reader.ReadFloat();
        var insideBlock = reader.ReadBool();
        var sequence = reader.ReadVarInt();
        
        string handName = hand == 0 ? "Main Hand" : "Off Hand";
        string faceName = face switch
        {
            0 => "Bottom",
            1 => "Top",
            2 => "North",
            3 => "South",
            4 => "West",
            5 => "East",
            _ => $"Unknown ({face})"
        };
        
        Console.WriteLine($"  │  → Parsed UseItemOnPacket (0x3F): {handName}, clicked ({location.X}, {location.Y}, {location.Z}) on {faceName} face, Sequence: {sequence}");
        
        return new UseItemOnPacket
        {
            Hand = hand,
            Location = location,
            Face = face,
            CursorPositionX = cursorX,
            CursorPositionY = cursorY,
            CursorPositionZ = cursorZ,
            InsideBlock = insideBlock,
            Sequence = sequence
        };
    }
    
    private static void SkipNbtTag(ProtocolReader reader)
    {
        // NBT Tag Types:
        // 0 = TAG_End
        // 1 = TAG_Byte
        // 2 = TAG_Short
        // 3 = TAG_Int
        // 4 = TAG_Long
        // 5 = TAG_Float
        // 6 = TAG_Double
        // 7 = TAG_Byte_Array
        // 8 = TAG_String
        // 9 = TAG_List
        // 10 = TAG_Compound
        // 11 = TAG_Int_Array
        // 12 = TAG_Long_Array
        
        byte tagType = reader.ReadByte();
        
        if (tagType == 0) // TAG_End
        {
            return;
        }
        
        // Skip tag name (string: VarInt length + bytes)
        if (tagType != 9 && tagType != 10) // Lists and compounds handle their own names differently
        {
            int nameLength = reader.ReadVarInt();
            if (nameLength > 0)
            {
                reader.ReadBytes(nameLength);
            }
        }
        
        // Skip tag payload based on type
        switch (tagType)
        {
            case 1: // TAG_Byte
                reader.ReadByte();
                break;
            case 2: // TAG_Short
                reader.ReadShort();
                break;
            case 3: // TAG_Int
                reader.ReadInt();
                break;
            case 4: // TAG_Long
                reader.ReadLong();
                break;
            case 5: // TAG_Float
                reader.ReadFloat();
                break;
            case 6: // TAG_Double
                reader.ReadDouble();
                break;
            case 7: // TAG_Byte_Array
                int byteArrayLength = reader.ReadInt();
                reader.ReadBytes(byteArrayLength);
                break;
            case 8: // TAG_String
                int stringLength = reader.ReadVarInt();
                reader.ReadBytes(stringLength);
                break;
            case 9: // TAG_List
                byte listTagType = reader.ReadByte();
                int listLength = reader.ReadInt();
                for (int i = 0; i < listLength; i++)
                {
                    if (listTagType == 0) // TAG_End in list (shouldn't happen, but handle it)
                    {
                        // Shouldn't occur, but if it does, read TAG_End
                        reader.ReadByte();
                    }
                    else if (listTagType == 9) // Nested TAG_List
                    {
                        SkipNbtTag(reader);
                    }
                    else if (listTagType == 10) // Nested TAG_Compound
                    {
                        SkipNbtTag(reader);
                    }
                    else
                    {
                        // Skip payload without name (lists don't have names for items)
                        SkipNbtPayload(listTagType, reader);
                    }
                }
                break;
            case 10: // TAG_Compound
                // Read tags until TAG_End
                while (true)
                {
                    byte compoundTagType = reader.ReadByte();
                    if (compoundTagType == 0) // TAG_End
                    {
                        break;
                    }
                    // Skip tag name
                    int compoundNameLength = reader.ReadVarInt();
                    if (compoundNameLength > 0)
                    {
                        reader.ReadBytes(compoundNameLength);
                    }
                    // Recursively skip tag payload
                    SkipNbtTag(reader);
                }
                break;
            case 11: // TAG_Int_Array
                int intArrayLength = reader.ReadInt();
                for (int i = 0; i < intArrayLength; i++)
                {
                    reader.ReadInt();
                }
                break;
            case 12: // TAG_Long_Array
                int longArrayLength = reader.ReadInt();
                for (int i = 0; i < longArrayLength; i++)
                {
                    reader.ReadLong();
                }
                break;
        }
    }
    
    private static void SkipNbtPayload(byte tagType, ProtocolReader reader)
    {
        // Skip payload for a tag type (without name)
        switch (tagType)
        {
            case 1: // TAG_Byte
                reader.ReadByte();
                break;
            case 2: // TAG_Short
                reader.ReadShort();
                break;
            case 3: // TAG_Int
                reader.ReadInt();
                break;
            case 4: // TAG_Long
                reader.ReadLong();
                break;
            case 5: // TAG_Float
                reader.ReadFloat();
                break;
            case 6: // TAG_Double
                reader.ReadDouble();
                break;
            case 7: // TAG_Byte_Array
                int byteArrayLength = reader.ReadInt();
                reader.ReadBytes(byteArrayLength);
                break;
            case 8: // TAG_String
                int stringLength = reader.ReadVarInt();
                reader.ReadBytes(stringLength);
                break;
            case 11: // TAG_Int_Array
                int intArrayLength = reader.ReadInt();
                for (int i = 0; i < intArrayLength; i++)
                {
                    reader.ReadInt();
                }
                break;
            case 12: // TAG_Long_Array
                int longArrayLength = reader.ReadInt();
                for (int i = 0; i < longArrayLength; i++)
                {
                    reader.ReadLong();
                }
                break;
        }
    }
    
    public static SlotData ParseSlotData(ProtocolReader reader)
    {
        // Modern Slot Data structure (1.20.5+):
        // 1. VarInt: Item Count (if 0, slot is empty, no further fields)
        // 2. VarInt: Item ID (only if count > 0)
        // 3. VarInt: Number of components to add
        // 4. VarInt: Number of components to remove
        // 5. Array of components to add (component type + data)
        // 6. Array of components to remove (just component type)
        
        var itemCount = reader.ReadVarInt();
        if (itemCount == 0)
        {
            return SlotData.Empty;
        }
        
        var itemId = reader.ReadVarInt();
        
        // Read components to add
        var componentsToAdd = reader.ReadVarInt();
        for (int i = 0; i < componentsToAdd; i++)
        {
            var componentType = reader.ReadVarInt();
            // Skip component data - we'll need to implement proper component parsing later
            // For now, we'll skip based on component type
            SkipComponentData(reader, componentType);
        }
        
        // Read components to remove
        var componentsToRemove = reader.ReadVarInt();
        for (int i = 0; i < componentsToRemove; i++)
        {
            var componentType = reader.ReadVarInt();
            // Components to remove only have the type, no data
        }
        
        // For now, we don't store component data
        return new SlotData(itemId, (byte)itemCount, null);
    }
    
    private static void SkipComponentData(ProtocolReader reader, int componentType)
    {
        // Component types and their data structures are complex
        // For now, we'll implement a basic skip for common components
        // TODO: Implement full component parsing based on component type
        
        // This is a simplified implementation that may not work for all component types
        // We'll need to expand this as we encounter different components
        
        switch (componentType)
        {
            case 0: // custom_data - NBT
                SkipNbtTag(reader);
                break;
            case 1: // max_stack_size - VarInt
                reader.ReadVarInt();
                break;
            case 2: // max_damage - VarInt
                reader.ReadVarInt();
                break;
            case 3: // damage - VarInt
                reader.ReadVarInt();
                break;
            case 4: // unbreakable - Boolean
                reader.ReadBool();
                break;
            case 5: // custom_name - Text Component (JSON string)
                reader.ReadString();
                break;
            case 6: // item_name - Text Component (JSON string)
                reader.ReadString();
                break;
            case 7: // lore - Array of Text Components
                var loreCount = reader.ReadVarInt();
                for (int i = 0; i < loreCount; i++)
                {
                    reader.ReadString();
                }
                break;
            case 8: // rarity - VarInt Enum
                reader.ReadVarInt();
                break;
            case 9: // enchantments - Complex structure
                SkipEnchantments(reader);
                break;
            case 10: // can_place_on - Block predicate
                SkipBlockPredicate(reader);
                break;
            case 11: // can_break - Block predicate
                SkipBlockPredicate(reader);
                break;
            // Add more component types as needed
            default:
                // For unknown component types, we'll try to skip by reading a VarInt length and that many bytes
                // This is a fallback and may not work for all types
                Console.WriteLine($"  │  ⚠ Unknown component type {componentType} - attempting to skip");
                // Try reading as a length-prefixed structure
                try
                {
                    var length = reader.ReadVarInt();
                    if (length > 0 && length < 10000) // Sanity check
                    {
                        reader.ReadBytes(length);
                    }
                }
                catch
                {
                    // If that fails, we're in trouble
                    Console.WriteLine($"  │  ⚠ Failed to skip component type {componentType}");
                }
                break;
        }
    }
    
    private static void SkipEnchantments(ProtocolReader reader)
    {
        // Enchantments structure: Array of (VarInt enchantment ID, VarInt level)
        var count = reader.ReadVarInt();
        for (int i = 0; i < count; i++)
        {
            reader.ReadVarInt(); // Enchantment ID
            reader.ReadVarInt(); // Level
        }
        reader.ReadBool(); // Show in tooltip
    }
    
    private static void SkipBlockPredicate(ProtocolReader reader)
    {
        // Block predicate structure is complex, for now just skip the array
        var count = reader.ReadVarInt();
        for (int i = 0; i < count; i++)
        {
            // Each entry: Optional block ID + optional properties + optional NBT
            var hasBlockId = reader.ReadBool();
            if (hasBlockId)
            {
                reader.ReadVarInt(); // Block ID
            }
            
            var propertyCount = reader.ReadVarInt();
            for (int j = 0; j < propertyCount; j++)
            {
                reader.ReadString(); // Property name
                reader.ReadString(); // Property value
            }
            
            var hasNbt = reader.ReadBool();
            if (hasNbt)
            {
                SkipNbtTag(reader);
            }
        }
        reader.ReadBool(); // Show in tooltip
    }
    
    private static ClickContainerButtonPacket ParseClickContainerButton(ProtocolReader reader)
    {
        var windowId = reader.ReadByte();
        var buttonId = reader.ReadByte();
        
        string windowType = windowId == 0 ? "Player Inventory" : $"Window {windowId}";
        Console.WriteLine($"  │  → Parsed ClickContainerButtonPacket (0x10): {windowType}, Button ID {buttonId}");
        
        return new ClickContainerButtonPacket
        {
            WindowId = windowId,
            ButtonId = buttonId
        };
    }
    
    private static ClickContainerPacket ParseClickContainer(ProtocolReader reader)
    {
        // Click Container packet structure (0x11):
        // 1. Byte: Window ID
        // 2. VarInt: State ID
        // 3. Short: Slot
        // 4. Byte: Button
        // 5. VarInt: Mode
        // 6. VarInt: Array length (slot changes)
        // 7. For each slot change:
        //    a. Short: Slot
        //    b. SlotData: Slot Data
        // 8. SlotData: Carried Item
        
        var windowId = reader.ReadByte();
        var stateId = reader.ReadVarInt();
        var slot = reader.ReadShort();
        var button = reader.ReadByte();
        var mode = reader.ReadVarInt();
        
        var slotChangesCount = reader.ReadVarInt();
        var slots = new List<(short, SlotData)>();
        for (int i = 0; i < slotChangesCount; i++)
        {
            var changeSlot = reader.ReadShort();
            var slotData = ParseSlotData(reader);
            slots.Add((changeSlot, slotData));
        }
        
        var carriedItem = ParseSlotData(reader);
        
        // Log parsed packet - especially for creative menu (Window ID != 0)
        string windowType = windowId == 0 ? "Player Inventory" : $"Window {windowId} (Creative Menu?)";
        string carriedInfo = carriedItem.Present 
            ? $"item ID {carriedItem.ItemId}, count {carriedItem.ItemCount}" 
            : "empty";
        string slotInfo = slot >= 0 ? $"slot {slot}" : $"outside (slot {slot})";
        
        Console.WriteLine($"  │  → Parsed ClickContainerPacket (0x11): {windowType}, {slotInfo}, button {button}, mode {mode}, carried: {carriedInfo}");
        
        return new ClickContainerPacket
        {
            WindowId = windowId,
            StateId = stateId,
            Slot = slot,
            Button = button,
            Mode = mode,
            Slots = slots,
            CarriedItem = carriedItem
        };
    }
    
    private static CloseContainerPacket ParseCloseContainer(ProtocolReader reader)
    {
        var windowId = reader.ReadByte();
        
        string windowType = windowId == 0 ? "Player Inventory" : $"Window {windowId}";
        Console.WriteLine($"  │  → Parsed CloseContainerPacket (0x12 serverbound): {windowType}");
        
        return new CloseContainerPacket
        {
            WindowId = windowId
        };
    }
    
    private static SetHeldItemPacket ParseSetHeldItem(ProtocolReader reader)
    {
        var slot = reader.ReadShort();
        
        int inventorySlot = slot >= 0 && slot <= 8 ? 36 + slot : -1; // Hotbar slots 0-8 map to inventory slots 36-44
        Console.WriteLine($"  │  → Parsed SetHeldItemPacket (0x34): Hotbar slot {slot} (inventory slot {inventorySlot})");
        
        return new SetHeldItemPacket
        {
            Slot = slot
        };
    }
    
    private static SetCreativeModeSlotPacket ParseSetCreativeModeSlot(ProtocolReader reader)
    {
        var slot = reader.ReadShort();
        var slotData = ParseSlotData(reader);
        
        // Log parsed packet details
        string itemInfo = slotData.Present 
            ? $"item ID {slotData.ItemId}, count {slotData.ItemCount}" 
            : "empty";
        Console.WriteLine($"  │  → Parsed SetCreativeModeSlotPacket (0x37): slot {slot}, {itemInfo}");
        
        return new SetCreativeModeSlotPacket
        {
            Slot = slot,
            SlotData = slotData
        };
    }
    
    private static SwingArmPacket ParseSwingArm(ProtocolReader reader)
    {
        var hand = reader.ReadVarInt();
        
        string handName = hand == 0 ? "Main Hand" : "Off Hand";
        Console.WriteLine($"  │  → Parsed SwingArmPacket (0x3C): {handName}");
        
        return new SwingArmPacket
        {
            Hand = hand
        };
    }
}

