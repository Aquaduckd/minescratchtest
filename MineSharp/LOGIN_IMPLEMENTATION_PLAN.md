# Login Implementation Plan

This document outlines the step-by-step plan to implement successful player login from initial connection to entering the PLAY state.

## Overview

The login flow consists of 4 main states:
1. **HANDSHAKING** - Initial connection and protocol negotiation
2. **LOGIN** - Authentication
3. **CONFIGURATION** - Registry synchronization and client settings
4. **PLAY** - Game state entry

## Implementation Order

### Phase 1: Core Protocol Infrastructure

#### 1.1 VarInt/VarLong Encoding/Decoding
**Files**: `MineSharp.Core/Protocol/ProtocolReader.cs`, `MineSharp.Core/Protocol/ProtocolWriter.cs`

**Methods to implement**:
- `ProtocolReader.ReadVarInt()` - Read VarInt from bytes
- `ProtocolReader.ReadVarLong()` - Read VarLong from bytes
- `ProtocolWriter.WriteVarInt(int value)` - Write VarInt to buffer
- `ProtocolWriter.WriteVarLong(long value)` - Write VarLong to buffer

**Why first**: All packets use VarInt for length and packet IDs. This is foundational.

#### 1.2 Basic Data Type Reading/Writing
**Files**: `MineSharp.Core/Protocol/ProtocolReader.cs`, `MineSharp.Core/Protocol/ProtocolWriter.cs`

**Methods to implement**:
- `ReadString()` / `WriteString()` - UTF-8 strings with VarInt length prefix
- `ReadUuid()` / `WriteUuid()` - 16-byte UUID
- `ReadByte()` / `WriteByte()` - Single byte
- `ReadBytes(int count)` / `WriteBytes(byte[] value)` - Byte arrays
- `ReadBool()` / `WriteBool()` - Boolean (1 byte)
- `ReadInt()` / `WriteInt()` - 32-bit integer
- `ReadLong()` / `WriteLong()` - 64-bit integer
- `ReadFloat()` / `WriteFloat()` - 32-bit float
- `ReadDouble()` / `WriteDouble()` - 64-bit double
- `ReadShort()` / `WriteShort()` - 16-bit integer
- `ReadUnsignedShort()` / `WriteUnsignedShort()` - 16-bit unsigned integer

**Why**: Needed for parsing all packet fields.

#### 1.3 Packet Length Reading
**Files**: `MineSharp.Network/ClientConnection.cs`

**Methods to implement**:
- `ReadPacketAsync()` - Read packet length (VarInt), then read packet data

**Why**: Must read complete packets before parsing.

### Phase 2: TCP Server and Connection Handling

#### 2.1 TCP Server Setup
**Files**: `MineSharp.Network/TcpServer.cs`

**Methods to implement**:
- `StartAsync()` - Start listening on port 25565
- `AcceptConnectionsAsync()` - Accept incoming connections
- `Stop()` - Shutdown server gracefully

**Why**: Need to accept connections before handling them.

#### 2.2 Client Connection Handler
**Files**: `MineSharp.Network/ClientConnection.cs`

**Methods to implement**:
- `HandleConnectionAsync()` - Main connection loop
- `ReadPacketAsync()` - Read complete packet from stream
- `SendPacketAsync(byte[] packet)` - Send packet to client
- `SetState(ConnectionState newState)` - Update connection state
- `Disconnect()` - Clean up connection

**Why**: Core connection management needed for all states.

### Phase 3: Handshaking State

#### 3.1 Handshake Packet Parsing
**Files**: `MineSharp.Core/Protocol/PacketParser.cs`

**Methods to implement**:
- `ParsePacket()` - Parse handshake packet (packet ID 0x00)
  - Read VarInt: protocol version
  - Read String: server address
  - Read UnsignedShort: server port
  - Read VarInt: intent (1=Status, 2=Login, 3=Transfer)

**Why**: First packet received, determines next state.

#### 3.2 Handshake Handler
**Files**: `MineSharp.Network/Handlers/HandshakingHandler.cs`

