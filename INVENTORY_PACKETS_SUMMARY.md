# Inventory-Related Packets Summary

Based on the Minecraft Java Edition protocol documentation, here are the inventory-related packets:

## Serverbound Packets (Client → Server)

### 1. **Click Container** (Packet ID: **0x11**, serverbound)
**Purpose**: Sent when a player clicks a slot in an inventory window (chest, player inventory, etc.)

**Structure**:
- **Window ID** (VarInt): The ID of the window that was clicked. 0 for player inventory
- **State ID** (VarInt): The last received State ID from server (for synchronization)
- **Slot** (Short): The slot number that was clicked
- **Button** (Byte): The mouse button used (0=left, 1=right, etc.)
- **Mode** (VarInt): Click mode (0=Click, 1=Shift+Click, 2=Number Key, 3=Middle Click, 4=Drop, 5=Drag, 6=Double Click)
- **Changed Slots** (Array): Array of slot changes expected by client
  - Each entry: **Slot Number** (Short) + **Hashed Slot Data**
- **Carried Item** (Hashed Slot): The item stack the player is carrying
- **Sequence** (VarInt): Block change sequence number (for block placement)

**Notes**:
- The server compares the client's expected results with actual results
- If slots don't match, server sends `Set Container Slot` packets
- If State ID doesn't match, server sends full `Set Container Content`

### 2. **Click Container Button** (Packet ID: **0x10**, serverbound)
**Purpose**: Sent when a player clicks a button in a container (e.g., brewing stand button, furnace fuel button)

**Structure**:
- **Window ID** (VarInt): The window ID
- **Button ID** (VarInt): The button that was clicked (meaning depends on window type)

### 3. **Close Container** (Packet ID: **0x12**, serverbound - "container_close")
**Purpose**: Sent when a player closes an inventory window

**Structure**:
- **Window ID** (VarInt): The ID of the window being closed

**Notes**:
- Vanilla clients send Close Window packet with Window ID 0 to close their inventory
- Even though there's never an `Open Screen` packet for the inventory (ID 0)

### 4. **Set Held Item (serverbound)** (Packet ID: **0x34**, serverbound - "set_carried_item")
**Purpose**: Sent when a player changes their selected hotbar slot (scrolls through hotbar)

**Structure**:
- **Slot** (VarInt): The new hotbar slot (0-8)

### 5. **Set Creative Mode Slot** (Packet ID: **0x37**, serverbound - "set_creative_mode_slot")
**Purpose**: Sent when a player places or removes an item in creative mode inventory (middle-click also sends this)

**Structure**:
- **Slot** (Short): The slot number (-1 for outside inventory)
- **Clicked Item** (Slot Data): The item stack being placed/removed

## Clientbound Packets (Server → Client)

### 1. **Open Screen** (Packet ID: varies)
**Purpose**: Instructs the client to open a container window (chest, crafting table, etc.)

**Structure**:
- **Window ID** (VarInt): Unique identifier for the window (server counter starting at 1)
- **Window Type** (VarInt): The type of window (chest, crafting table, furnace, etc.)
- **Window Title** (Chat): The title to display for the window

**Notes**:
- Window ID 0 is reserved for player inventory (never sent via Open Screen)
- Window IDs wrap around after 100 in vanilla implementation

### 2. **Set Container Content** (Packet ID: **0x12**, clientbound - "container_set_content")
**Purpose**: Replaces all contents of a container window

**Structure**:
- **Window ID** (Unsigned Byte): The window ID (0 for player inventory)
- **State ID** (VarInt): Sequence number for synchronization
- **Slot Count** (VarInt): Number of slots
- **Slots** (Array of Slot Data): All item stacks in the container
- **Carried Item** (Slot Data): The item stack the player is carrying

**When Sent**:
- Upon initialization of a container window
- Upon initialization of player inventory
- In response to state ID mismatches

### 3. **Set Container Slot** (Packet ID: **0x14**, clientbound - "container_set_slot")
**Purpose**: Updates a single slot in a container window

**Structure**:
- **Window ID** (Unsigned Byte): The window ID (0 for player inventory)
- **State ID** (VarInt): Sequence number for synchronization
- **Slot** (Short): The slot index to update
- **Slot Data** (Slot Data): The item stack to place in that slot

**Notes**:
- Used to correct mismatches after `Click Container` packets
- Window ID 0 updates can be sent even when another window is open (for hotbar/offhand)

