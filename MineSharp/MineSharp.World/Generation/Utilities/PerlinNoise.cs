using System;

namespace MineSharp.World.Generation.Utilities;

/// <summary>
/// Simple Perlin noise implementation for terrain generation.
/// Based on classic Perlin noise algorithm.
/// </summary>
public static class PerlinNoise
{
    private static readonly int[] Permutation = {
        151, 160, 137, 91, 90, 15, 131, 13, 201, 95, 96, 53, 194, 233, 7, 225,
        140, 36, 103, 30, 69, 142, 8, 99, 37, 240, 21, 10, 23, 190, 6, 148,
        247, 120, 234, 75, 0, 26, 197, 62, 94, 252, 219, 203, 117, 35, 11, 32,
        57, 177, 33, 88, 237, 149, 56, 87, 174, 20, 125, 136, 171, 168, 68, 175,
        74, 165, 71, 134, 139, 48, 27, 166, 77, 146, 158, 231, 83, 111, 229, 122,
        60, 211, 133, 230, 220, 105, 92, 41, 55, 46, 245, 40, 244, 102, 143, 54,
        65, 25, 63, 161, 1, 216, 80, 73, 209, 76, 132, 187, 208, 89, 18, 169,
        200, 196, 135, 130, 116, 188, 159, 86, 164, 100, 109, 198, 173, 186, 3, 64,
        52, 217, 226, 250, 124, 123, 5, 202, 38, 147, 118, 126, 255, 82, 85, 212,
        207, 206, 59, 227, 47, 16, 58, 17, 182, 189, 28, 42, 223, 183, 170, 213,
        119, 248, 152, 2, 44, 154, 163, 70, 221, 153, 101, 155, 167, 43, 172, 9,
        129, 22, 39, 253, 19, 98, 108, 110, 79, 113, 224, 232, 178, 185, 112, 104,
        218, 246, 97, 228, 251, 34, 242, 193, 238, 210, 144, 12, 191, 179, 162, 241,
        81, 51, 145, 235, 249, 14, 239, 107, 49, 192, 214, 31, 181, 199, 106, 157,
        184, 84, 204, 176, 115, 121, 50, 45, 127, 4, 150, 254, 138, 236, 205, 93,
        222, 114, 67, 29, 24, 72, 243, 141, 128, 195, 78, 66, 215, 61, 156, 180
    };

    private static readonly int[] P;

    static PerlinNoise()
    {
        // Double the permutation array for wrapping
        P = new int[512];
        for (int i = 0; i < 256; i++)
        {
            P[256 + i] = P[i] = Permutation[i];
        }
    }

    /// <summary>
    /// Generate 2D Perlin noise value at given coordinates.
    /// </summary>
    /// <param name="x">X coordinate</param>
    /// <param name="z">Z coordinate</param>
    /// <param name="seed">Seed offset for variation</param>
    /// <returns>Noise value in range [-1, 1]</returns>
    public static double Noise2D(double x, double z, int seed = 0)
    {
        // Apply seed offset
        x += seed * 100.0;
        z += seed * 200.0;

        // Get integer and fractional parts
        int X = (int)Math.Floor(x) & 255;
        int Z = (int)Math.Floor(z) & 255;

        x -= Math.Floor(x);
        z -= Math.Floor(z);

        // Fade curves
        double u = Fade(x);
        double v = Fade(z);

        // Hash coordinates of the 4 square corners
        int A = P[X] + Z;
        int AA = P[A];
        int AB = P[A + 1];
        int B = P[X + 1] + Z;
        int BA = P[B];
        int BB = P[B + 1];

        // Blend the 4 corner values
        return Lerp(v,
            Lerp(u, Grad(P[AA], x, z, 0),
                    Grad(P[BA], x - 1, z, 0)),
            Lerp(u, Grad(P[AB], x, z - 1, 0),
                    Grad(P[BB], x - 1, z - 1, 0)));
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

    private static double Grad(int hash, double x, double z, double y)
    {
        int h = hash & 15;
        double u = h < 8 ? x : z;
        double v = h < 4 ? z : (h == 12 || h == 14 ? x : y);
        return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
    }
}

