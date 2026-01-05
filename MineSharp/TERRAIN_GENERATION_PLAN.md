# Terrain Generation System Architecture Plan

## Overview
Design a flexible, extensible terrain generation system that supports multiple generation modes (flat, noise, etc.) and allows easy addition of new generators.

## Design Principles

1. **Strategy Pattern**: Use interface-based design for generators
2. **Dependency Injection**: Generators injected into BlockManager
3. **Configuration-Driven**: Generator selection via configuration
4. **Extensibility**: Easy to add new generators without modifying existing code
5. **Testability**: Each generator can be tested independently
6. **Performance**: Support for async/background generation if needed

## Architecture

### 1. Core Interface

**File**: `MineSharp.World/Generation/ITerrainGenerator.cs`

```csharp
namespace MineSharp.World.Generation;

/// <summary>
/// Interface for terrain generators.
/// All terrain generators must implement this interface.
/// </summary>
public interface ITerrainGenerator
{
    /// <summary>
    /// Unique identifier for this generator (e.g., "flat", "noise", "void").
    /// </summary>
    string GeneratorId { get; }
    
    /// <summary>
    /// Human-readable name for this generator.
    /// </summary>
    string DisplayName { get; }
    
    /// <summary>
    /// Generates terrain for a chunk.
    /// </summary>
    /// <param name="chunk">The chunk to populate with blocks</param>
    /// <param name="chunkX">Chunk X coordinate</param>
    /// <param name="chunkZ">Chunk Z coordinate</param>
    void GenerateChunk(Chunk chunk, int chunkX, int chunkZ);
    
    /// <summary>
    /// Generates heightmap for a chunk (256 entries, one per column).
    /// Used for MOTION_BLOCKING heightmap in chunk data packets.
    /// </summary>
    /// <param name="chunkX">Chunk X coordinate</param>
    /// <param name="chunkZ">Chunk Z coordinate</param>
    /// <returns>Array of 256 height values (one per column)</returns>
    int[] GenerateHeightmap(int chunkX, int chunkZ);
    
    /// <summary>
    /// Optional: Get configuration schema for this generator.
    /// Returns null if generator has no configurable parameters.
    /// </summary>
    GeneratorConfigSchema? GetConfigSchema();
    
    /// <summary>
    /// Optional: Initialize generator with configuration.
    /// </summary>
    void Configure(GeneratorConfig? config);
}
```

### 2. Generator Registry

**File**: `MineSharp.World/Generation/TerrainGeneratorRegistry.cs`

```csharp
namespace MineSharp.World.Generation;

/// <summary>
/// Registry for managing terrain generators.
/// Allows registration and retrieval of generators by ID.
/// </summary>
public class TerrainGeneratorRegistry
{
    private readonly Dictionary<string, Func<ITerrainGenerator>> _generatorFactories;
    private readonly Dictionary<string, ITerrainGenerator> _generatorInstances;
    
    public TerrainGeneratorRegistry()
    {
        _generatorFactories = new Dictionary<string, Func<ITerrainGenerator>>();
        _generatorInstances = new Dictionary<string, ITerrainGenerator>();
        RegisterDefaultGenerators();
    }
    
    /// <summary>
    /// Register a generator factory function.
    /// </summary>
    public void Register(string generatorId, Func<ITerrainGenerator> factory)
    {
        _generatorFactories[generatorId] = factory;
    }
    
    /// <summary>
    /// Get a generator instance by ID (creates if needed, caches instances).
    /// </summary>
    public ITerrainGenerator GetGenerator(string generatorId)
    {
        if (!_generatorInstances.TryGetValue(generatorId, out var generator))
        {
            if (!_generatorFactories.TryGetValue(generatorId, out var factory))
            {
                throw new ArgumentException($"Unknown generator ID: {generatorId}");
            }
            generator = factory();
            _generatorInstances[generatorId] = generator;
        }
        return generator;
    }
    
    /// <summary>
    /// Get list of all registered generator IDs.
    /// </summary>
    public IEnumerable<string> GetRegisteredGeneratorIds()
    {
        return _generatorFactories.Keys;
    }
    
    /// <summary>
    /// Check if a generator ID is registered.
    /// </summary>
    public bool IsRegistered(string generatorId)
    {
        return _generatorFactories.ContainsKey(generatorId);
    }
    
    private void RegisterDefaultGenerators()
    {
        Register("flat", () => new FlatWorldGenerator());
        Register("noise", () => new NoiseTerrainGenerator());
        Register("void", () => new VoidWorldGenerator());
    }
}
```

