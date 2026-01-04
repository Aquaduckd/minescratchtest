# Block Manager Integration Plan

## Overview
The `BlockManager` class will serve as the single source of truth for all block data. This document outlines how it will integrate with existing systems.

## Integration Points

### 1. World Class (`packet_debug_server.py`)

**Current State:**
- `World` class has its own `block_data` dictionary
- Methods: `get_block_at()`, `set_block()`, `generate_chunk_section_blocks()`, etc.

**New State:**
- `World` class will have a `BlockManager` instance
- `World` methods will delegate to `BlockManager`
- Example:
  ```python
  class World:
      def __init__(self):
          self.block_manager = BlockManager()
      
      def get_block_at(self, x, y, z):
          return self.block_manager.get_block(x, y, z)
      
      def set_block(self, x, y, z, block_id):
          return self.block_manager.set_block(x, y, z, block_id)
  ```

### 2. Protocol Builder (`minecraft_protocol.py`)

**Current State:**
- `PacketBuilder.build_chunk_data()` generates blocks on-the-fly
- No access to stored block data

**New State:**
- `build_chunk_data()` will accept a `BlockManager` parameter
- Will query `BlockManager` for chunk section data
- Example:
  ```python
  def build_chunk_data(
      chunk_x: int,
      chunk_z: int,
      block_manager: BlockManager,  # NEW
      flat_world: bool = True,
      ground_y: int = 64
  ) -> bytes:
      # Query block_manager for each section
      for section_y in range(24):
          block_count, palette, palette_indices = \
              block_manager.get_chunk_section_for_protocol(
                  chunk_x, chunk_z, section_y
              )
          # Use this data to build packet...
  ```

### 3. Chunk Loading (`packet_debug_server.py`)

**Current State:**
```python
chunk_data = PacketBuilder.build_chunk_data(...)
if world:
    world.load_chunk_blocks(chunk_x, chunk_z, ground_y=64)
```

**New State:**
```python
# Load chunk in block manager first
if world and world.block_manager:
    world.block_manager.load_chunk(chunk_x, chunk_z, ground_y=64)

# Then generate packet from block manager
chunk_data = PacketBuilder.build_chunk_data(
    chunk_x=chunk_x,
    chunk_z=chunk_z,
    block_manager=world.block_manager,  # NEW
    flat_world=True,
    ground_y=64
)
```

### 4. Block Breaking (`packet_debug_server.py`)

**Current State:**
```python
world.set_block(x, y, z, 0)  # Update internal world
block_update = PacketBuilder.build_block_update(x, y, z, 0)  # Send packet
```

**New State:**
```python
# Single update through block manager
world.block_manager.set_block(x, y, z, 0)

# Generate packet from current state
block_update = PacketBuilder.build_block_update(x, y, z, 0)
# (build_block_update can optionally verify with block_manager)
```

### 5. Block Placing (`packet_debug_server.py`)

**Current State:**
```python
world.set_block(place_x, place_y, place_z, block_state_id)
block_update = PacketBuilder.build_block_update(...)
```

**New State:**
```python
# Single update through block manager
world.block_manager.set_block(place_x, place_y, place_z, block_state_id)
block_update = PacketBuilder.build_block_update(...)
```

### 6. Collision Detection (`packet_debug_server.py`)

**Current State:**
```python
block_id = self.get_block_at(check_x, check_y, check_z)
```

**New State:**
```python
block_id = self.block_manager.get_block(check_x, check_y, check_z)
```

## Migration Strategy

### Phase 1: Create BlockManager (DONE)
- ✅ Scaffold `BlockManager` class with all methods
- ✅ Define interfaces and data structures

### Phase 2: Implement BlockManager
- Implement all `BlockManager` methods
- Add unit tests
- Verify block storage/retrieval works correctly

### Phase 3: Integrate with World Class
- Add `BlockManager` instance to `World.__init__()`
- Refactor `World` methods to delegate to `BlockManager`
- Keep old methods as wrappers for backward compatibility

### Phase 4: Integrate with Protocol Builder
- Modify `build_chunk_data()` to accept `BlockManager`
- Update all call sites to pass `BlockManager` instance
- Remove duplicate generation logic from protocol builder

### Phase 5: Update Block Operations
- Update block breaking to use `BlockManager`
- Update block placing to use `BlockManager`
- Ensure all block modifications go through `BlockManager`

### Phase 6: Cleanup
- Remove old `World.block_data` dictionary
- Remove duplicate generation methods
- Remove unused code

## Benefits After Integration

1. **Single Source of Truth**: All block data in one place
2. **No Desynchronization**: Protocol and game logic use same data
3. **No Code Duplication**: Generation logic in one place
4. **Easier Testing**: Can test block operations independently
5. **Better Performance**: Generate once, use many times
6. **Easier to Extend**: Add new features (block history, undo/redo, etc.)

