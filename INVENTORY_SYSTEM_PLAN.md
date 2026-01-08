# Inventory System Implementation Plan

## Overview
This plan outlines the implementation of a complete inventory system for players, including packet parsing, handling, and inventory management.

## Current State
- ✅ `Player` class exists with `Inventory` property
- ✅ `Inventory` class skeleton exists (all methods throw `NotImplementedException`)
- ❌ No inventory packet types defined
- ❌ No inventory packet parsing
- ❌ No inventory packet handling
- ❌ No item/slot data structures

---

## Files to Create

### 1. Core Data Types

#### `MineSharp/MineSharp.Core/DataTypes/SlotData.cs`
- **Purpose**: Represents a slot in an inventory (can be empty or contain an item)
- **Structure**:
  - `bool Present` - Whether slot has an item
  - `int ItemId` - Item/block ID (only if Present)
  - `byte ItemCount` - Stack size (only if Present)
  - `NbtCompound? Nbt` - Optional NBT data (only if Present)
- **Methods**: 
  - `SlotData.Empty` - Static property for empty slot
  - `SlotData(int itemId, byte count, NbtCompound? nbt = null)` - Constructor
  - `ToProtocolBytes()` - Serialize for clientbound packets (future)

#### `MineSharp/MineSharp.Core/DataTypes/HashedSlotData.cs` (Optional - for optimization)
- **Purpose**: Optimized slot data with CRC32C hashes (used in Click Container packet)
- **Structure**: Similar to SlotData but with hashed component data
- **Note**: May be deferred until performance optimization is needed

### 2. Packet Types (Serverbound - Client → Server)

#### `MineSharp/MineSharp.Core/Protocol/PacketTypes/ClickContainerPacket.cs`
- **Purpose**: Client clicks a slot in inventory
- **Packet ID**: 0x11
- **Fields**:
  - `byte WindowId` - Window identifier (0 = player inventory)
  - `int StateId` - Inventory state ID for synchronization
  - `short Slot` - Slot index clicked (-999 = outside window)
  - `byte Button` - Mouse button (0=Left, 1=Right, 2=Middle)
  - `int Mode` - Click mode (0=Click, 1=Shift+Click, 2=Number Key, etc.)
  - `List<SlotData> Slots` - Array of expected slot changes (for validation)
  - `SlotData CarriedItem` - Item currently held by cursor

#### `MineSharp/MineSharp.Core/Protocol/PacketTypes/ClickContainerButtonPacket.cs`
- **Purpose**: Client clicks a button in container (brewing stand, etc.)
- **Packet ID**: 0x10
- **Fields**:
  - `byte WindowId`
  - `byte ButtonId`

#### `MineSharp/MineSharp.Core/Protocol/PacketTypes/CloseContainerPacket.cs`
- **Purpose**: Client closes a container window
- **Packet ID**: 0x12 (serverbound)
- **Fields**:
  - `byte WindowId`

#### `MineSharp/MineSharp.Core/Protocol/PacketTypes/SetHeldItemPacket.cs`
- **Purpose**: Client changes selected hotbar slot
- **Packet ID**: 0x34
- **Fields**:
  - `short Slot` - Hotbar slot index (0-8)

#### `MineSharp/MineSharp.Core/Protocol/PacketTypes/SetCreativeModeSlotPacket.cs`
- **Purpose**: Creative mode inventory changes
- **Packet ID**: 0x37
- **Fields**:
  - `short Slot` - Slot index
  - `SlotData SlotData` - Item to set in slot

### 3. Packet Types (Clientbound - Server → Client)

#### `MineSharp/MineSharp.Core/Protocol/PacketTypes/OpenScreenPacket.cs`
- **Purpose**: Server opens a container window for player
- **Packet ID**: (TBD from protocol docs)
- **Fields**:
  - `int WindowId`
  - `int WindowType` - Container type ID
  - `ChatComponent WindowTitle`

#### `MineSharp/MineSharp.Core/Protocol/PacketTypes/SetContainerContentPacket.cs`
- **Purpose**: Server sends full container inventory
- **Packet ID**: 0x12 (clientbound)
- **Fields**:
  - `byte WindowId`
  - `int StateId`
  - `List<SlotData> Slots` - All slots in container
  - `SlotData CarriedItem` - Item in cursor