### 3. Generator Implementations

#### 3.1 Flat World Generator

**File**: `MineSharp.World/Generation/FlatWorldGenerator.cs`

```csharp
namespace MineSharp.World.Generation;

/// <summary>
/// Generates a flat world with a single layer of grass at y=64.
/// </summary>
public class FlatWorldGenerator : ITerrainGenerator
{
    public string GeneratorId => "flat";
    public string DisplayName => "Flat World";
    
    private int _groundY = 64;
    private int _dirtBlockStateId = 2105; // Dirt
    private int _grassBlockStateId = 2098; // Grass (test ID) or 9 (actual)
    
    public void GenerateChunk(Chunk chunk, int chunkX, int chunkZ)
    {
        // Fill below ground with dirt
        for (int y = -64; y < _groundY - 1; y++)
        {
            for (int z = 0; z < 16; z++)
            {
                for (int x = 0; x < 16; x++)
                {
                    chunk.SetBlock(x, y, z, new Block(_dirtBlockStateId, "minecraft:dirt"));
                }
            }
        }
        
        // Dirt layer at y=63
        for (int z = 0; z < 16; z++)
        {
            for (int x = 0; x < 16; x++)
            {
                chunk.SetBlock(x, _groundY - 1, z, new Block(_dirtBlockStateId, "minecraft:dirt"));
            }
        }
        
        // Grass layer at y=64
        for (int z = 0; z < 16; z++)
        {
            for (int x = 0; x < 16; x++)
            {
                chunk.SetBlock(x, _groundY, z, new Block(_grassBlockStateId, "minecraft:grass_block"));
            }
        }
        // Everything above is air (already initialized)
    }
    
    public int[] GenerateHeightmap(int chunkX, int chunkZ)
    {
        var heightmap = new int[256];
        for (int i = 0; i < 256; i++)
        {
            heightmap[i] = _groundY + 1; // Heightmap is highest solid block + 1
        }
        return heightmap;
    }
    
    public GeneratorConfigSchema? GetConfigSchema() => null; // No configuration needed
    
    public void Configure(GeneratorConfig? config) { } // No configuration needed
}
```

#### 3.2 Noise Terrain Generator

**File**: `MineSharp.World/Generation/NoiseTerrainGenerator.cs`

