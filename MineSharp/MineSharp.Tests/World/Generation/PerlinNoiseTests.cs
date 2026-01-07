using System;
using MineSharp.World.Generation.Utilities;
using Xunit;

namespace MineSharp.Tests.World.Generation;

public class PerlinNoiseTests
{
    [Fact]
    public void Noise2D_ReturnsDeterministicValues()
    {
        // Arrange
        double x = 10.5;
        double z = 20.3;
        int seed = 42;

        // Act
        double value1 = PerlinNoise.Noise2D(x, z, seed);
        double value2 = PerlinNoise.Noise2D(x, z, seed);

        // Assert
        Assert.Equal(value1, value2, precision: 10);
    }

    [Fact]
    public void Noise2D_ReturnsValuesInRange()
    {
        // Arrange
        Random random = new Random(12345);
        
        // Act & Assert
        for (int i = 0; i < 100; i++)
        {
            double x = random.NextDouble() * 1000;
            double z = random.NextDouble() * 1000;
            double value = PerlinNoise.Noise2D(x, z, 0);
            
            Assert.True(value >= -1.0 && value <= 1.0, 
                $"Noise value {value} at ({x}, {z}) is outside [-1, 1] range");
        }
    }

    [Fact]
    public void Noise2D_DifferentSeedsProduceDifferentValues()
    {
        // Arrange
        double x = 10.5;
        double z = 20.3;

        // Act
        double value1 = PerlinNoise.Noise2D(x, z, seed: 0);
        double value2 = PerlinNoise.Noise2D(x, z, seed: 1);

        // Assert
        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Noise2D_NoRepetitionAt256BlockIntervals()
    {
        // Arrange
        double z = 100.5; // Use non-integer to avoid edge cases
        int seed = 0;

        // Act
        double value0 = PerlinNoise.Noise2D(0.5, z, seed);
        double value256 = PerlinNoise.Noise2D(256.5, z, seed);
        double value512 = PerlinNoise.Noise2D(512.5, z, seed);

        // Assert
        // With hash-based approach, these should be different (no repetition)
        // Use a small epsilon to account for floating point precision
        const double epsilon = 0.0001;
        Assert.True(Math.Abs(value0 - value256) > epsilon, 
            $"Values at 0.5 and 256.5 should differ, but got {value0} and {value256}");
        Assert.True(Math.Abs(value256 - value512) > epsilon, 
            $"Values at 256.5 and 512.5 should differ, but got {value256} and {value512}");
        Assert.True(Math.Abs(value0 - value512) > epsilon, 
            $"Values at 0.5 and 512.5 should differ, but got {value0} and {value512}");
    }

    [Fact]
    public void Noise2D_ContinuousValues()
    {
        // Arrange
        double z = 100.0;
        int seed = 0;
        double step = 0.1;

        // Act & Assert
        // Check that nearby values are similar (continuity)
        for (double x = 0; x < 10; x += step)
        {
            double value1 = PerlinNoise.Noise2D(x, z, seed);
            double value2 = PerlinNoise.Noise2D(x + step, z, seed);
            
            // Values should be similar (difference should be small)
            double diff = Math.Abs(value1 - value2);
            Assert.True(diff < 0.5, 
                $"Noise values at {x} and {x + step} differ by {diff}, indicating discontinuity");
        }
    }

    [Fact]
    public void Noise2DOctaves_WorksWithMultipleOctaves()
    {
        // Arrange
        double x = 10.5;
        double z = 20.3;
        int seed = 42;

        // Act
        double value1 = PerlinNoise.Noise2DOctaves(x, z, octaves: 1, persistence: 0.5, lacunarity: 2.0, scale: 0.03, seed);
        double value2 = PerlinNoise.Noise2DOctaves(x, z, octaves: 3, persistence: 0.5, lacunarity: 2.0, scale: 0.03, seed);

        // Assert
        Assert.True(value1 >= -1.0 && value1 <= 1.0);
        Assert.True(value2 >= -1.0 && value2 <= 1.0);
        // More octaves should produce different (usually more detailed) noise
        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Noise2DOctaves_NoRepetitionAt256BlockIntervals()
    {
        // Arrange
        double z = 100.0;
        int seed = 0;

        // Act
        double value0 = PerlinNoise.Noise2DOctaves(0.0, z, octaves: 1, persistence: 0.5, lacunarity: 2.0, scale: 0.03, seed);
        double value256 = PerlinNoise.Noise2DOctaves(256.0, z, octaves: 1, persistence: 0.5, lacunarity: 2.0, scale: 0.03, seed);
        double value512 = PerlinNoise.Noise2DOctaves(512.0, z, octaves: 1, persistence: 0.5, lacunarity: 2.0, scale: 0.03, seed);

        // Assert
        // With hash-based approach, these should be different (no repetition)
        Assert.NotEqual(value0, value256, precision: 5);
        Assert.NotEqual(value256, value512, precision: 5);
    }

    [Fact]
    public void Noise2D_LargeCoordinatesWork()
    {
        // Arrange
        // Test with very large coordinates to ensure hash function works correctly
        double x1 = 1000000.5;
        double z1 = 2000000.3;
        double x2 = 1000001.5;
        double z2 = 2000001.3;
        int seed = 0;

        // Act
        double value1 = PerlinNoise.Noise2D(x1, z1, seed);
        double value2 = PerlinNoise.Noise2D(x2, z2, seed);

        // Assert
        Assert.True(value1 >= -1.0 && value1 <= 1.0);
        Assert.True(value2 >= -1.0 && value2 <= 1.0);
        // Nearby values should be similar
        double diff = Math.Abs(value1 - value2);
        Assert.True(diff < 0.5, "Large coordinates should still produce continuous noise");
    }
}