**Methods to implement**:
- `HandleHandshakeAsync()` - Process handshake packet
  - Extract protocol version, address, port, intent
  - If intent == 2 (Login), transition to LOGIN state
  - If intent == 1 (Status), transition to STATUS state (not implemented yet)

**Why**: Routes connection to correct state.

### Phase 4: Login State

#### 4.1 Login Start Packet Parsing
**Files**: `MineSharp.Core/Protocol/PacketParser.cs`

**Methods to implement**:
- Parse Login Start packet (packet ID 0x00 in LOGIN state)
  - Read String: username
  - Read UUID: player UUID

**Why**: Client sends this to start login.

#### 4.2 Login Success Packet Building
**Files**: `MineSharp.Core/Protocol/PacketBuilder.cs`

**Methods to implement**:
- `BuildLoginSuccessPacket()` - Build Login Success packet (packet ID 0x02)
  - Write UUID
  - Write String: username
  - Write VarInt: 0 (property count, empty for offline mode)

**Why**: Server responds with this to complete authentication.

#### 4.3 Login Handler
**Files**: `MineSharp.Network/Handlers/LoginHandler.cs`

**Methods to implement**:
- `HandleLoginStartAsync()` - Process login start
  - Extract username and UUID
  - Call `SendLoginSuccessAsync()`
  - Transition to CONFIGURATION state
- `SendLoginSuccessAsync()` - Send login success packet

**Why**: Completes authentication and moves to configuration.

### Phase 5: Configuration State

#### 5.1 Registry Data Loading
**Files**: `MineSharp.Data/RegistryManager.cs`, `MineSharp.Data/DataLoader.cs`

**Methods to implement**:
- `DataLoader.LoadJson<T>()` - Load JSON file (use System.Text.Json)
- `RegistryManager.LoadRegistries()` - Load `extracted_data/registries.json`
- `RegistryManager.GetRegistry()` - Get specific registry by name

**Why**: Need registry data to send to client.

#### 5.2 Registry Data Packet Building
**Files**: `MineSharp.Core/Protocol/PacketBuilder.cs`

**Methods to implement**:
- `BuildRegistryDataPacket()` - Build Registry Data packet (packet ID 0x05)
  - Write String: registry name (e.g., "minecraft:block")
  - Write VarInt: entry count
  - For each entry:
    - Write String: entry name (e.g., "minecraft:stone")
    - Write VarInt: protocol ID
    - Write NBT: entry data (can be empty for now)

**Required registries** (from Python implementation):
1. `minecraft:block`
2. `minecraft:item`
3. `minecraft:fluid`
4. `minecraft:entity_type`
5. `minecraft:game_event`
6. `minecraft:painting_variant`
7. `minecraft:particle_type`
8. `minecraft:sound_event`
9. `minecraft:stat_type`
10. `minecraft:villager_type`
11. `minecraft:worldgen/biome`
12. `minecraft:damage_type`

**Why**: Client needs registry data to understand game objects.

#### 5.3 Client Information Packet Parsing
**Files**: `MineSharp.Core/Protocol/PacketParser.cs`

**Methods to implement**:
- Parse Client Information packet (packet ID 0x00 in CONFIGURATION state)
  - Read String: locale
  - Read Byte: view distance
  - Read VarInt: chat mode
  - Read Bool: chat colors
  - Read Byte: displayed skin parts
  - Read VarInt: main hand
  - Read Bool: enable text filtering
  - Read Bool: allow server listings

**Why**: Client sends settings that affect gameplay.

#### 5.4 Finish Configuration Packet Building
**Files**: `MineSharp.Core/Protocol/PacketBuilder.cs`

**Methods to implement**:
- `BuildFinishConfigurationPacket()` - Build Finish Configuration packet (packet ID 0x03)
  - Empty packet (no data)

**Why**: Signals end of configuration phase.

#### 5.5 Acknowledge Finish Configuration Packet Parsing
**Files**: `MineSharp.Core/Protocol/PacketParser.cs`

