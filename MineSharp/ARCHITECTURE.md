# MineSharp Architecture

## Overview

This document outlines the architecture for the C# Minecraft server implementation. The design focuses on:
- **Clear separation of concerns** - Each layer has a single responsibility
- **Thread safety** - All state is properly synchronized
- **Scalability** - Designed to handle multiple concurrent connections
- **Maintainability** - Clean, testable code with clear boundaries

## Project Structure

```
MineSharp/
├── MineSharp.Core/              # Core protocol and data types
│   ├── Protocol/
│   │   ├── ConnectionState.cs
│   │   ├── PacketDirection.cs
│   │   ├── ProtocolReader.cs
│   │   ├── ProtocolWriter.cs
│   │   └── PacketTypes/        # Packet definitions
│   ├── DataTypes/              # Minecraft data types (VarInt, Position, etc.)
│   └── Models/                 # Data models (Player, Entity, etc.)
│
├── MineSharp.Network/          # Network layer
│   ├── TcpServer.cs            # Main TCP server
│   ├── ClientConnection.cs     # Per-connection handler
│   ├── PacketHandler.cs        # Packet routing/dispatching
│   └── Handlers/               # State-specific packet handlers
│       ├── HandshakingHandler.cs
│       ├── LoginHandler.cs
│       ├── ConfigurationHandler.cs
│       └── PlayHandler.cs
│
├── MineSharp.World/            # World state management
│   ├── World.cs                # Main world state container
│   ├── ChunkManager.cs         # Chunk loading/unloading
│   ├── BlockManager.cs          # Block storage and queries
│   ├── EntityManager.cs        # Entity lifecycle and updates
│   └── PlayerManager.cs        # Player state management
│
├── MineSharp.Game/             # Game logic
│   ├── Player.cs                # Player state and behavior
│   ├── Entity.cs                # Base entity class
│   ├── ItemEntity.cs            # Item entity implementation
│   └── GameEvents.cs            # Game event system
│
├── MineSharp.Data/             # Static data and registries
│   ├── RegistryManager.cs      # Registry data loading
│   ├── LootTableManager.cs     # Loot table data
│   └── DataLoader.cs            # JSON data loading utilities
│
└── MineSharp.Server/            # Server entry point and orchestration
    ├── Program.cs               # Entry point
    ├── ServerConfiguration.cs   # Server settings
    └── Server.cs                # Main server orchestration
```

## State Segregation

### 1. Connection State (Per-Client)

**Location**: `MineSharp.Network.ClientConnection`

**Responsibilities**:
- TCP socket management
- Packet reading/writing
- Connection lifecycle (connect, disconnect)
- Current protocol state (Handshaking, Login, Configuration, Play)

**Thread Safety**: Each connection runs on its own thread/task. No shared mutable state.

```csharp
public class ClientConnection
{
    private TcpClient _client;
    private ConnectionState _state;
    private Guid _connectionId;
    private CancellationTokenSource _cancellationToken;
    
    // Connection-specific state
    public ConnectionState State { get; private set; }
    public Guid ConnectionId { get; }
    public Player? Player { get; set; }  // Set when entering PLAY state
}
```

### 2. Player State (Per-Player)

**Location**: `MineSharp.Game.Player`

**Responsibilities**:
- Player position, rotation, inventory
- Chunk visibility tracking
- Client settings (view distance, etc.)
- Player-specific game state

**Thread Safety**: 
- Player state is owned by `World.PlayerManager`
- All modifications go through `PlayerManager` methods
- Player position updates are synchronized

```csharp
public class Player
{
    public Guid Uuid { get; }
    public Vector3 Position { get; private set; }
    public float Yaw { get; private set; }
    public float Pitch { get; private set; }
    public int ViewDistance { get; }
    public Inventory Inventory { get; }
    public HashSet<(int X, int Z)> LoadedChunks { get; }
    
    // Thread-safe position update
    public void UpdatePosition(Vector3 newPosition);
}
```

### 3. World State (Shared, Thread-Safe)

**Location**: `MineSharp.World.World`

**Responsibilities**:
- Central container for all game state
- Coordinates between subsystems
- Thread-safe access to shared state

**Thread Safety**: 
- All public methods are thread-safe
- Uses locks/ConcurrentDictionary for shared collections
- Immutable snapshots for read operations where possible

```csharp
public class World
{
    private readonly PlayerManager _playerManager;
    private readonly EntityManager _entityManager;
    private readonly ChunkManager _chunkManager;
    private readonly BlockManager _blockManager;
    
    // Thread-safe accessors
    public Player? GetPlayer(Guid uuid);
    public void AddPlayer(Player player);
    public void RemovePlayer(Guid uuid);
    
    // World updates (called on main game loop)
    public void Tick(TimeSpan deltaTime);
}
```

### 4. Block State (Shared, Thread-Safe)

**Location**: `MineSharp.World.BlockManager`

**Responsibilities**:
- Block storage (chunk-based)
- Block queries (get block, set block, check collisions)
- Chunk loading/unloading coordination

**Thread Safety**:
- Chunk-level locking (fine-grained)
- Read operations are lock-free when possible
- Write operations are synchronized

```csharp
public class BlockManager
{
    private readonly ConcurrentDictionary<(int X, int Z), Chunk> _chunks;
    
    public Block GetBlock(int x, int y, int z);
    public void SetBlock(int x, int y, int z, Block block);
    public bool IsChunkLoaded(int chunkX, int chunkZ);
}
```

