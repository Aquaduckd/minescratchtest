using System;
using MineSharp.Network.ChunkLoading;
using Xunit;

namespace MineSharp.Tests.Network.ChunkLoading;

public class ChunkLoadPriorityCalculatorTests
{
    [Fact]
    public void CalculatePriority_CloserChunks_HaveHigherPriority()
    {
        // Arrange
        int playerChunkX = 0;
        int playerChunkZ = 0;
        var createdAt = DateTime.UtcNow;

        // Act
        int closePriority = ChunkLoadPriorityCalculator.CalculatePriority(1, 1, playerChunkX, playerChunkZ, createdAt, 0);
        int farPriority = ChunkLoadPriorityCalculator.CalculatePriority(10, 10, playerChunkX, playerChunkZ, createdAt, 0);

        // Assert
        Assert.True(closePriority > farPriority, "Closer chunks should have higher priority");
    }

    [Fact]
    public void CalculatePriority_SameDistance_SamePriority()
    {
        // Arrange
        int playerChunkX = 0;
        int playerChunkZ = 0;
        var createdAt = DateTime.UtcNow;

        // Act
        int priority1 = ChunkLoadPriorityCalculator.CalculatePriority(5, 0, playerChunkX, playerChunkZ, createdAt, 0);
        int priority2 = ChunkLoadPriorityCalculator.CalculatePriority(0, 5, playerChunkX, playerChunkZ, createdAt, 0);

        // Assert
        Assert.Equal(priority1, priority2);
    }

    [Fact]
    public void CalculatePriority_FewerRetries_HaveHigherPriority()
    {
        // Arrange
        int playerChunkX = 0;
        int playerChunkZ = 0;
        var createdAt = DateTime.UtcNow;

        // Act
        int noRetryPriority = ChunkLoadPriorityCalculator.CalculatePriority(5, 5, playerChunkX, playerChunkZ, createdAt, 0);
        int oneRetryPriority = ChunkLoadPriorityCalculator.CalculatePriority(5, 5, playerChunkX, playerChunkZ, createdAt, 1);
        int manyRetryPriority = ChunkLoadPriorityCalculator.CalculatePriority(5, 5, playerChunkX, playerChunkZ, createdAt, 5);

        // Assert
        Assert.True(noRetryPriority > oneRetryPriority, "Fewer retries should have higher priority");
        Assert.True(oneRetryPriority > manyRetryPriority, "Fewer retries should have higher priority");
    }

    [Fact]
    public void CalculatePriority_StableChunks_HaveHigherPriority()
    {
        // Arrange
        int playerChunkX = 0;
        int playerChunkZ = 0;
        var createdAt = DateTime.UtcNow;

        // Act
        int stablePriority = ChunkLoadPriorityCalculator.CalculatePriority(5, 5, playerChunkX, playerChunkZ, createdAt, 0, isStable: true);
        int unstablePriority = ChunkLoadPriorityCalculator.CalculatePriority(5, 5, playerChunkX, playerChunkZ, createdAt, 0, isStable: false);

        // Assert
        Assert.True(stablePriority > unstablePriority, "Stable chunks should have higher priority");
    }

    [Fact]
    public void CalculatePriority_OlderChunks_GetSlightBoost()
    {
        // Arrange
        int playerChunkX = 0;
        int playerChunkZ = 0;
        var oldCreatedAt = DateTime.UtcNow.AddSeconds(-10);
        var newCreatedAt = DateTime.UtcNow;

        // Act
        int oldPriority = ChunkLoadPriorityCalculator.CalculatePriority(5, 5, playerChunkX, playerChunkZ, oldCreatedAt, 0);
        int newPriority = ChunkLoadPriorityCalculator.CalculatePriority(5, 5, playerChunkX, playerChunkZ, newCreatedAt, 0);

        // Assert
        Assert.True(oldPriority > newPriority, "Older chunks should get slight priority boost");
    }

    [Fact]
    public void CalculatePriority_DistanceDominates_OverOtherFactors()
    {
        // Arrange
        int playerChunkX = 0;
        int playerChunkZ = 0;
        var createdAt = DateTime.UtcNow;

        // Act
        // Close chunk with retries
        int closeWithRetries = ChunkLoadPriorityCalculator.CalculatePriority(1, 1, playerChunkX, playerChunkZ, createdAt, 3);
        // Far chunk with no retries
        int farNoRetries = ChunkLoadPriorityCalculator.CalculatePriority(20, 20, playerChunkX, playerChunkZ, createdAt, 0);

        // Assert
        Assert.True(closeWithRetries > farNoRetries, "Distance should dominate - close chunks load first even with retries");
    }

    [Fact]
    public void CalculatePriority_PlayerAtOrigin_ChunkAtOrigin_HasHighestPriority()
    {
        // Arrange
        int playerChunkX = 0;
        int playerChunkZ = 0;
        var createdAt = DateTime.UtcNow;

        // Act
        int originPriority = ChunkLoadPriorityCalculator.CalculatePriority(0, 0, playerChunkX, playerChunkZ, createdAt, 0);
        int nearbyPriority = ChunkLoadPriorityCalculator.CalculatePriority(1, 0, playerChunkX, playerChunkZ, createdAt, 0);

        // Assert
        Assert.True(originPriority > nearbyPriority, "Chunk at player position should have highest priority");
    }
}

