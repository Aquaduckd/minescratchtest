# MineSharp - Minecraft Server in C#

A high-performance Minecraft server implementation in C#.

## Prerequisites

- .NET 6.0 SDK or later
- Extracted Minecraft server data in `../extracted_data/` directory
  - Run `python extract_server_data.py` from the project root to generate this data

## Building

```bash
cd MineSharp
dotnet build
```

## Running the Server

### Option 1: Run from project root (recommended)

```bash
cd /home/kevin/coding/minescratchtest
dotnet run --project MineSharp/MineSharp.Server
```

### Option 2: Run from MineSharp directory

```bash
cd MineSharp
dotnet run --project MineSharp.Server
```

### Option 3: Run the compiled executable

```bash
cd MineSharp/MineSharp.Server/bin/Debug/net6.0
./MineSharp.Server
```

## Configuration

Default settings (can be modified in `ServerConfiguration.cs`):
- **Port**: 25565
- **View Distance**: 10 chunks
- **Max Players**: 20
- **Data Path**: Auto-detected (looks for `extracted_data/` in project root)

## Connecting

1. Start the server using one of the methods above
2. Connect with a Minecraft 1.21.10 client to `localhost:25565`
3. The server will handle:
   - Handshake
   - Login (offline mode)
   - Configuration (registry data)
   - Play state entry

## Current Status

✅ **Implemented:**
- Core protocol (VarInt, VarLong, all data types)
- Handshake packet handling
- Login flow (Login Start → Login Success)
- Configuration flow (Known Packs, Registry Data, Finish Configuration)
- Play state entry (Login Play, Position, Time, Game Event packets)
- Registry data loading from JSON files

🚧 **In Progress:**
- Chunk data packets
- Player movement handling
- World state management

## Testing

Run all tests:
```bash
cd MineSharp
dotnet test
```

## Project Structure

```
MineSharp/
├── MineSharp.Core/          # Core protocol types and utilities
├── MineSharp.Network/       # Network layer (TCP server, packet handling)
├── MineSharp.Game/          # Game logic (Player, Entity, etc.)
├── MineSharp.World/         # World management (Chunks, Blocks)
├── MineSharp.Data/          # Data loading (Registries, Loot Tables)
├── MineSharp.Server/        # Server entry point
└── MineSharp.Tests/         # Unit and integration tests
```