### 5. Entity State (Shared, Thread-Safe)

**Location**: `MineSharp.World.EntityManager`

**Responsibilities**:
- Entity lifecycle (spawn, despawn, update)
- Entity position and state tracking
- Entity-to-entity interactions

**Thread Safety**:
- Entities stored in ConcurrentDictionary
- Update loop runs on dedicated thread
- Position updates are synchronized

```csharp
public class EntityManager
{
    private readonly ConcurrentDictionary<int, Entity> _entities;
    private int _nextEntityId;
    
    public int SpawnEntity(Entity entity);
    public void RemoveEntity(int entityId);
    public void UpdateEntities(TimeSpan deltaTime);
}
```

## State Flow

### Connection Lifecycle

```
1. Client connects
   → ClientConnection created
   → State = HANDSHAKING
   → HandshakingHandler processes packets

2. Handshake complete
   → State = LOGIN
   → LoginHandler processes packets

3. Login complete
   → State = CONFIGURATION
   → ConfigurationHandler processes packets
   → Player object created (not yet in world)

4. Configuration complete
   → State = PLAY
   → Player added to World.PlayerManager
   → PlayHandler processes packets
   → Chunks sent to client

5. Client disconnects
   → Player removed from World.PlayerManager
   → ClientConnection disposed
```

### State Access Patterns

**Connection State**:
- Owned by `ClientConnection`
- Modified only by packet handlers
- Read by packet routing logic

**Player State**:
- Created during Configuration phase
- Added to `World.PlayerManager` when entering PLAY
- Modified through `PlayerManager` methods (thread-safe)
- Read by game logic and packet handlers

**World State**:
- Single `World` instance shared across all connections
- All modifications go through `World` methods
- Game loop calls `World.Tick()` periodically

## Threading Model

### Main Threads

1. **Server Thread**: Accepts new connections
2. **Connection Threads**: One per client (async/await tasks)
3. **World Update Thread**: Runs game loop (20 TPS)
4. **Entity Update Thread**: Updates entity physics (20 TPS)

### Thread Safety Strategy

1. **Immutable Data**: Packet definitions, registry data (read-only after load)
2. **Lock-Free Reads**: Use ConcurrentDictionary, atomic operations where possible
3. **Fine-Grained Locks**: Chunk-level locks for block access
4. **Message Passing**: Use channels/queues for cross-thread communication
5. **Single Writer**: Each connection writes to its own socket (no contention)

## Key Design Decisions

### 1. Separation of Network and Game Logic

- Network layer (`MineSharp.Network`) handles protocol only
- Game logic (`MineSharp.Game`, `MineSharp.World`) is protocol-agnostic
- Clear boundaries allow for testing and future protocol changes

### 2. State Ownership

- **Connection owns**: Socket, connection state, packet buffers
- **World owns**: All game state (players, entities, blocks)
- **Player owns**: Player-specific data (position, inventory, loaded chunks)

### 3. Dependency Injection

- Use interfaces for testability
- `IWorld`, `IPlayerManager`, `IBlockManager`, etc.
- Allows mocking for unit tests

### 4. Async/Await Throughout

- All I/O operations are async
- Non-blocking packet processing
- Efficient resource utilization

## Example: Packet Flow

```
1. Client sends packet
   → ClientConnection.ReadPacketAsync()
   → PacketHandler.RoutePacket()
   → State-specific handler (e.g., PlayHandler.HandlePlayerPosition())
   
2. Handler processes packet
   → Updates Player state via PlayerManager
   → May trigger world updates (e.g., block breaking)
   → May send response packets
   
3. Response packet
   → PacketBuilder.BuildPacket()
   → ClientConnection.SendPacketAsync()
   → Written to socket
```

## Data Loading

### Static Data (Loaded Once at Startup)

- Registry data (from `extracted_data/registries.json`)
- Block definitions (from `extracted_data/blocks.json`)
- Item definitions (from `extracted_data/items.json`)
- Loot tables (from `extracted_data/loot_tables.json`)
- Biome data (from `extracted_data/biomes.json`)

**Location**: `MineSharp.Data.RegistryManager`

**Thread Safety**: Read-only after initialization, safe for concurrent reads

## Performance Considerations

1. **Chunk Loading**: Lazy loading, background thread for generation
2. **Entity Updates**: Batch updates, spatial partitioning for collision
3. **Packet Batching**: Group multiple packets when possible
4. **Memory Pooling**: Reuse buffers for packet reading/writing
5. **Lock Contention**: Minimize lock scope, use reader-writer locks where appropriate

## Testing Strategy

1. **Unit Tests**: Test each layer in isolation
   - Protocol encoding/decoding
   - State management logic
   - Game logic (block breaking, item drops, etc.)

2. **Integration Tests**: Test layer interactions
   - Packet flow through handlers
   - World state updates
   - Multi-player scenarios

3. **Protocol Tests**: Use packet logs from Python server
   - Replay captured packets
   - Verify responses match expected behavior

## Migration Path from Python

1. Start with protocol layer (Core/Protocol)
2. Implement network layer with basic handlers
3. Add world state management
4. Implement game logic incrementally
5. Use Python server as reference for packet formats

## Next Steps

1. Create solution structure with projects
2. Implement Core protocol types (VarInt, Position, etc.)
3. Implement basic TCP server and connection handling
4. Implement packet parsing for Handshaking state
5. Gradually add states and features

