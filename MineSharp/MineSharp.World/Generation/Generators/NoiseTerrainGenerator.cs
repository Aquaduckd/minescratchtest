using MineSharp.World.Generation;
using MineSharp.World.Generation.Utilities;
using System;
using System.Collections.Generic;

namespace MineSharp.World.Generation.Generators;

/// <summary>
/// Generates terrain using Perlin noise for natural-looking landscapes.
/// Based on Python implementation with same parameters.
/// </summary>
public class NoiseTerrainGenerator : ITerrainGenerator
{
    public string GeneratorId => "noise";
    public string DisplayName => "Noise Terrain";
    
    // Noise parameters (matching Python defaults)
    private long _seed = 0;
    private double _scale = 0.03;
    private int _amplitude = 16;
    private int _baseHeight = 64;
    private int _octaves = 1;
    private double _persistence = 0.5;
    private double _lacunarity = 2.0;
    
    // Mountain parameters
    private double _mountainScale = 0.01;
    private int _mountainAmplitude = 300;
    private double _mountainThreshold = 0.5;
    
    // Block IDs (matching Python implementation)
    private const int DirtBlockStateId = 2105;
    private const int GrassBlockStateId = 2098;
    private const int StoneBlockStateId = 2100; // Gray wool (for stone)
    private const int WaterBlockStateId = 86; // Water (full water block, level=0)
    private const int SandBlockStateId = 2097; // Yellow wool (for sand)
    private const int SnowBlockStateId = 2093; // White wool (for snow)
    
    // Cache for height maps
    private readonly Dictionary<(int, int), int[,]> _heightMapCache = new();
    
    public void GenerateChunk(Chunk chunk, int chunkX, int chunkZ)
    {
        // Generate height map for this chunk
        var heightMap = GenerateHeightMap(chunkX, chunkZ);
        
        const int seaLevel = 64;
        const int snowThreshold = 90;
        
        // Fill blocks based on height map
        for (int z = 0; z < 16; z++)
        {
            for (int x = 0; x < 16; x++)
            {
                int surfaceHeight = heightMap[z, x];
                
                for (int y = -64; y <= 320; y++)
                {
                    if (y > surfaceHeight)
                    {
                        // Above surface - air or water
                        if (y < seaLevel)
                        {
                            // Below sea level - fill with water
                            chunk.SetBlock(x, y, z, new Block(WaterBlockStateId, "minecraft:water"));
                        }
                        else
                        {
                            // Above sea level - air (already initialized)
                            // No need to set, chunk is already air
                        }
                    }
                    else if (y == surfaceHeight)
                    {
                        // Surface block - determine type based on height
                        int surfaceBlockId;
                        string surfaceBlockName;
                        if (surfaceHeight >= snowThreshold)
                        {
                            // Mountain peaks - white wool (snow)
                            surfaceBlockId = SnowBlockStateId;
                            surfaceBlockName = "minecraft:white_wool";
                        }
                        else if (surfaceHeight <= seaLevel)
                        {
                            // Sea level and below - yellow wool (sand)
                            surfaceBlockId = SandBlockStateId;
                            surfaceBlockName = "minecraft:yellow_wool";
                        }
                        else
                        {
                            // Normal terrain - grass
                            surfaceBlockId = GrassBlockStateId;
                            surfaceBlockName = "minecraft:grass_block";
                        }
                        chunk.SetBlock(x, y, z, new Block(surfaceBlockId, surfaceBlockName));
                    }
                    else if (y >= surfaceHeight - 3)
                    {
                        // Top 3 blocks below surface - dirt
                        chunk.SetBlock(x, y, z, new Block(DirtBlockStateId, "minecraft:dirt"));
                    }
                    else
                    {
                        // Below dirt layer - gray wool (stone)
                        chunk.SetBlock(x, y, z, new Block(StoneBlockStateId, "minecraft:gray_wool"));
                    }
                }
            }
        }
    }
    
    public int[] GenerateHeightmap(int chunkX, int chunkZ)
    {
        var heightMap = GenerateHeightMap(chunkX, chunkZ);
        var heightmap = new int[256]; // 16x16 = 256 columns
        
        for (int z = 0; z < 16; z++)
        {
            for (int x = 0; x < 16; x++)
            {
                // Heightmap is highest solid block + 1
                heightmap[z * 16 + x] = heightMap[z, x] + 1;
            }
        }
        
        return heightmap;
    }
    
