using System;

namespace MineSharp.World.Generation.Utilities;

/// <summary>
/// Simple Perlin noise implementation for terrain generation.
/// Uses hash-based permutation to support infinite terrain without repetition.
/// Based on classic Perlin noise algorithm with hash-based gradients.
/// </summary>
public static class PerlinNoise
{
    // Gradient vectors for 2D noise (12 directions)
    // Each pair represents (gx, gz) for a gradient direction
    private static readonly double[] Gradients2D = {
        1.0, 1.0,   // Diagonal
        -1.0, 1.0,  // Diagonal
        1.0, -1.0,  // Diagonal
        -1.0, -1.0, // Diagonal
        1.0, 0.0,   // Horizontal
        -1.0, 0.0,  // Horizontal
        0.0, 1.0,   // Vertical
        0.0, -1.0,  // Vertical
        1.0, 1.0,   // Diagonal (duplicate for variety)
        0.0, -1.0,  // Vertical (duplicate)
        -1.0, 1.0,  // Diagonal (duplicate)
        0.0, 1.0    // Vertical (duplicate)
    };
    
    private const int GradientCount = 12; // Number of gradient directions

    /// <summary>
    /// Hash function for generating pseudo-random values from coordinates.
    /// Uses a simple but effective hash that works well for noise generation.
    /// </summary>
    private static int Hash(int x, int z, int seed)
    {
        // Combine coordinates with seed using prime multipliers
        // This creates a good distribution without repetition
        // Note: XOR has lower precedence than multiplication, so we need parentheses
        uint hash = ((uint)x * 73856093u) ^ ((uint)z * 19349663u) ^ ((uint)seed * 83492791u);
        
        // Mix bits for better distribution
        hash ^= hash >> 16;
        hash *= 0x85ebca6b;
        hash ^= hash >> 13;
        hash *= 0xc2b2ae35;
        hash ^= hash >> 16;
        
        return (int)hash;
    }

    /// <summary>
    /// Generate 2D Perlin noise value at given coordinates.
    /// Uses hash-based permutation to support infinite terrain without repetition.
    /// </summary>
    /// <param name="x">X coordinate</param>
    /// <param name="z">Z coordinate</param>
    /// <param name="seed">Seed offset for variation</param>
    /// <returns>Noise value in range [-1, 1]</returns>
    public static double Noise2D(double x, double z, int seed = 0)
    {
        // Get integer and fractional parts
        int X = (int)Math.Floor(x);
        int Z = (int)Math.Floor(z);

        x -= X;
        z -= Z;

        // Fade curves
        double u = Fade(x);
        double v = Fade(z);

        // Hash the 4 square corners to get gradient indices
        int hash00 = Hash(X, Z, seed);
        int hash10 = Hash(X + 1, Z, seed);
        int hash01 = Hash(X, Z + 1, seed);
        int hash11 = Hash(X + 1, Z + 1, seed);

        // Get gradient indices (modulo gradient count)
        int grad00 = Math.Abs(hash00) % GradientCount;
        int grad10 = Math.Abs(hash10) % GradientCount;
        int grad01 = Math.Abs(hash01) % GradientCount;
        int grad11 = Math.Abs(hash11) % GradientCount;

        // Calculate dot products with gradient vectors
        double dot00 = DotGradient2D(grad00, x, z);
        double dot10 = DotGradient2D(grad10, x - 1, z);
        double dot01 = DotGradient2D(grad01, x, z - 1);
        double dot11 = DotGradient2D(grad11, x - 1, z - 1);

        // Blend the 4 corner values using bilinear interpolation
        return Lerp(v,
            Lerp(u, dot00, dot10),
            Lerp(u, dot01, dot11));
    }

    /// <summary>
    /// Calculate dot product with a 2D gradient vector.
    /// </summary>
    private static double DotGradient2D(int gradIndex, double x, double z)
    {
        int index = (Math.Abs(gradIndex) % GradientCount) * 2;
        double gx = Gradients2D[index];
        double gz = Gradients2D[index + 1];
        return gx * x + gz * z;
    }

    /// <summary>
    /// Generate octave-based Perlin noise (fractal noise).
    /// </summary>
    /// <param name="x">X coordinate</param>
    /// <param name="z">Z coordinate</param>
    /// <param name="octaves">Number of octaves</param>
    /// <param name="persistence">How much each octave contributes (0-1)</param>
    /// <param name="lacunarity">Frequency multiplier between octaves</param>
    /// <param name="scale">Base frequency scale</param>
    /// <param name="seed">Seed offset</param>
    /// <returns>Noise value in range [-1, 1]</returns>
    public static double Noise2DOctaves(double x, double z, int octaves, double persistence, double lacunarity, double scale, int seed = 0)
    {
        double total = 0.0;
        double frequency = scale;
        double amplitude = 1.0;
        double maxValue = 0.0;

        for (int i = 0; i < octaves; i++)
        {
            double noiseValue = Noise2D(x * frequency, z * frequency, seed);
            total += noiseValue * amplitude;
            maxValue += amplitude;

            amplitude *= persistence;
            frequency *= lacunarity;
        }

        // Normalize to [-1, 1] range
        if (maxValue > 0)
        {
            total /= maxValue;
        }

        return total;
    }

    private static double Fade(double t)
    {
        return t * t * t * (t * (t * 6 - 15) + 10);
    }

    private static double Lerp(double t, double a, double b)
    {
        return a + t * (b - a);
    }

}

