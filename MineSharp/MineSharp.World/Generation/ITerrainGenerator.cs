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