#### `MineSharp/MineSharp.Core/Protocol/PacketTypes/SetContainerSlotPacket.cs`
- **Purpose**: Server updates a single slot
- **Packet ID**: 0x15
- **Fields**:
  - `byte WindowId`
  - `int StateId`
  - `short Slot` - Slot index (-1 = not in window)
  - `SlotData SlotData` - New slot contents

#### `MineSharp/MineSharp.Core/Protocol/PacketTypes/SetHeldItemClientPacket.cs`
- **Purpose**: Server updates player's selected hotbar slot
- **Packet ID**: 0x67
- **Fields**:
  - `byte Slot` - Hotbar slot (0-8)

#### `MineSharp/MineSharp.Core/Protocol/PacketTypes/CloseContainerClientPacket.cs`
- **Purpose**: Server forces client to close container
- **Packet ID**: 0x12 (clientbound - same as serverbound!)
- **Fields**:
  - `byte WindowId`

### 4. Inventory Management

#### `MineSharp/MineSharp.Game/ItemStack.cs`
- **Purpose**: Represents a stack of items
- **Structure**:
  - `int ItemId` - Item/block ID
  - `byte Count` - Stack size (1-64, or up to 127 for certain items)
  - `NbtCompound? Nbt` - Optional NBT data
  - `int? Damage` - Item damage/durability (if applicable)
- **Methods**:
  - `bool IsEmpty` - Check if stack is empty
  - `bool CanStackWith(ItemStack other)` - Check if stacks can merge
  - `ItemStack Split(int amount)` - Split stack
  - `ItemStack? Combine(ItemStack other)` - Try to combine stacks
  - `static ItemStack FromSlotData(SlotData slotData)` - Convert from protocol
  - `SlotData ToSlotData()` - Convert to protocol

### 5. Test Files

#### `MineSharp/MineSharp.Tests/Core/DataTypes/SlotDataTests.cs`
- Unit tests for SlotData serialization/deserialization
- Edge cases (empty slots, NBT data, max stack sizes)

#### `MineSharp/MineSharp.Tests/Core/Protocol/PacketTypes/ClickContainerPacketTests.cs`
- Unit tests for ClickContainerPacket parsing

#### `MineSharp/MineSharp.Tests/Core/Protocol/PacketTypes/SetHeldItemPacketTests.cs`
- Unit tests for SetHeldItemPacket parsing

#### `MineSharp/MineSharp.Tests/Game/InventoryTests.cs`
- Unit tests for Inventory class methods
- Slot management, state ID tracking, hotbar selection

#### `MineSharp/MineSharp.Tests/Game/ItemStackTests.cs`
- Unit tests for ItemStack operations
- Stacking, splitting, combining logic

#### `MineSharp/MineSharp.Tests/Network/Handlers/InventoryHandlerTests.cs` (Integration)
- Integration tests for inventory packet handling
- End-to-end inventory operations

---

## Files to Modify

### 1. Core Protocol

#### `MineSharp/MineSharp.Core/Protocol/PacketParser.cs`
- **Add parsing methods**:
  - `ParseClickContainer(ProtocolReader reader)` - Parse packet 0x11
  - `ParseClickContainerButton(ProtocolReader reader)` - Parse packet 0x10
  - `ParseCloseContainer(ProtocolReader reader)` - Parse packet 0x12 (serverbound)
  - `ParseSetHeldItem(ProtocolReader reader)` - Parse packet 0x34
  - `ParseSetCreativeModeSlot(ProtocolReader reader)` - Parse packet 0x37
  - `ParseSlotData(ProtocolReader reader)` - Helper for parsing slot data
- **Add routing in `ParsePacket`**:
  - `else if (packetId == 0x10)` → `ParseClickContainerButton`
  - `else if (packetId == 0x11)` → `ParseClickContainer`
  - `else if (packetId == 0x12)` → Need to distinguish serverbound vs clientbound (or handle both)
  - `else if (packetId == 0x34)` → `ParseSetHeldItem`
  - `else if (packetId == 0x37)` → `ParseSetCreativeModeSlot`

#### `MineSharp/MineSharp.Core/DataTypes/Position.cs` (if needed)
- May need to check if any position-related parsing is needed for inventory operations

### 2. Network Layer

