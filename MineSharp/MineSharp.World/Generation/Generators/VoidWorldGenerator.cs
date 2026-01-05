using MineSharp.World.Generation;

namespace MineSharp.World.Generation.Generators;

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
        // TODO: Implementation
    }
    
    public int[] GenerateHeightmap(int chunkX, int chunkZ)
    {
        // TODO: Implementation
        throw new NotImplementedException();
    }
    
    public GeneratorConfigSchema? GetConfigSchema()
    {
        // TODO: Implementation
        return null;
    }
    
    public void Configure(GeneratorConfig? config)
    {
        // TODO: Implementation
    }
}