    private int[,] GenerateHeightMap(int chunkX, int chunkZ)
    {
        // Check cache
        var cacheKey = (chunkX, chunkZ);
        if (_heightMapCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }
        
        // Generate height map (16x16)
        var heightMap = new int[16, 16];
        
        for (int z = 0; z < 16; z++)
        {
            for (int x = 0; x < 16; x++)
            {
                // Convert local chunk coordinates to world coordinates
                int worldX = chunkX * 16 + x;
                int worldZ = chunkZ * 16 + z;
                
                // Generate base terrain noise value
                double noiseValue = GetNoise(worldX, worldZ);
                
                // Generate mountain placement noise
                double mountainNoise = GetMountainNoise(worldX, worldZ);
                
                // Calculate effective amplitude based on mountain noise
                int effectiveAmplitude;
                if (mountainNoise > _mountainThreshold)
                {
                    // In mountain areas: use higher amplitude
                    double mountainFactor = (mountainNoise - _mountainThreshold) / (1.0 - _mountainThreshold);
                    effectiveAmplitude = _amplitude + (int)(_mountainAmplitude * mountainFactor);
                }
                else
                {
                    // In normal areas: use base amplitude
                    effectiveAmplitude = _amplitude;
                }
                
                // Convert noise value (-1 to 1) to height using effective amplitude
                int height = (int)(_baseHeight + noiseValue * effectiveAmplitude);
                
                // Clamp height to reasonable bounds (0-255)
                height = Math.Max(0, Math.Min(255, height));
                
                heightMap[z, x] = height;
            }
        }
        
        // Cache the result
        _heightMapCache[cacheKey] = heightMap;
        
        return heightMap;
    }
    
    private double GetNoise(double x, double z)
    {
        // Use seed as offset to ensure different worlds
        int seedOffset = (int)_seed;
        
        // Use octave-based noise
        return PerlinNoise.Noise2DOctaves(x, z, _octaves, _persistence, _lacunarity, _scale, seedOffset);
    }
    
    private double GetMountainNoise(double x, double z)
    {
        // Use different seed offset to ensure mountains are independent of base terrain
        int seedOffset = (int)_seed;
        
        // Use larger scale (lower frequency) for mountain placement
        // This creates large regions where mountains can appear
        double noiseValue = PerlinNoise.Noise2DOctaves(
            x * _mountainScale,
            z * _mountainScale,
            2, // Fewer octaves for smoother mountain regions
            0.5,
            2.0,
            1.0,
            seedOffset + 1000 // Different seed offset
        );
        
        // Normalize from [-1, 1] to [0, 1]
        return (noiseValue + 1.0) / 2.0;
    }
    
    public GeneratorConfigSchema? GetConfigSchema()
    {
        return new GeneratorConfigSchema
        {
            Properties = new Dictionary<string, ConfigProperty>
            {
                ["seed"] = new ConfigProperty
                {
                    Type = "long",
                    Description = "Random seed for deterministic generation",
                    DefaultValue = 0L
                },
                ["scale"] = new ConfigProperty
                {
                    Type = "double",
                    Description = "Noise scale (lower = larger features, higher = smaller features)",
                    DefaultValue = 0.03
                },
                ["amplitude"] = new ConfigProperty
                {
                    Type = "int",
                    Description = "Base height variation range (how much terrain varies)",
                    DefaultValue = 16
                },
                ["baseHeight"] = new ConfigProperty
                {
                    Type = "int",
                    Description = "Base Y level for terrain",
                    DefaultValue = 64
                },
                ["octaves"] = new ConfigProperty
                {
                    Type = "int",
                    Description = "Number of noise layers for detail",
                    DefaultValue = 1
                },
                ["persistence"] = new ConfigProperty
                {
                    Type = "double",
                    Description = "How much each octave contributes (0-1)",
                    DefaultValue = 0.5
                },
                ["lacunarity"] = new ConfigProperty
                {
                    Type = "double",
                    Description = "Frequency multiplier between octaves",
                    DefaultValue = 2.0
                },
                ["mountainScale"] = new ConfigProperty
                {
                    Type = "double",
                    Description = "Scale for mountain placement noise (lower = larger mountain regions)",
                    DefaultValue = 0.01
                },
                ["mountainAmplitude"] = new ConfigProperty
                {
                    Type = "int",
                    Description = "Additional amplitude for mountains (added to base amplitude)",
                    DefaultValue = 300
                },
                ["mountainThreshold"] = new ConfigProperty
                {
                    Type = "double",
                    Description = "Threshold (0-1) above which mountains appear (higher = rarer mountains)",
                    DefaultValue = 0.5
                }
            }
        };
    }
    
    public void Configure(GeneratorConfig? config)
    {
        if (config == null) return;
        
        if (config.TryGetValue("seed", out var seed))
            _seed = Convert.ToInt64(seed);
        if (config.TryGetValue("scale", out var scale))
            _scale = Convert.ToDouble(scale);
        if (config.TryGetValue("amplitude", out var amp))
            _amplitude = Convert.ToInt32(amp);
        if (config.TryGetValue("baseHeight", out var baseH))
            _baseHeight = Convert.ToInt32(baseH);
        if (config.TryGetValue("octaves", out var oct))
            _octaves = Convert.ToInt32(oct);
        if (config.TryGetValue("persistence", out var pers))
            _persistence = Convert.ToDouble(pers);
        if (config.TryGetValue("lacunarity", out var lac))
            _lacunarity = Convert.ToDouble(lac);
        if (config.TryGetValue("mountainScale", out var mScale))
            _mountainScale = Convert.ToDouble(mScale);
        if (config.TryGetValue("mountainAmplitude", out var mAmp))
            _mountainAmplitude = Convert.ToInt32(mAmp);
        if (config.TryGetValue("mountainThreshold", out var mThresh))
            _mountainThreshold = Convert.ToDouble(mThresh);
        
        // Clear cache when configuration changes
        _heightMapCache.Clear();
    }
}

