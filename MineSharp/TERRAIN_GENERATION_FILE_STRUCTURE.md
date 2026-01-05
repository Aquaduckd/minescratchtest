# Terrain Generation System - File Structure

## Complete Directory Tree

```
MineSharp/
├── MineSharp.Core/
│   └── ... (existing files)
│
├── MineSharp.World/
│   ├── Generation/                          # NEW: Terrain generation namespace
│   │   ├── ITerrainGenerator.cs             # Core interface for all generators
│   │   ├── TerrainGeneratorRegistry.cs     # Registry for managing generators
│   │   ├── GeneratorConfig.cs              # Configuration classes
│   │   │
│   │   ├── Generators/                     # Generator implementations
│   │   │   ├── FlatWorldGenerator.cs       # Flat world generator
│   │   │   ├── VoidWorldGenerator.cs       # Void/empty world generator
│   │   │   ├── NoiseTerrainGenerator.cs    # Perlin noise terrain (future)
│   │   │   └── SuperflatGenerator.cs        # Configurable superflat (future)
│   │   │
│   │   └── Utilities/                       # Helper utilities (optional)
│   │       ├── PerlinNoise.cs              # Perlin noise implementation (future)
│   │       └── HeightmapHelper.cs          # Heightmap utilities (if needed)
│   │
│   ├── BlockManager.cs                      # MODIFIED: Uses ITerrainGenerator
│   ├── Chunk.cs                             # (existing, unchanged)
│   ├── ChunkManager.cs                      # (existing, unchanged)
│   ├── EntityManager.cs                     # (existing, unchanged)
│   ├── PlayerManager.cs                     # (existing, unchanged)
│   ├── World.cs                             # MODIFIED: Accepts ITerrainGenerator
│   └── MineSharp.World.csproj               # (existing, unchanged)
│
├── MineSharp.Server/
│   ├── Server.cs                            # MODIFIED: Initializes generator from config
│   ├── ServerConfiguration.cs              # MODIFIED: Adds generator config
│   └── MineSharp.Server.csproj              # (existing, unchanged)
│
└── MineSharp.Tests/
    └── World/
        ├── Generation/                       # NEW: Generator tests
        │   ├── TerrainGeneratorRegistryTests.cs
        │   ├── FlatWorldGeneratorTests.cs
        │   ├── VoidWorldGeneratorTests.cs
        │   └── NoiseTerrainGeneratorTests.cs (future)
        │
        └── ChunkGenerationTests.cs           # MODIFIED: Use generators
```

## Detailed File Descriptions

### Core Interface & Registry

#### `MineSharp.World/Generation/ITerrainGenerator.cs`
- **Purpose**: Core interface defining the contract for all terrain generators
- **Key Members**:
  - `string GeneratorId { get; }`
  - `string DisplayName { get; }`
  - `void GenerateChunk(Chunk chunk, int chunkX, int chunkZ)`
  - `int[] GenerateHeightmap(int chunkX, int chunkZ)`
  - `GeneratorConfigSchema? GetConfigSchema()`
  - `void Configure(GeneratorConfig? config)`

#### `MineSharp.World/Generation/TerrainGeneratorRegistry.cs`
- **Purpose**: Central registry for managing and retrieving terrain generators
- **Key Members**:
  - `void Register(string generatorId, Func<ITerrainGenerator> factory)`
  - `ITerrainGenerator GetGenerator(string generatorId)`
  - `IEnumerable<string> GetRegisteredGeneratorIds()`
  - `bool IsRegistered(string generatorId)`
- **Default Generators**: Auto-registers "flat", "noise", "void"

#### `MineSharp.World/Generation/GeneratorConfig.cs`
- **Purpose**: Configuration classes for generator-specific settings
- **Classes**:
  - `GeneratorConfig : Dictionary<string, object>`
  - `GeneratorConfigSchema`
  - `ConfigProperty`

### Generator Implementations

#### `MineSharp.World/Generation/Generators/FlatWorldGenerator.cs`
- **Purpose**: Generates flat world with grass at y=64
- **GeneratorId**: `"flat"`
- **Configuration**: None (no configurable parameters)
- **Features**:
  - Dirt below y=63
  - Grass at y=64
  - Air above

#### `MineSharp.World/Generation/Generators/VoidWorldGenerator.cs`
- **Purpose**: Generates empty void world (all air)
- **GeneratorId**: `"void"`
- **Configuration**: None
- **Use Cases**: Testing, creative building servers

#### `MineSharp.World/Generation/Generators/NoiseTerrainGenerator.cs`
- **Purpose**: Perlin noise-based natural terrain generation
- **GeneratorId**: `"noise"`
- **Configuration**: 
  - `seed` (long)
  - `amplitude` (double)
  - `baseHeight` (int)
  - `scale` (double)
  - `octaves` (int)