```csharp
namespace MineSharp.World.Generation;

/// <summary>
/// Generates terrain using Perlin noise for natural-looking landscapes.
/// </summary>
public class NoiseTerrainGenerator : ITerrainGenerator
{
    public string GeneratorId => "noise";
    public string DisplayName => "Noise Terrain";
    
    private long _seed = 0;
    private double _amplitude = 32.0;
    private int _baseHeight = 64;
    private int _dirtBlockStateId = 2105;
    private int _grassBlockStateId = 2098;
    private int _stoneBlockStateId = 1; // TODO: Get actual stone ID
    
    // Perlin noise implementation would go here
    // For now, placeholder
    
    public void GenerateChunk(Chunk chunk, int chunkX, int chunkZ)
    {
        // TODO: Implement Perlin noise-based generation
        // For now, fallback to flat
        var flatGen = new FlatWorldGenerator();
        flatGen.GenerateChunk(chunk, chunkX, chunkZ);
    }
    
    public int[] GenerateHeightmap(int chunkX, int chunkZ)
    {
        // TODO: Generate heightmap from noise
        var heightmap = new int[256];
        for (int z = 0; z < 16; z++)
        {
            for (int x = 0; x < 16; x++)
            {
                int worldX = chunkX * 16 + x;
                int worldZ = chunkZ * 16 + z;
                // TODO: Calculate height from noise
                int height = _baseHeight;
                heightmap[z * 16 + x] = height + 1;
            }
        }
        return heightmap;
    }
    
    public GeneratorConfigSchema? GetConfigSchema()
    {
        return new GeneratorConfigSchema
        {
            Properties = new Dictionary<string, ConfigProperty>
            {
                ["seed"] = new ConfigProperty { Type = "long", Description = "Random seed" },
                ["amplitude"] = new ConfigProperty { Type = "double", Description = "Height variation" },
                ["baseHeight"] = new ConfigProperty { Type = "int", Description = "Base terrain height" }
            }
        };
    }
    
    public void Configure(GeneratorConfig? config)
    {
        if (config == null) return;
        
        if (config.TryGetValue("seed", out var seed))
            _seed = Convert.ToInt64(seed);
        if (config.TryGetValue("amplitude", out var amp))
            _amplitude = Convert.ToDouble(amp);
        if (config.TryGetValue("baseHeight", out var baseH))
            _baseHeight = Convert.ToInt32(baseH);
    }
}
```

#### 3.3 Void World Generator

**File**: `MineSharp.World/Generation/VoidWorldGenerator.cs`

```csharp
namespace MineSharp.World.Generation;

/// <summary>
/// Generates an empty void world (all air).
/// Useful for testing or creative building servers.
/// </summary>
public class VoidWorldGenerator : ITerrainGenerator
{
    public string GeneratorId => "void";
    public string DisplayName => "Void World";
    
    public void GenerateChunk(Chunk chunk, int chunkX, int chunkZ)
    {
        // Do nothing - chunk is already initialized with air
    }
    
    public int[] GenerateHeightmap(int chunkX, int chunkZ)
    {
        var heightmap = new int[256];
        // Heightmap for void: all 0 (or -64, depending on protocol)
        for (int i = 0; i < 256; i++)
        {
            heightmap[i] = -64; // Minimum world height
        }
        return heightmap;
    }
    
    public GeneratorConfigSchema? GetConfigSchema() => null;
    public void Configure(GeneratorConfig? config) { }
}
```

### 4. Configuration Support

**File**: `MineSharp.World/Generation/GeneratorConfig.cs`

```csharp
namespace MineSharp.World.Generation;

/// <summary>
/// Configuration dictionary for generator-specific settings.
/// </summary>
public class GeneratorConfig : Dictionary<string, object>
{
}

/// <summary>
/// Schema definition for generator configuration.
/// </summary>
public class GeneratorConfigSchema
{
    public Dictionary<string, ConfigProperty> Properties { get; set; } = new();
}

public class ConfigProperty
{
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public object? DefaultValue { get; set; }
}
```

### 5. BlockManager Integration

**File**: `MineSharp.World/BlockManager.cs` (modifications)

```csharp
public class BlockManager
{
    private readonly ConcurrentDictionary<(int X, int Z), Chunk> _chunks;
    private readonly ITerrainGenerator _generator;
    
    public BlockManager(ITerrainGenerator generator)
    {
        _chunks = new ConcurrentDictionary<(int X, int Z), Chunk>();
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
    }
    
    // Convenience constructor for default flat generator
    public BlockManager() : this(new FlatWorldGenerator())
    {
    }
    
    public Chunk GetOrCreateChunk(int chunkX, int chunkZ)
    {
        return _chunks.GetOrAdd((chunkX, chunkZ), _ =>
        {
            var chunk = new Chunk(chunkX, chunkZ);
            _generator.GenerateChunk(chunk, chunkX, chunkZ);
            return chunk;
        });
    }
    
    public int[] GenerateHeightmap(int chunkX, int chunkZ)
    {
        return _generator.GenerateHeightmap(chunkX, chunkZ);
    }
}
```

### 6. Server Configuration

**File**: `MineSharp.Server/ServerConfiguration.cs` (modifications)

