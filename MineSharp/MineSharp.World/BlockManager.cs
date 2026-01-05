using MineSharp.Core.DataTypes;
using MineSharp.World.Generation;
using System.Collections.Concurrent;
using System.Linq;

namespace MineSharp.World;

/// <summary>
/// Manages block storage and queries.
/// Single source of truth for block data.
/// </summary>
public class BlockManager
{
    private readonly ConcurrentDictionary<(int X, int Z), Chunk> _chunks;
    private readonly bool _useTerrainGeneration;
    private readonly ITerrainGenerator? _generator;

    public BlockManager(ITerrainGenerator generator)
    {
        _chunks = new ConcurrentDictionary<(int X, int Z), Chunk>();
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _useTerrainGeneration = false; // Legacy flag, kept for compatibility
    }

    // Convenience constructor for default flat generator
    public BlockManager() : this(new Generation.Generators.FlatWorldGenerator())
    {
    }

    // Legacy constructor - kept for backward compatibility
    public BlockManager(bool useTerrainGeneration = false)
    {
        _chunks = new ConcurrentDictionary<(int X, int Z), Chunk>();
        _useTerrainGeneration = useTerrainGeneration;
        // TODO: Remove this constructor once migration is complete
        _generator = null;
    }

    public Block GetBlock(int x, int y, int z)
    {
        int chunkX = x >> 4; // Divide by 16
        int chunkZ = z >> 4;
        int localX = x & 0xF; // Modulo 16
        int localZ = z & 0xF;
        
        var chunk = GetOrCreateChunk(chunkX, chunkZ);
        return chunk.GetBlock(localX, y, localZ);
    }

    public void SetBlock(int x, int y, int z, Block block)
    {
        int chunkX = x >> 4; // Divide by 16
        int chunkZ = z >> 4;
        int localX = x & 0xF; // Modulo 16
        int localZ = z & 0xF;
        
        var chunk = GetOrCreateChunk(chunkX, chunkZ);
        chunk.SetBlock(localX, y, localZ, block);
    }

    public bool IsChunkLoaded(int chunkX, int chunkZ)
    {
        return _chunks.ContainsKey((chunkX, chunkZ));
    }

    public Chunk GetOrCreateChunk(int chunkX, int chunkZ)
    {
        return _chunks.GetOrAdd((chunkX, chunkZ), _ =>
        {
            var chunk = new Chunk(chunkX, chunkZ);
            
            // Use generator if available, otherwise fall back to legacy method
            if (_generator != null)
            {
                _generator.GenerateChunk(chunk, chunkX, chunkZ);
            }
            else
            {
                GenerateFlatWorldChunk(chunk);
            }
            
            return chunk;
        });
    }

    private void GenerateFlatWorldChunk(Chunk chunk)
    {
        // Flat world: matches Python implementation
        // - Sections below y=63: all dirt (block 2105)
        // - Section with y=63: dirt layer
        // - Section with y=64: grass layer (block 9)
        // - Sections above y=64: all air
        const int groundY = 64;
        const int dirtBlockStateId = 2105; // Dirt block state ID (matches Python)
        
        // Fill all blocks below y=63 with dirt
        for (int y = -64; y < groundY - 1; y++)
        {
            for (int z = 0; z < 16; z++)
            {
                for (int x = 0; x < 16; x++)
                {
                    chunk.SetBlock(x, y, z, new Block(dirtBlockStateId, "minecraft:dirt"));
                }
            }
        }
        
        // Dirt layer at y=63
        for (int z = 0; z < 16; z++)
        {
            for (int x = 0; x < 16; x++)
            {
                chunk.SetBlock(x, groundY - 1, z, new Block(dirtBlockStateId, "minecraft:dirt"));
            }
        }
        
        // Grass layer at y=64
        // Note: Python uses 2098 (lime wool for testing), but actual grass_block is 9
        // Using Python's test ID for comparison
        const int grassBlockStateId = 2098; // Matches Python test ID
        for (int z = 0; z < 16; z++)
        {
            for (int x = 0; x < 16; x++)
            {
                chunk.SetBlock(x, groundY, z, new Block(grassBlockStateId, "minecraft:grass_block"));
            }
        }
        
        // Everything above y=64 is air (already initialized)
    }

