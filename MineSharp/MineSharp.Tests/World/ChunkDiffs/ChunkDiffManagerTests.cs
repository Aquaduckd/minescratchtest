using MineSharp.World.ChunkDiffs;
using MineSharp.World;
using Xunit;

namespace MineSharp.Tests.World.ChunkDiffs;

public class ChunkDiffManagerTests
{
    [Fact]
    public void Instance_ReturnsSameInstance()
    {
        // Act
        var instance1 = ChunkDiffManager.Instance;
        var instance2 = ChunkDiffManager.Instance;

        // Assert
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void GetOrCreateDiff_CreatesNewDiff_WhenNotExists()
    {
        // Arrange
        var manager = ChunkDiffManager.Instance;
        // Use unique chunk coordinates to avoid conflicts with other tests
        int chunkX = 999;
        int chunkZ = 999;

        // Act
        var diff = manager.GetOrCreateDiff(chunkX, chunkZ);

        // Assert
        Assert.NotNull(diff);
        Assert.Equal(chunkX, diff.ChunkX);
        Assert.Equal(chunkZ, diff.ChunkZ);
        Assert.True(diff.IsEmpty);
    }

    [Fact]
    public void GetOrCreateDiff_ReturnsExistingDiff_WhenExists()
    {
        // Arrange
        var manager = ChunkDiffManager.Instance;
        int chunkX = 5;
        int chunkZ = 10;
        
        var diff1 = manager.GetOrCreateDiff(chunkX, chunkZ);
        diff1.SetBlock(80, 64, 160, 9); // Block in chunk (5, 10): 80 >> 4 = 5, 160 >> 4 = 10

        // Act
        var diff2 = manager.GetOrCreateDiff(chunkX, chunkZ);

        // Assert
        Assert.Same(diff1, diff2);
        Assert.False(diff2.IsEmpty);
        Assert.Equal(9, diff2.GetBlock(80, 64, 160));
    }

    [Fact]
    public void GetDiff_ReturnsNull_WhenDiffNotExists()
    {
        // Arrange
        var manager = ChunkDiffManager.Instance;

        // Act
        var diff = manager.GetDiff(99, 99);

        // Assert
        Assert.Null(diff);
    }

    [Fact]
    public void GetDiff_ReturnsDiff_WhenDiffExists()
    {
        // Arrange
        var manager = ChunkDiffManager.Instance;
        int chunkX = 5;
        int chunkZ = 10;
        
        var createdDiff = manager.GetOrCreateDiff(chunkX, chunkZ);
        createdDiff.SetBlock(80, 64, 160, 9);

        // Act
        var retrievedDiff = manager.GetDiff(chunkX, chunkZ);

        // Assert
        Assert.NotNull(retrievedDiff);
        Assert.Same(createdDiff, retrievedDiff);
    }

    [Fact]
    public void RecordBlockChange_CreatesDiffAndRecordsChange()
    {
        // Arrange
        var manager = ChunkDiffManager.Instance;
        int worldX = 80; // In chunk 5 (80 >> 4 = 5)
        int worldY = 64;
        int worldZ = 160; // In chunk 10 (160 >> 4 = 10)
        int blockStateId = 9;

        // Act
        manager.RecordBlockChange(worldX, worldY, worldZ, blockStateId);

        // Assert
        var diff = manager.GetDiff(5, 10);
        Assert.NotNull(diff);
        Assert.Equal(blockStateId, diff.GetBlock(worldX, worldY, worldZ));
    }

    [Fact]
    public void RecordBlockChange_UpdatesExistingChange()
    {
        // Arrange
        var manager = ChunkDiffManager.Instance;
        int worldX = 80;
        int worldY = 64;
        int worldZ = 160;

        // Act
        manager.RecordBlockChange(worldX, worldY, worldZ, 9);
        manager.RecordBlockChange(worldX, worldY, worldZ, 10); // Update

        // Assert
        var diff = manager.GetDiff(5, 10);
        Assert.Equal(10, diff.GetBlock(worldX, worldY, worldZ));
        Assert.Equal(1, diff.Count); // Still only one change
    }

    [Fact]
    public void ApplyDiffsToChunk_AppliesChangesToChunk()
    {
        // Arrange
        var manager = ChunkDiffManager.Instance;
        var chunk = new Chunk(1, 2);
        
        // Record some block changes in chunk (1, 2)
        // Block at world (25, 64, 35) is in chunk (1, 2): 25 >> 4 = 1, 35 >> 4 = 2
        manager.RecordBlockChange(25, 64, 35, 9);
        manager.RecordBlockChange(26, 65, 36, 10);

        // Act
        manager.ApplyDiffsToChunk(chunk);

        // Assert
        // Local coordinates: 25 - (1 * 16) = 9, 35 - (2 * 16) = 3
        Assert.Equal(9, chunk.GetBlockStateId(9, 64, 3));
        Assert.Equal(10, chunk.GetBlockStateId(10, 65, 4));
    }

    [Fact]
    public void ApplyDiffsToChunk_DoesNothing_WhenNoDiffsExist()
    {
        // Arrange
        var manager = ChunkDiffManager.Instance;
        var chunk = new Chunk(99, 99); // Chunk with no diffs
        
        // Generate some default blocks (e.g., air)
        int originalBlockId = chunk.GetBlockStateId(0, 64, 0);

        // Act
        manager.ApplyDiffsToChunk(chunk);

        // Assert
        // Block should remain unchanged
        Assert.Equal(originalBlockId, chunk.GetBlockStateId(0, 64, 0));
    }

    [Fact]
    public void ApplyDiffsToChunk_DoesNothing_WhenDiffIsEmpty()
    {
        // Arrange
        var manager = ChunkDiffManager.Instance;
        // Use unique chunk coordinates
        int chunkX = 998;
        int chunkZ = 998;
        var chunk = new Chunk(chunkX, chunkZ);
        
        // Create an empty diff
        var diff = manager.GetOrCreateDiff(chunkX, chunkZ);
        Assert.True(diff.IsEmpty);

        int originalBlockId = chunk.GetBlockStateId(0, 64, 0);

        // Act
        manager.ApplyDiffsToChunk(chunk);

        // Assert
        // Block should remain unchanged
        Assert.Equal(originalBlockId, chunk.GetBlockStateId(0, 64, 0));
    }

    [Fact]
    public void ClearDiff_RemovesDiff()
    {
        // Arrange
        var manager = ChunkDiffManager.Instance;
        int chunkX = 5;
        int chunkZ = 10;
        
        manager.RecordBlockChange(80, 64, 160, 9);
        Assert.NotNull(manager.GetDiff(chunkX, chunkZ));

        // Act
        manager.ClearDiff(chunkX, chunkZ);

        // Assert
        var diff = manager.GetDiff(chunkX, chunkZ);
        Assert.Null(diff); // Diff should be removed
    }

    [Fact]
    public void ClearDiff_DoesNothing_WhenDiffNotExists()
    {
        // Arrange
        var manager = ChunkDiffManager.Instance;

        // Act & Assert (should not throw)
        manager.ClearDiff(99, 99);
    }

    [Fact]
    public void GetStatistics_ReturnsCorrectCounts()
    {
        // Arrange
        var manager = ChunkDiffManager.Instance;
        
        // Clear any existing state for clean test
        manager.ClearDiff(1, 2);
        manager.ClearDiff(3, 4);
        
        manager.RecordBlockChange(25, 64, 35, 9); // Chunk (1, 2): 1 change
        manager.RecordBlockChange(26, 65, 36, 10); // Chunk (1, 2): 2 changes
        manager.RecordBlockChange(50, 64, 70, 11); // Chunk (3, 4): 1 change

        // Act
        var (chunkCount, totalBlockChanges) = manager.GetStatistics();

        // Assert
        Assert.True(chunkCount >= 2); // At least 2 chunks
        Assert.True(totalBlockChanges >= 3); // At least 3 block changes
        
        // Verify specific chunks
        var diff1 = manager.GetDiff(1, 2);
        var diff2 = manager.GetDiff(3, 4);
        if (diff1 != null && diff2 != null)
        {
            Assert.Equal(2, diff1.Count);
            Assert.Equal(1, diff2.Count);
        }
    }

    [Fact]
    public void RecordBlockChange_HandlesMultipleChunks()
    {
        // Arrange
        var manager = ChunkDiffManager.Instance;
        
        // Blocks in different chunks
        manager.RecordBlockChange(25, 64, 35, 9);   // Chunk (1, 2)
        manager.RecordBlockChange(50, 64, 70, 10);  // Chunk (3, 4)
        manager.RecordBlockChange(80, 64, 160, 11); // Chunk (5, 10)

        // Act & Assert
        var diff1 = manager.GetDiff(1, 2);
        var diff2 = manager.GetDiff(3, 4);
        var diff3 = manager.GetDiff(5, 10);
        
        Assert.NotNull(diff1);
        Assert.NotNull(diff2);
        Assert.NotNull(diff3);
        
        Assert.Equal(9, diff1.GetBlock(25, 64, 35));
        Assert.Equal(10, diff2.GetBlock(50, 64, 70));
        Assert.Equal(11, diff3.GetBlock(80, 64, 160));
    }

    [Fact]
    public void ApplyDiffsToChunk_HandlesNegativeCoordinates()
    {
        // Arrange
        var manager = ChunkDiffManager.Instance;
        var chunk = new Chunk(-1, -1);
        
        // Block at world (-10, 64, -5) is in chunk (-1, -1)
        manager.RecordBlockChange(-10, 64, -5, 9);

        // Act
        manager.ApplyDiffsToChunk(chunk);

        // Assert
        // Local coordinates: -10 - (-1 * 16) = 6, -5 - (-1 * 16) = 11
        Assert.Equal(9, chunk.GetBlockStateId(6, 64, 11));
    }
}