**Methods to implement**:
- Parse Acknowledge Finish Configuration packet (packet ID 0x03 in CONFIGURATION state)
  - Empty packet (no data)

**Why**: Client acknowledges configuration is complete.

#### 5.6 Configuration Handler
**Files**: `MineSharp.Network/Handlers/ConfigurationHandler.cs`

**Methods to implement**:
- `SendRegistryDataAsync()` - Send all required registries
  - Loop through required registries
  - Build and send Registry Data packet for each
- `HandleClientInformationAsync()` - Process client information
  - Store view distance and other settings
  - Send Finish Configuration
- `SendFinishConfigurationAsync()` - Send finish configuration packet
- Handle Acknowledge Finish Configuration
  - Transition to PLAY state

**Why**: Synchronizes client with server data and settings.

### Phase 6: Play State Entry

#### 6.1 Login (Play) Packet Building
**Files**: `MineSharp.Core/Protocol/PacketBuilder.cs`

**Methods to implement**:
- `BuildLoginPacket()` - Build Login (play) packet (packet ID 0x2C)
  - Write Int: entity ID (usually 1 for first player)
  - Write Bool: is hardcore
  - Write VarInt: game mode count (1)
  - Write VarInt: game mode (0=Survival, 1=Creative, 2=Adventure, 3=Spectator)
  - Write VarInt: dimension count (1)
  - Write String: dimension name (e.g., "minecraft:overworld")
  - Write Long: hashed seed
  - Write Byte: max players
  - Write VarInt: view distance
  - Write Bool: reduced debug info
  - Write Bool: enable respawn screen
  - Write Bool: is debug
  - Write Bool: is flat
  - Write Bool: has death location (false for new player)
  - If has death location:
    - Write String: death dimension name
    - Write Position: death location

**Why**: First packet in PLAY state, sets up player in world.

#### 6.2 Synchronize Player Position Packet Building
**Files**: `MineSharp.Core/Protocol/PacketBuilder.cs`

**Methods to implement**:
- `BuildSynchronizePlayerPositionPacket()` - Build Synchronize Player Position packet (packet ID 0x3C)
  - Write Double: x
  - Write Double: y
  - Write Double: z
  - Write Float: yaw
  - Write Float: pitch
  - Write Byte: flags (0 = no relative positioning)
  - Write VarInt: teleport ID

**Why**: Sets initial player position.

#### 6.3 Update Time Packet Building
**Files**: `MineSharp.Core/Protocol/PacketBuilder.cs`

**Methods to implement**:
- `BuildUpdateTimePacket()` - Build Update Time packet (packet ID 0x5C)
  - Write Long: world age
  - Write Long: time of day

**Why**: Sets world time.

#### 6.4 Game Event Packet Building
**Files**: `MineSharp.Core/Protocol/PacketBuilder.cs`

**Methods to implement**:
- `BuildGameEventPacket()` - Build Game Event packet (packet ID 0x1C)
  - Write Byte: event ID (13 = "Start waiting for level chunks")
  - Write Float: value (0.0 for event 13)

**Why**: Signals client to start loading chunks.

#### 6.5 Chunk Data Packet Building
**Files**: `MineSharp.Core/Protocol/PacketBuilder.cs`

**Methods to implement**:
- `BuildChunkDataPacket()` - Build Chunk Data and Update Light packet (packet ID 0x2C)
  - Write Int: chunk X
  - Write Int: chunk Z
  - Write NBT: heightmaps (can be empty for now)
  - Write VarInt: data size
  - Write ByteArray: chunk data (compressed)
  - Write VarInt: block entity count (0 for now)
  - Write Bool: trust edges
  - Write VarInt: sky light mask count (0 for now)
  - Write VarInt: block light mask count (0 for now)
  - Write VarInt: empty sky light mask count (0 for now)
  - Write VarInt: empty block light mask count (0 for now)
  - Write VarInt: sky light array count (0 for now)
  - Write VarInt: block light array count (0 for now)

**Why**: Sends world chunks to client for rendering.

#### 6.6 Play Handler - Initial Setup
**Files**: `MineSharp.Network/Handlers/PlayHandler.cs`