    /// <summary>
    /// Gets chunk section data for protocol encoding.
    /// Returns: (blockCount, palette, paletteIndices)
    /// </summary>
    public (short blockCount, List<int> palette, List<int> paletteIndices) GetChunkSectionForProtocol(
        int chunkX, int chunkZ, int sectionIdx)
    {
        var chunk = GetOrCreateChunk(chunkX, chunkZ);
        int sectionYMin = -64 + (sectionIdx * 16);
        int sectionYMax = sectionYMin + 15;
        
        var palette = new HashSet<int>();
        var paletteIndices = new List<int>(4096); // 16x16x16 = 4096 blocks per section
        
        short blockCount = 0;
        
        // Iterate through all blocks in this section
        for (int y = sectionYMin; y <= sectionYMax; y++)
        {
            for (int z = 0; z < 16; z++)
            {
                for (int x = 0; x < 16; x++)
                {
                    int blockStateId = chunk.GetBlockStateId(x, y, z);
                    palette.Add(blockStateId);
                    
                    if (blockStateId != 0) // Not air
                    {
                        blockCount++;
                    }
                    
                    paletteIndices.Add(blockStateId);
                }
            }
        }
        
        // Convert palette set to sorted list for consistent ordering
        var paletteList = palette.OrderBy(id => id).ToList();
        
        // Create mapping from block state ID to palette index
        var paletteMap = new Dictionary<int, int>();
        for (int i = 0; i < paletteList.Count; i++)
        {
            paletteMap[paletteList[i]] = i;
        }
        
        // Convert palette indices to use palette mapping
        var mappedIndices = new List<int>(paletteIndices.Count);
        foreach (var blockStateId in paletteIndices)
        {
            if (!paletteMap.TryGetValue(blockStateId, out int paletteIndex))
            {
                throw new InvalidOperationException($"Block state ID {blockStateId} not found in palette for chunk section {sectionIdx}");
            }
            if (paletteIndex < 0 || paletteIndex >= paletteList.Count)
            {
                throw new InvalidOperationException($"Invalid palette index {paletteIndex} for palette size {paletteList.Count}");
            }
            mappedIndices.Add(paletteIndex);
        }
        
        // Validate all indices are in valid range
        foreach (var idx in mappedIndices)
        {
            if (idx < 0 || idx >= paletteList.Count)
            {
                throw new InvalidOperationException($"Palette index {idx} out of range [0, {paletteList.Count - 1}]");
            }
        }
        
        return (blockCount, paletteList, mappedIndices);
    }

    /// <summary>
    /// Generates heightmap for a chunk (256 entries, one per column).
    /// </summary>
    public int[] GenerateHeightmap(int chunkX, int chunkZ)
    {
        // Use generator if available, otherwise fall back to legacy method
        if (_generator != null)
        {
            return _generator.GenerateHeightmap(chunkX, chunkZ);
        }
        
        // Legacy method
        var chunk = GetOrCreateChunk(chunkX, chunkZ);
        var heightmap = new int[256]; // 16x16 = 256 columns
        
        for (int z = 0; z < 16; z++)
        {
            for (int x = 0; x < 16; x++)
            {
                heightmap[z * 16 + x] = chunk.GetHeightmapValue(x, z);
            }
        }
        
        return heightmap;
    }

    public bool CheckLineIntersectsSolidBlock(Vector3 lineStart, Vector3 lineEnd, out Vector3? intersectionPoint)
    {
        // TODO: Implement line intersection check
        throw new NotImplementedException();
    }
}

/// <summary>
/// Represents a block in the world.
/// </summary>
public class Block
{
    public int BlockStateId { get; set; }
    public string BlockName { get; set; } = string.Empty;

    public Block(int blockStateId, string blockName = "")
    {
        BlockStateId = blockStateId;
        BlockName = blockName;
    }

    public static Block Air() => new Block(0, "minecraft:air");
    public static Block GrassBlock() => new Block(9, "minecraft:grass_block");

    public bool IsAir()
    {
        return BlockStateId == 0;
    }

    public bool IsSolid()
    {
        // For now, only air is non-solid
        return !IsAir();
    }
}

/// <summary>
/// Represents a 16x16x384 chunk of blocks.
/// </summary>
public class Chunk
{
    public int ChunkX { get; }
    public int ChunkZ { get; }
    private readonly Block[] _blocks;
    private const int ChunkSizeX = 16;
    private const int ChunkSizeZ = 16;
    private const int WorldHeight = 384; // -64 to 320
    private const int WorldMinY = -64;

    public Chunk(int chunkX, int chunkZ)
    {
        ChunkX = chunkX;
        ChunkZ = chunkZ;
        _blocks = new Block[ChunkSizeX * ChunkSizeZ * WorldHeight];
        
        // Initialize all blocks to air
        for (int i = 0; i < _blocks.Length; i++)
        {
            _blocks[i] = Block.Air();
        }
    }

    private int GetIndex(int x, int y, int z)
    {
        // Convert world Y to array index (y = -64 maps to index 0)
        int arrayY = y - WorldMinY;
        return (arrayY * ChunkSizeX * ChunkSizeZ) + (z * ChunkSizeX) + x;
    }

    public Block GetBlock(int x, int y, int z)
    {
        if (x < 0 || x >= ChunkSizeX || z < 0 || z >= ChunkSizeZ || y < WorldMinY || y >= WorldMinY + WorldHeight)
        {
            return Block.Air(); // Out of bounds = air
        }
        
        int index = GetIndex(x, y, z);
        return _blocks[index];
    }

    public void SetBlock(int x, int y, int z, Block block)
    {
        if (x < 0 || x >= ChunkSizeX || z < 0 || z >= ChunkSizeZ || y < WorldMinY || y >= WorldMinY + WorldHeight)
        {
            return; // Out of bounds, ignore
        }
        
        int index = GetIndex(x, y, z);
        _blocks[index] = block;
    }

    /// <summary>
    /// Gets block state ID for a position within the chunk (local coordinates 0-15).
    /// </summary>
    public int GetBlockStateId(int x, int y, int z)
    {
        return GetBlock(x, y, z).BlockStateId;
    }

    /// <summary>
    /// Gets the heightmap value for a column (x, z) in the chunk.
    /// Returns the highest non-air block Y coordinate + 1, or 64 for flat world.
    /// </summary>
    public int GetHeightmapValue(int x, int z)
    {
        // For flat world: ground is at y=64
        return 64;
    }
}