#### `MineSharp/MineSharp.Network/PacketHandler.cs`
- **Add routing in Play state**:
  - `else if (packetId == 0x10 && packet is ClickContainerButtonPacket buttonPacket)` → `_playHandler.HandleClickContainerButtonAsync`
  - `else if (packetId == 0x11 && packet is ClickContainerPacket clickPacket)` → `_playHandler.HandleClickContainerAsync`
  - `else if (packetId == 0x12 && packet is CloseContainerPacket closePacket)` → `_playHandler.HandleCloseContainerAsync`
  - `else if (packetId == 0x34 && packet is SetHeldItemPacket heldPacket)` → `_playHandler.HandleSetHeldItemAsync`
  - `else if (packetId == 0x37 && packet is SetCreativeModeSlotPacket creativePacket)` → `_playHandler.HandleSetCreativeModeSlotAsync`

#### `MineSharp/MineSharp.Network/Handlers/PlayHandler.cs`
- **Add handler methods**:
  - `HandleClickContainerAsync(ClientConnection connection, ClickContainerPacket packet)`
  - `HandleClickContainerButtonAsync(ClientConnection connection, ClickContainerButtonPacket packet)`
  - `HandleCloseContainerAsync(ClientConnection connection, CloseContainerPacket packet)`
  - `HandleSetHeldItemAsync(ClientConnection connection, SetHeldItemPacket packet)`
  - `HandleSetCreativeModeSlotAsync(ClientConnection connection, SetCreativeModeSlotPacket packet)`
- **Integration with Player**:
  - Get `Player` from `connection.Player` (property exists)
  - Validate `Player` is not null (player may not be fully initialized)
  - Access `player.Inventory` to perform operations
  - Update inventory state
  - Send response packets if needed (SetContainerContent, SetContainerSlot, etc.)

### 3. Game Logic

#### `MineSharp/MineSharp.Game/Inventory.cs`
- **Implement all stub methods**:
  - `SetSlot(int slotIndex, int itemId, int count)` - Set slot contents
  - `GetSlot(int slotIndex)` - Get slot contents (returns ItemStack?)
  - `SetSelectedHotbarSlot(int slot)` - Change selected hotbar slot (0-8)
  - `GetSelectedHotbarSlot()` - Get selected hotbar slot
  - `GetHeldItem()` - Get item in selected hotbar slot
  - `SetCursorItem(int itemId, int count)` - Set cursor item
  - `ClearCursorItem()` - Clear cursor item
  - `IncrementStateId()` - Increment state ID for synchronization
- **Add slot management**:
  - Define slot ranges (hotbar: 0-8, main: 9-35, armor: 36-39, offhand: 40, crafting: 41-44)
  - Validation for slot indices
  - Thread-safety (if needed)
- **Add container support** (future):
  - Track open containers
  - Window ID management
  - Container-specific slots

#### `MineSharp/MineSharp.Game/Player.cs`
- **No changes needed** (already has `Inventory` property)
- **Future enhancement**: Add helper method `GetHeldItem()` that returns `player.Inventory.GetHeldItem()` for convenience

### 4. Block Placement Integration

#### `MineSharp/MineSharp.Network/Handlers/PlayHandler.cs`
- **Modify `HandleUseItemOnAsync`**:
  - Get `Player` from connection
  - Get held item from `player.Inventory.GetHeldItem()`
  - Extract block/item ID from held item
  - Use actual block type instead of hardcoded stone
  - Handle empty hand case (air)

---

## Implementation Phases

### Phase 1: Core Data Structures (Foundation)
1. Create `SlotData.cs` with basic structure
2. Create `ItemStack.cs` with stacking logic
3. Update `Inventory.cs` to use `ItemStack` instead of raw dictionaries
4. Implement basic inventory methods (SetSlot, GetSlot, GetHeldItem)
5. Write unit tests for SlotData and ItemStack

### Phase 2: Packet Types (Protocol Layer)
1. Create serverbound packet types (ClickContainer, SetHeldItem, etc.)
2. Create clientbound packet types (SetContainerContent, SetContainerSlot, etc.)
3. Add parsing methods to `PacketParser.cs`
4. Add routing in `PacketHandler.cs`
5. Write unit tests for packet parsing

### Phase 3: Packet Handling (Network Layer)
1. Implement handler methods in `PlayHandler.cs`
2. Integrate with Player.Inventory
3. Implement basic click handling (simple slot swaps)
4. Implement held item changes
5. Update block placement to use held item
6. Write integration tests

### Phase 4: Advanced Features (Polish)
1. Implement creative mode slot setting
2. Implement container button clicks
3. Implement container opening/closing
4. Add state ID synchronization
5. Add validation and error handling
6. Optimize with hashed slot data (if needed)