- **Status**: Future implementation

#### `MineSharp.World/Generation/Generators/SuperflatGenerator.cs`
- **Purpose**: Configurable superflat with custom layers
- **GeneratorId**: `"superflat"`
- **Configuration**: Layer definitions
- **Status**: Future implementation

### Modified Files

#### `MineSharp.World/BlockManager.cs`
**Changes**:
- Remove `_useTerrainGeneration` boolean
- Remove `GenerateFlatWorldChunk()` method
- Add `ITerrainGenerator _generator` field
- Constructor: `BlockManager(ITerrainGenerator generator)`
- Convenience constructor: `BlockManager()` → uses `FlatWorldGenerator`
- `GetOrCreateChunk()` calls `_generator.GenerateChunk()`
- `GenerateHeightmap()` calls `_generator.GenerateHeightmap()`

#### `MineSharp.World/World.cs`
**Changes**:
- Constructor: `World(int viewDistance = 10, ITerrainGenerator? generator = null)`
- Default to `FlatWorldGenerator` if generator is null
- Pass generator to `BlockManager` constructor

#### `MineSharp.Server/ServerConfiguration.cs`
**Changes**:
- Remove `bool UseTerrainGeneration`
- Add `string TerrainGeneratorId { get; set; } = "flat"`
- Add `GeneratorConfig? TerrainGeneratorConfig { get; set; }`

#### `MineSharp.Server/Server.cs`
**Changes**:
- In `LoadData()`:
  - Create `TerrainGeneratorRegistry`
  - Get generator by ID from config
  - Configure generator if config provided
  - Pass generator to `World` constructor

### Test Files

#### `MineSharp.Tests/World/Generation/TerrainGeneratorRegistryTests.cs`
- Test generator registration
- Test generator retrieval
- Test default generators
- Test error handling for unknown IDs

#### `MineSharp.Tests/World/Generation/FlatWorldGeneratorTests.cs`
- Test chunk generation (blocks at correct positions)
- Test heightmap generation
- Test configuration (should be null)

#### `MineSharp.Tests/World/Generation/VoidWorldGeneratorTests.cs`
- Test chunk generation (all air)
- Test heightmap generation (all minimum height)

#### `MineSharp.Tests/World/Generation/NoiseTerrainGeneratorTests.cs` (Future)
- Test noise generation
- Test configuration loading
- Test deterministic generation (same seed = same terrain)

## Namespace Organization

```csharp
// Core interfaces and registry
namespace MineSharp.World.Generation;

// Generator implementations
namespace MineSharp.World.Generation.Generators;

// Utilities (if needed)
namespace MineSharp.World.Generation.Utilities;

// Tests
namespace MineSharp.Tests.World.Generation;
```

## File Size Estimates

- `ITerrainGenerator.cs`: ~50 lines
- `TerrainGeneratorRegistry.cs`: ~100 lines
- `GeneratorConfig.cs`: ~50 lines
- `FlatWorldGenerator.cs`: ~80 lines
- `VoidWorldGenerator.cs`: ~40 lines
- `NoiseTerrainGenerator.cs`: ~300+ lines (when implemented)

**Total New Code**: ~620 lines (excluding tests and future generators)

## Migration Impact

### Files to Modify
1. `BlockManager.cs` - Remove flat generation, add generator injection
2. `World.cs` - Accept generator parameter
3. `ServerConfiguration.cs` - Replace boolean with generator ID
4. `Server.cs` - Initialize generator from config
5. `ChunkGenerationTests.cs` - Update to use generators

### Files to Create
1. `Generation/` directory structure
2. Interface and registry files
3. Generator implementation files
4. Test files

### Breaking Changes
- `BlockManager(bool useTerrainGeneration)` → `BlockManager(ITerrainGenerator generator)`
- `World(int viewDistance, bool useTerrainGeneration)` → `World(int viewDistance, ITerrainGenerator? generator)`
- `ServerConfiguration.UseTerrainGeneration` → `ServerConfiguration.TerrainGeneratorId`

## Example Usage After Implementation

```csharp
// In Server.cs
var registry = new TerrainGeneratorRegistry();
var generator = registry.GetGenerator("flat"); // or "noise", "void"
var world = new World(viewDistance: 10, generator: generator);

// Or with configuration
var config = new GeneratorConfig
{
    ["seed"] = 12345L,
    ["amplitude"] = 32.0,
    ["baseHeight"] = 64
};
var noiseGen = registry.GetGenerator("noise");
noiseGen.Configure(config);
var world = new World(viewDistance: 10, generator: noiseGen);
```

