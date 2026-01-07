# Chunk Batching on Player Spawn

## Overview
When a player first spawns, chunks are loaded in **two batches** to ensure immediate ground rendering while loading the full view distance in the background.

## Spawn Position
- **World Position**: `(0, 65, 0)`
- **Chunk Coordinates**: `(0, 0)`
- **View Distance**: `10` (default)

## Batch 1: Spawn Chunks (Synchronous - 9 chunks)

**Purpose**: Ensure immediate ground exists when player spawns

**Chunks Loaded**: 3x3 grid around spawn chunk `(0, 0)`

```
(-1, -1)  (-1, 0)  (-1, 1)
( 0, -1)  ( 0, 0)  ( 0, 1)  ← Spawn chunk (center)
( 1, -1)  ( 1, 0)  ( 1, 1)
```

**Total**: 9 chunks

**Loading Method**:
1. `ChunkLoader.UpdateDesiredChunks()` is called with the 3x3 grid
2. ChunkLoader starts loading chunks in background threads
3. Code **waits synchronously** (up to 5 seconds) for all 9 chunks to finish loading
4. Only after all 9 chunks are loaded does the code proceed

**Why Synchronous**: Player position is sent immediately after, so we need ground to exist

## Batch 2: Full View Distance (Asynchronous - 441 total chunks)

**Purpose**: Load the complete view distance around the player

**Chunks Loaded**: Square region with view distance 10

**Calculation**:
- View distance 10 = 10 chunks in each direction from center
- Total: `(10 * 2 + 1) × (10 * 2 + 1) = 21 × 21 = 441 chunks`

**Chunk Coordinates Range**:
- X: `-10` to `+10` (relative to spawn chunk 0)
- Z: `-10` to `+10` (relative to spawn chunk 0)
- Absolute: `(-10, -10)` to `(10, 10)`

**Loading Method**:
1. `ChunkLoader.UpdateDesiredChunks()` is called with all 441 chunks
2. ChunkLoader processes this in the background
3. The 9 spawn chunks are already loaded, so only **432 new chunks** need loading
4. Loading happens asynchronously - player can move around while chunks load

## Complete Flow

```
1. Player enters Play state
   ↓
2. ChunkLoader created and started
   ↓
3. Batch 1: Load 3x3 spawn chunks (synchronous wait)
   - Chunks: (-1,-1) to (1,1) = 9 chunks
   - Wait up to 5 seconds for completion
   ↓
4. Send player position packet (spawn at 0, 65, 0)
   ↓
5. Batch 2: Load full view distance (asynchronous)
   - Total: 21×21 = 441 chunks
   - Already loaded: 9 chunks (from Batch 1)
   - Remaining: 432 chunks
   - Loaded in background by ChunkLoader
```

## Chunk Loading Order (Within Batches)

The ChunkLoader's background task processes chunks in the order they appear in the desired chunks set. However, multiple chunks can load concurrently since each chunk gets its own loading task.

## Visual Representation

```
Full View Distance (21×21 = 441 chunks):
┌─────────────────────────────────────────┐
│                                         │
│  ┌─────────┐                            │
│  │ Batch 1 │  ← 3×3 spawn chunks       │
│  │  (9)    │     (loaded first)         │
│  └─────────┘                            │
│                                         │
│         ↑                               │
│    Spawn chunk (0, 0)                   │
│                                         │
│  Batch 2: Remaining 432 chunks          │
│  (loaded in background)                 │
│                                         │
└─────────────────────────────────────────┘
```

## Key Points

1. **Spawn chunks are prioritized**: The 3x3 grid loads first and synchronously
2. **Player spawns quickly**: Only 9 chunks need to load before player appears
3. **Full view distance loads in background**: Remaining 432 chunks load asynchronously
4. **No blocking**: After spawn chunks, player can move while other chunks load
5. **Square region**: Minecraft uses square regions, not circular (21×21 for view distance 10)

