# Slot Data Format Update

## Issue
The `ParseSlotData` method was using the **old NBT-based format** from pre-1.20.5 versions of Minecraft, which caused parsing errors when receiving modern packets.

## Old Format (Pre-1.20.5)
```
1. Boolean - Present (whether slot has an item)
2. If Present:
   a. VarInt - Item ID
   b. Byte - Item Count
   c. Boolean - Has NBT
   d. If Has NBT: NBT data
```

## New Format (1.20.5+)
```
1. VarInt - Item Count (if 0, slot is empty, no further fields)
2. VarInt - Item ID (only if count > 0)
3. VarInt - Number of components to add
4. VarInt - Number of components to remove
5. Array of components to add:
   - VarInt - Component type
   - Varies - Component data (depends on type)
6. Array of components to remove:
   - VarInt - Component type (no data)
```

## Changes Made

### PacketParser.cs
- **Updated `ParseSlotData`**: Now reads the modern format starting with Item Count as VarInt
- **Added `SkipComponentData`**: Skips component data based on component type
- **Added `SkipEnchantments`**: Helper to skip enchantment component data
- **Added `SkipBlockPredicate`**: Helper to skip block predicate component data

### Component Types Implemented (Partial)
Currently handles basic skipping for:
- 0: custom_data (NBT)
- 1: max_stack_size (VarInt)
- 2: max_damage (VarInt)
- 3: damage (VarInt)
- 4: unbreakable (Boolean)
- 5: custom_name (String)
- 6: item_name (String)
- 7: lore (Array of Strings)
- 8: rarity (VarInt)
- 9: enchantments (Complex)
- 10: can_place_on (Block predicate)
- 11: can_break (Block predicate)

Unknown component types attempt a fallback skip mechanism.

## Creative Mode Behavior

### Key Insight
When a player clicks in the **creative menu** to pick up an item:
- **No packet is sent** at that moment
- The client handles this entirely locally

When the player **places** that item somewhere:
- `Set Creative Mode Slot` (0x37) is sent with:
  - Slot number (0-45 for inventory, -1 for dropping)
  - Item data (using the new Slot format)

### Related Packets
- **Pick Item From Block (0x23)**: Middle-click on a block
- **Pick Item From Entity (0x24)**: Middle-click on an entity

## TODO
- [ ] Implement full component parsing for all component types (currently ~80+ types)
- [ ] Store component data in SlotData/ItemStack for proper item handling
- [ ] Add tests for new Slot Data format parsing
- [x] Update PacketBuilder to write the new format for clientbound packets

## PacketBuilder Changes

### Updated Methods
- **`WriteSlotData`** (new private helper): Writes Slot Data in the modern format
  - Writes VarInt item count (0 for empty)
  - Writes VarInt item ID (if count > 0)
  - Writes VarInt components to add count (0 for now)
  - Writes VarInt components to remove count (0 for now)
  
- **`BuildSetContainerSlotPacket`**: Now uses `WriteSlotData` helper
- **`BuildSetContainerContentPacket`**: Now uses `WriteSlotData` helper for all slots and carried item

## References
- Protocol docs: `docs/protocol/Java Edition protocol_Slot Data – Minecraft Wiki.html`
- Protocol docs: `docs/protocol/Java Edition protocol_Packets – Minecraft Wiki.html` (Set Creative Mode Slot section)




