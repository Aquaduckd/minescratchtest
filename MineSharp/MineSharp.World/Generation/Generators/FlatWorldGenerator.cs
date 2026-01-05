using MineSharp.World.Generation;

namespace MineSharp.World.Generation.Generators;

/// <summary>
/// Generates a flat world with a single layer of grass at y=64.
/// </summary>
public class FlatWorldGenerator : ITerrainGenerator
{
    public string GeneratorId => "flat";
    public string DisplayName => "Flat World";
    
    private const int GroundY = 64;
    private const int DirtBlockStateId = 2105; // Dirt block state ID (matches Python)
    private const int GrassBlockStateId = 2098; // Grass block state ID (matches Python test ID)
    
    public void GenerateChunk(Chunk chunk, int chunkX, int chunkZ)
    {
        // Flat world: matches Python implementation
        // - Sections below y=63: all dirt (block 2105)
        // - Section with y=63: dirt layer
        // - Section with y=64: grass layer (block 2098)
        // - Sections above y=64: all air
        
        // Fill all blocks below y=63 with dirt
        for (int y = -64; y < GroundY - 1; y++)
        {
            for (int z = 0; z < 16; z++)
            {
                for (int x = 0; x < 16; x++)
                {
                    chunk.SetBlock(x, y, z, new Block(DirtBlockStateId, "minecraft:dirt"));
                }
            }
        }
        
        // Dirt layer at y=63
        for (int z = 0; z < 16; z++)
        {
            for (int x = 0; x < 16; x++)
            {
                chunk.SetBlock(x, GroundY - 1, z, new Block(DirtBlockStateId, "minecraft:dirt"));
            }
        }
        
        // Grass layer at y=64
        for (int z = 0; z < 16; z++)
        {
            for (int x = 0; x < 16; x++)
            {
                chunk.SetBlock(x, GroundY, z, new Block(GrassBlockStateId, "minecraft:grass_block"));
            }
        }
        
        // Everything above y=64 is air (already initialized)
    }
    
    public int[] GenerateHeightmap(int chunkX, int chunkZ)
    {
        // For flat world: ground is at y=64
        // Heightmap value is the highest solid block Y + 1
        // So for flat world at y=64, heightmap should be 65
        var heightmap = new int[256]; // 16x16 = 256 columns
        
        for (int i = 0; i < 256; i++)
        {
            heightmap[i] = GroundY + 1; // Heightmap is highest solid block + 1
        }
        
        return heightmap;
    }
    
    public GeneratorConfigSchema? GetConfigSchema()
    {
        // Flat world generator has no configurable parameters
        return null;
    }
    
    public void Configure(GeneratorConfig? config)
    {
        // Flat world generator has no configuration
        // No-op
    }
}

