# Multiplayer Player Visibility - Scaffolding Plan

## Overview
This document outlines the scaffolding needed to implement multiplayer player visibility, allowing clients to see other players in the game world.

## Core Components Needed

### 1. Entity ID Management
**Location**: `MineSharp.World.EntityManager.cs`

**What's needed**:
- Implement `GetNextEntityId()` - Generate unique entity IDs for players
- Implement `SpawnEntity(Entity entity)` - Register entity with entity ID
- Implement `RemoveEntity(int entityId)` - Unregister entity
- Implement `GetEntity(int entityId)` - Retrieve entity by ID
- Store entity ID in `Player` class

**Entity ID Strategy**:
- Players should get entity IDs starting from 1 (first player = 1, second = 2, etc.)
- Non-player entities start from 1000 (already configured)
- Entity IDs must be unique and never reused while server is running

### 2. Player Entity ID Tracking
**Location**: `MineSharp.Game.Player.cs`

**What's needed**:
- Add `int EntityId { get; }` property to `Player` class
- Entity ID should be assigned when player is created/added to world
- Entity ID should be immutable (set once, never changed)

### 3. Entity Visibility Tracking
**Location**: New file `MineSharp.Game/PlayerVisibilityTracker.cs` or add to `Player.cs`

**What's needed**:
- Track which entities (players) are currently visible to each player
- `HashSet<int> VisibleEntityIds` - entity IDs that this player can see
- Thread-safe operations (similar to chunk tracking)
- Methods:
  - `AddVisibleEntity(int entityId)` - Mark entity as visible
  - `RemoveVisibleEntity(int entityId)` - Mark entity as not visible
  - `IsEntityVisible(int entityId)` - Check if entity is visible
  - `GetVisibleEntities()` - Get all visible entity IDs

### 4. Player Visibility Manager
**Location**: New file `MineSharp.Network/PlayerVisibilityManager.cs`

**What's needed**:
- Manages which players should be visible to each client
- Similar to `ChunkLoader` but for player entities
- Responsibilities:
  - When player joins: spawn them for all existing players, spawn all existing players for them
  - When player moves: update visibility based on distance
  - When player disconnects: despawn them for all other players
  - Track view distance (e.g., 48 blocks = 3 chunks)

**Key Methods**:
- `OnPlayerJoined(ClientConnection connection, Player player)` - Handle new player
- `OnPlayerMoved(Player player)` - Update visibility when player moves
- `OnPlayerDisconnected(Player player)` - Clean up on disconnect
- `UpdateVisibilityForPlayer(Player viewer, Player target)` - Check if target should be visible
- `SpawnPlayerForClient(ClientConnection connection, Player playerToSpawn)` - Send spawn packet
- `DespawnPlayerForClient(ClientConnection connection, int entityId)` - Send despawn packet

### 5. Packet Builders
**Location**: `MineSharp.Core.Protocol.PacketBuilder.cs`

**What's needed**:
- `BuildSpawnEntityPacket(...)` - Spawn Entity (0x01)
  - Parameters: entityId, uuid, entityType, position, velocity, yaw, pitch, headYaw, data
- `BuildRemoveEntitiesPacket(...)` - Remove Entities (0x4B)
  - Parameters: entityIds array
- `BuildUpdateEntityPositionPacket(...)` - Update Entity Position (0x33)
  - Parameters: entityId, deltaX, deltaY, deltaZ, onGround
- `BuildUpdateEntityPositionAndRotationPacket(...)` - Update Entity Position and Rotation (0x34)
  - Parameters: entityId, deltaX, deltaY, deltaZ, yaw, pitch, onGround
- `BuildUpdateEntityRotationPacket(...)` - Update Entity Rotation (0x36)
  - Parameters: entityId, yaw, pitch, onGround
- `BuildTeleportEntityPacket(...)` - Teleport Entity (0x7B)
  - Parameters: entityId, x, y, z, yaw, pitch, onGround

**Data Types Needed**:
- `LpVec3` - Low precision Vec3 (for velocity in spawn packet)
- `Angle` - Angle encoding (for yaw/pitch)
- Fixed-point encoding for position deltas (12 fraction bits, 4 integer bits)

### 6. Entity Type Registry Lookup
**Location**: New utility or add to existing registry system

**What's needed**:
- Lookup player entity type ID from `minecraft:entity_type` registry
- The entity type ID is needed for Spawn Entity packet
- Should be cached/looked up once at startup

### 7. Position Update Broadcasting
**Location**: `MineSharp.Network.Handlers.PlayHandler.cs`