**Methods to implement**:
- `SendLoginPacketAsync()` - Send login (play) packet
- `SendSynchronizePlayerPositionAsync()` - Send initial position
- `SendUpdateTimeAsync()` - Send world time
- `SendGameEventAsync()` - Send "start waiting for chunks" event
- `SendChunkDataAsync()` - Send initial chunks (3x3 grid around spawn)

**Why**: Gets player into the world and renders initial area.

### Phase 7: Player and World Setup

#### 7.1 Player Creation
**Files**: `MineSharp.Game/Player.cs`, `MineSharp.World/PlayerManager.cs`

**Methods to implement**:
- `PlayerManager.AddPlayer()` - Add player to world
- `Player` constructor - Initialize player with UUID and view distance
- `Player.UpdatePosition()` - Update position and detect chunk changes

**Why**: Need player object to track state.

#### 7.2 World Initialization
**Files**: `MineSharp.World/World.cs`, `MineSharp.World/BlockManager.cs`

**Methods to implement**:
- `BlockManager.GetOrCreateChunk()` - Get or create chunk
- `BlockManager.GetBlock()` - Get block at position (return air for now)
- Basic chunk structure for sending empty chunks

**Why**: Need world structure to send chunks.

### Phase 8: Packet Routing

#### 8.1 Packet Handler Router
**Files**: `MineSharp.Network/PacketHandler.cs`

**Methods to implement**:
- `HandlePacketAsync()` - Route packets to appropriate handler based on state
  - HANDSHAKING → HandshakingHandler
  - LOGIN → LoginHandler
  - CONFIGURATION → ConfigurationHandler
  - PLAY → PlayHandler

**Why**: Central routing for all packets.

## Testing Strategy

### Unit Tests
1. Test VarInt encoding/decoding with various values
2. Test packet parsing for each packet type
3. Test packet building for each packet type

### Integration Tests
1. Test complete handshake flow
2. Test complete login flow
3. Test complete configuration flow
4. Test play state entry

### Manual Testing
1. Connect with Minecraft client
2. Verify each state transition
3. Verify packets are sent/received correctly
4. Verify client enters world successfully

## Dependencies Between Phases

```
Phase 1 (Core Protocol) → Phase 2 (TCP Server)
Phase 2 → Phase 3 (Handshaking)
Phase 3 → Phase 4 (Login)
Phase 4 → Phase 5 (Configuration)
Phase 5 → Phase 6 (Play Entry)
Phase 7 (Player/World) → Phase 6 (needed for chunk sending)
Phase 8 (Routing) → All phases (ties everything together)
```

## Critical Path

The fastest path to a working login:

1. **VarInt reading/writing** (Phase 1.1)
2. **Basic data types** (Phase 1.2)
3. **TCP server and connection** (Phase 2)
4. **Handshake parsing and handling** (Phase 3)
5. **Login Start parsing and Login Success building** (Phase 4)
6. **Registry data loading and sending** (Phase 5.1, 5.2, 5.6)
7. **Client Information handling** (Phase 5.3, 5.6)
8. **Finish Configuration** (Phase 5.4, 5.5, 5.6)
9. **Login (play) packet** (Phase 6.1, 6.6)
10. **Basic chunk data** (Phase 6.5, 6.6)
11. **Packet routing** (Phase 8)

## Notes

- Start with minimal implementations (empty chunks, basic registries)
- Use the Python server's packet logs as reference
- Test each phase before moving to the next
- Registry data format is complex - start simple and expand
- Chunk data is large - start with empty/air chunks first
- Keep alive packets can be added later (not critical for initial login)

## Success Criteria

A successful login is achieved when:
1. ✅ Client connects to server
2. ✅ Handshake completes
3. ✅ Login Start → Login Success
4. ✅ All registries sent and acknowledged
5. ✅ Client Information received
6. ✅ Configuration phase completes
7. ✅ Login (play) packet sent
8. ✅ Player position synchronized
9. ✅ Chunks sent and client renders world
10. ✅ Client is in PLAY state and can move around