### 4. **Set Container Property** (Packet ID: varies)
**Purpose**: Updates properties of a container (e.g., furnace fuel time, brewing progress)

**Structure**:
- **Window ID** (Unsigned Byte): The window ID
- **Property** (Short): The property ID (varies by window type)
- **Value** (Short): The property value

### 5. **Close Container (clientbound)** (Packet ID: varies)
**Purpose**: Forces the client to close an inventory window

**Structure**:
- **Window ID** (Unsigned Byte): The window ID to close

**Notes**:
- Sent when a chest is destroyed while open, for example
- Client disregards the window ID and closes any active window

### 6. **Set Held Item (clientbound)** (Packet ID: 0x67)
**Purpose**: Updates which slot the player is holding

**Structure**:
- **Slot** (Byte): The new hotbar slot (0-8)

### 7. **Change Container Slot State** (Packet ID: 0x13)
**Purpose**: Updates the visual state of a slot (e.g., locked, highlighted)

**Structure**:
- **Window ID** (VarInt): The window ID
- **Slot** (Short): The slot index
- **Slot State ID** (VarInt): The state ID (e.g., 1=locked, 2=unlocked)

## Slot Data Structure

There are **two formats** for Slot Data:

### Regular Slot Data Format
Used in: Set Container Content, Set Container Slot, Set Creative Mode Slot, etc.

**Format**:
1. **Item Count** (VarInt):
   - `0`: Empty slot (no item)
   - `>0`: Item stack exists, read following fields
   
2. **Item Stack** (if Item Count > 0):
   - **Item ID** (VarInt, Optional): Registry ID of the item
   - **Number of components to add** (VarInt, Optional)
   - **Number of components to remove** (VarInt, Optional)
   - **Components to add** (Array): Structured component data
   - **Components to remove** (Array): Component types to remove

### Hashed Slot Data Format
Used in: **Click Container** packet only (for synchronization optimization)

**Format**:
1. **Has Item** (Boolean): Whether slot contains an item
2. **Item ID** (VarInt, Optional): Registry ID of the item (if Has Item = true)
3. **Item Count** (VarInt, Optional): Stack size (if Has Item = true)
4. **Components to add** (Array): Component type (VarInt) + Component data hash (Int - CRC32C checksum)
5. **Components to remove** (Array): Component types to remove

**Notes**:
- Hashed format uses **CRC32C checksums** (not CRC32) for component data
- Used in Click Container to reduce packet size
- Slot data uses structured components for 1.21+ (not raw NBT)
- Older versions (pre-1.21) used raw NBT data

## Window ID System

- **Window ID 0**: Player inventory (always available)
- **Window ID 1+**: Container windows (chest, crafting table, etc.)
- **Window ID -1**: No window / outside inventory

## Important Notes

1. **Synchronization**: Uses State ID sequence numbers to prevent desynchronization
2. **Window ID 0 Special Cases**:
   - Always available (player inventory)
   - Can be updated even when other windows are open (for hotbar/offhand)
   - Armor and offhand slots only exist on Window ID 0
3. **Click Modes**:
   - Normal click, shift-click, number key, middle click, drag, drop, double-click
4. **Creative Mode**: Has special packet `Set Creative Mode Slot` for creative inventory management
5. **Slot Numbers**:
   - Player inventory: 0-44 (9 hotbar + 27 main + 4 armor + 1 offhand + 1-2 crafting)
   - Container slots vary by window type
   - -999 or -1 indicates outside/outside inventory

## Current Implementation Status

- ✅ **Use Item On** (0x3F) - Block placement (handled)
- ❌ **Click Container** - Not implemented
- ❌ **Close Container** (serverbound) - Not implemented
- ❌ **Set Held Item** (serverbound) - Not implemented
- ❌ **Set Creative Mode Slot** - Not implemented
- ❌ **Slot Data** parsing - Not implemented

## Priority for Block Placement Feature

To properly get the block type for block placement, we need:
1. **Click Container** packet handling - to track player inventory
2. **Set Container Content** packet - to receive initial inventory state
3. **Set Container Slot** packet - to receive inventory updates
4. **Slot Data** parsing - to extract item/block types from inventory

However, for a minimal implementation, we could:
- Use a hardcoded default block (currently stone, block ID 1)
- Or implement a simple inventory system that tracks the player's held item

The **Use Item On** packet's `Hand` field (0=Main Hand, 1=Off Hand) tells us which hand is being used, but we'd need to query the player's inventory to get what item is in that hand.