**What's needed**:
- When player position updates (in `HandleSetPlayerPositionAsync`):
  - Broadcast position to nearby players (within view distance)
  - Use appropriate packet based on movement distance:
    - Small movement (< 8 blocks): `UpdateEntityPosition` or `UpdateEntityPositionAndRotation`
    - Large movement (>= 8 blocks): `TeleportEntity`
- Track last sent position per player to calculate deltas

### 8. Integration Points

#### A. Player Join Flow
**Location**: `PlayHandler.SendInitialPlayPacketsAsync()`

**What's needed**:
- After player is created and added to world:
  - Assign entity ID to player
  - Register player in EntityManager
  - Call `PlayerVisibilityManager.OnPlayerJoined()` to:
    - Spawn new player for all existing players
    - Spawn all existing players for new player

#### B. Player Movement Flow
**Location**: `PlayHandler.HandleSetPlayerPositionAsync()` and `HandleSetPlayerPositionAndRotationAsync()`

**What's needed**:
- After updating player position:
  - Call `PlayerVisibilityManager.OnPlayerMoved()` to:
    - Update visibility for this player (check which players should be visible)
    - Broadcast position updates to nearby players

#### C. Player Disconnect Flow
**Location**: `ClientConnection.Disconnect()` or cleanup handler

**What's needed**:
- When player disconnects:
  - Call `PlayerVisibilityManager.OnPlayerDisconnected()` to:
    - Despawn player for all other players
    - Remove player from EntityManager
    - Clean up visibility tracking

### 9. View Distance Configuration
**Location**: `MineSharp.World.World.cs` or `MineSharp.Game.Player.cs`

**What's needed**:
- Define player view distance (e.g., 48 blocks = 3 chunks)
- Use for entity visibility calculations
- Can reuse existing `ViewDistance` from `Player` class (currently in chunks, convert to blocks)

### 10. Distance Calculation Utilities
**Location**: New utility class or add to existing

**What's needed**:
- `CalculateDistance(Player p1, Player p2)` - Calculate distance between players
- `IsWithinViewDistance(Player viewer, Player target, double viewDistance)` - Check if target is visible
- Use Euclidean distance: `sqrt((x1-x2)² + (y1-y2)² + (z1-z2)²)`
- Or Manhattan distance for performance: `|x1-x2| + |y1-y2| + |z1-y2|`

## File Structure

```
MineSharp/
├── MineSharp.Game/
│   └── Player.cs (modify: add EntityId property)
│
├── MineSharp.World/
│   ├── EntityManager.cs (implement: entity ID generation, entity tracking)
│   └── World.cs (modify: ensure EntityManager is used)
│
├── MineSharp.Network/
│   ├── Handlers/
│   │   └── PlayHandler.cs (modify: integrate visibility manager)
│   ├── PlayerVisibilityManager.cs (new: main visibility management)
│   └── ClientConnection.cs (modify: add visibility manager reference)
│
└── MineSharp.Core.Protocol/
    └── PacketBuilder.cs (modify: add entity packet builders)
```

## Implementation Order

1. **Entity ID Management** (EntityManager)
   - Implement `GetNextEntityId()`
   - Implement basic entity tracking
   - Add EntityId to Player class

2. **Packet Builders** (PacketBuilder)
   - Implement all 6 entity-related packet builders
   - Test packet encoding/decoding

3. **Entity Visibility Tracking** (Player class)
   - Add visible entities tracking to Player
   - Thread-safe operations

4. **Player Visibility Manager** (new file)
   - Core visibility logic
   - Spawn/despawn methods
   - Distance-based visibility checks

5. **Integration** (PlayHandler)
   - Hook into player join flow
   - Hook into player movement flow
   - Hook into player disconnect flow

6. **Position Broadcasting** (PlayHandler)
   - Broadcast position updates
   - Choose correct packet type based on movement distance

## Testing Considerations

- Test with 2 players: verify they can see each other
- Test with 3+ players: verify all players see each other
- Test movement: verify position updates are broadcast
- Test view distance: verify players outside range are despawned
- Test disconnect: verify player is removed for others
- Test rapid movement: verify no packet spam
- Test entity ID uniqueness: verify no conflicts

## Notes

- Entity IDs for players should start at 1 (not 1000)
- View distance should be configurable (default: 48 blocks = 3 chunks)
- Position updates should be throttled (don't send every tiny movement)
- Use appropriate packet type based on movement distance
- Thread safety is critical (multiple players moving simultaneously)