```csharp
public class ServerConfiguration
{
    // ... existing properties ...
    
    /// <summary>
    /// Terrain generator ID (e.g., "flat", "noise", "void").
    /// </summary>
    public string TerrainGeneratorId { get; set; } = "flat";
    
    /// <summary>
    /// Generator-specific configuration.
    /// </summary>
    public GeneratorConfig? TerrainGeneratorConfig { get; set; }
}
```

**File**: `MineSharp.Server/Server.cs` (modifications)

```csharp
public class Server
{
    private void LoadData()
    {
        // ... existing code ...
        
        // Initialize terrain generator
        var registry = new TerrainGeneratorRegistry();
        var generator = registry.GetGenerator(_configuration.TerrainGeneratorId);
        
        if (_configuration.TerrainGeneratorConfig != null)
        {
            generator.Configure(_configuration.TerrainGeneratorConfig);
        }
        
        // Pass generator to World
        _world = new MineSharp.World.World(
            _configuration.ViewDistance,
            generator
        );
    }
}
```

### 7. World Integration

**File**: `MineSharp.World/World.cs` (modifications)

```csharp
public class World
{
    public World(int viewDistance = 10, ITerrainGenerator? generator = null)
    {
        // ... existing code ...
        
        generator ??= new FlatWorldGenerator(); // Default to flat
        _blockManager = new BlockManager(generator);
    }
}
```

## Implementation Phases

### Phase 1: Core Interface & Registry
1. Create `ITerrainGenerator` interface
2. Create `TerrainGeneratorRegistry` class
3. Create `GeneratorConfig` classes
4. Update `BlockManager` to use `ITerrainGenerator`
5. Add tests for registry

### Phase 2: Flat Generator
1. Extract existing `GenerateFlatWorldChunk` into `FlatWorldGenerator`
2. Update `BlockManager` to use generator
3. Update tests to use generator
4. Verify existing functionality still works

### Phase 3: Void Generator
1. Implement `VoidWorldGenerator`
2. Add tests
3. Verify void world works

### Phase 4: Noise Generator (Future)
1. Implement Perlin noise library or use existing
2. Implement `NoiseTerrainGenerator`
3. Add configuration support
4. Add tests

### Phase 5: Configuration Integration
1. Update `ServerConfiguration` to support generator selection
2. Update `Server` to initialize generator from config
3. Add JSON configuration support

### Phase 6: Additional Generators (Future)
- Superflat (configurable layers)
- Amplified (extreme terrain)
- Large Biomes
- Custom generators

## Testing Strategy

### Unit Tests
- Each generator tested independently
- Test `GenerateChunk` produces expected blocks
- Test `GenerateHeightmap` produces correct values
- Test configuration loading

### Integration Tests
- Test generator selection via registry
- Test BlockManager with different generators
- Test server startup with different generators

### Performance Tests
- Measure chunk generation time per generator
- Compare flat vs noise performance

## Benefits

1. **Extensibility**: Add new generators by implementing `ITerrainGenerator`
2. **Testability**: Each generator can be tested in isolation
3. **Configuration**: Easy to switch generators via config
4. **Maintainability**: Clear separation of concerns
5. **Performance**: Can optimize individual generators independently
6. **Flexibility**: Support for generator-specific configuration

## Migration Path

1. Keep existing `GenerateFlatWorldChunk` method temporarily
2. Implement new system alongside existing code
3. Migrate tests to use new system
4. Remove old code once verified

## File Structure

```
MineSharp.World/
├── Generation/
│   ├── ITerrainGenerator.cs
│   ├── TerrainGeneratorRegistry.cs
│   ├── GeneratorConfig.cs
│   ├── FlatWorldGenerator.cs
│   ├── NoiseTerrainGenerator.cs
│   └── VoidWorldGenerator.cs
├── BlockManager.cs (modified)
├── World.cs (modified)
└── ...

MineSharp.Server/
├── ServerConfiguration.cs (modified)
└── Server.cs (modified)
```

