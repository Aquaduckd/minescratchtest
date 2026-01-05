using MineSharp.World.Generation.Generators;

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
        if (string.IsNullOrEmpty(generatorId))
        {
            throw new ArgumentException("Generator ID cannot be null or empty", nameof(generatorId));
        }
        
        if (factory == null)
        {
            throw new ArgumentNullException(nameof(factory));
        }
        
        _generatorFactories[generatorId] = factory;
        // Clear cached instance if it exists (force recreation on next GetGenerator call)
        _generatorInstances.Remove(generatorId);
    }
    
    /// <summary>
    /// Get a generator instance by ID (creates if needed, caches instances).
    /// </summary>
    public ITerrainGenerator GetGenerator(string generatorId)
    {
        if (string.IsNullOrEmpty(generatorId))
        {
            throw new ArgumentException("Generator ID cannot be null or empty", nameof(generatorId));
        }
        
        // Return cached instance if available
        if (_generatorInstances.TryGetValue(generatorId, out var cachedInstance))
        {
            return cachedInstance;
        }
        
        // Create new instance from factory
        if (!_generatorFactories.TryGetValue(generatorId, out var factory))
        {
            throw new ArgumentException($"Unknown generator ID: {generatorId}. Registered generators: {string.Join(", ", _generatorFactories.Keys)}", nameof(generatorId));
        }
        
        var generator = factory();
        _generatorInstances[generatorId] = generator;
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
        Register("void", () => new VoidWorldGenerator());
        Register("noise", () => new NoiseTerrainGenerator());
    }
}