### Phase 5: Testing & Refinement
1. Comprehensive integration tests
2. Edge case handling
3. Thread-safety verification
4. Performance testing

---

## Design Decisions

### 1. Slot Numbering
- **Player Inventory**: 
  - 0: Crafting output
  - 1-4: 2x2 crafting grid
  - 5-8: Armor slots (5=boots, 6=leggings, 7=chestplate, 8=helmet)
  - 9-35: Main inventory (27 slots)
  - 36-44: Hotbar (9 slots)
- **Window ID**: 0 = player inventory (always open)

### 2. State ID Synchronization
- Each inventory operation increments state ID
- Client sends expected state ID in packets
- Server validates state ID matches
- Server sends new state ID in responses
- Prevents desync from out-of-order packets

### 3. Item Stack Representation
- Use `ItemStack` class instead of raw tuples
- Provides type safety and validation
- Supports future NBT/damage extensions
- Easy conversion to/from protocol format

### 4. Thread Safety
- `Inventory` class may need thread-safety if accessed from multiple threads
- Consider using locks or concurrent collections
- Player instance may be accessed from network thread and game thread

### 5. NBT Data
- Initially support `null` NBT (most items don't have NBT)
- Add full NBT parsing later if needed
- Use existing NBT library if available, or simple structure

---

## Dependencies

### Existing
- ✅ `ProtocolReader` - For packet parsing
- ✅ `ClientConnection` - For packet handling
- ✅ `Player` - Has Inventory property
- ✅ `PlayHandler` - For packet handlers

### New/Needed
- NBT parsing library (or simple NBT structure) - Currently NBT is `byte[]`, can use that initially
- `ChatComponent` for window titles - May need to create simple structure
- Connection → Player lookup - ✅ `ClientConnection.Player` property exists

---

## Testing Strategy

### Unit Tests
- **SlotData**: Empty slots, item slots, NBT handling
- **ItemStack**: Stacking, splitting, combining, validation
- **Inventory**: Slot setting/getting, hotbar selection, state IDs
- **Packet Parsing**: All inventory packet types

### Integration Tests
- Click container → inventory updates
- Set held item → block placement uses correct item
- Creative mode slot → inventory reflects change
- State ID synchronization across operations

### Manual Testing
- Connect with Minecraft client
- Perform inventory operations
- Verify server state matches client
- Test edge cases (empty slots, full inventory, etc.)

---

## Future Enhancements (Out of Scope for Initial Implementation)

1. **Container Support**: Chests, furnaces, crafting tables, etc.
2. **Item Durability**: Track and update item damage
3. **Enchantments**: Parse and manage item enchantments
4. **Item Effects**: Apply item-specific effects (tools, weapons)
5. **Crafting**: Implement crafting recipe system
6. **Persistence**: Save/load inventory to disk
7. **Creative Mode**: Full creative inventory management
8. **Slot Locking**: Prevent slot changes in certain situations
9. **Inventory Restrictions**: Prevent placing items in certain slots (e.g., non-armor in armor slots)

---

## Questions to Resolve

1. **NBT Library**: Currently NBT is `byte[]` in the codebase. Should we use `byte[]` initially or create a structured type? **Decision**: Use `byte[]` initially, can add structured parsing later if needed.
2. **Connection → Player Lookup**: ✅ **Resolved** - `ClientConnection.Player` property exists and can be accessed directly.
3. **ChatComponent**: Does this exist, or do we need to create it for window titles? **Decision**: Create simple structure initially (can be JSON string for now).
4. **Window ID Management**: How do we handle window IDs for containers? Auto-increment? **Decision**: Start with window ID 0 (player inventory), add auto-increment for containers later.
5. **Thread Safety**: Do we need thread-safe Inventory operations? **Decision**: Add locks to Inventory methods since they may be accessed from network thread and game thread.

---

## Estimated Complexity

- **SlotData/ItemStack**: Low (simple data structures)
- **Inventory Implementation**: Medium (slot management, state tracking)
- **Packet Parsing**: Medium (multiple packet types, slot data arrays)
- **Packet Handling**: High (complex click logic, state synchronization)
- **Integration**: Low (already has Inventory property)

**Total Estimated Time**: 
- Phase 1: 2-3 hours
- Phase 2: 3-4 hours
- Phase 3: 4-6 hours
- Phase 4: 2-3 hours
- Phase 5: 2-3 hours
**Total**: ~13-19 hours of focused development




