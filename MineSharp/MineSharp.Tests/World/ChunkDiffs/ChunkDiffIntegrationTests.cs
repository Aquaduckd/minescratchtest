using MineSharp.World.ChunkDiffs;
using MineSharp.World;
using MineSharp.World.Generation.Generators;
using Xunit;

namespace MineSharp.Tests.World.ChunkDiffs;

public class ChunkDiffIntegrationTests
{
    [Fact]
    public void DiffPersistsAcrossChunkCreation()
    {
        // Arrange
        var manager = ChunkDiffManager.Instance;
        var blockManager = new BlockManager(new FlatWorldGenerator());
        
        int chunkX = 5;
        int chunkZ = 10;
        int worldX = 80; // In chunk 5
        int worldY = 64;
        int worldZ = 160; // In chunk 10
        
        // Record a block change
        manager.RecordBlockChange(worldX, worldY, worldZ, 9);
        
        // Act: Get chunk (this will apply diffs)
        var chunk = blockManager.GetOrCreateChunk(chunkX, chunkZ);
        
        // Assert
        int localX = worldX - (chunkX * 16); // 80 - 80 = 0
        int localZ = worldZ - (chunkZ * 16); // 160 - 160 = 0
        Assert.Equal(9, chunk.GetBlockStateId(localX, worldY, localZ));
    }

    [Fact]
    public void MultipleDiffsAppliedCorrectly()
    {
        // Arrange
        var manager = ChunkDiffManager.Instance;
        // Use unique chunk coordinates to avoid conflicts
        int chunkX = 996;
        int chunkZ = 996;
        
        // Clear any existing state for this chunk
        manager.ClearDiff(chunkX, chunkZ);
        
        // Calculate world coordinates in this chunk
        int worldX1 = chunkX * 16 + 9;  // Local: 9
        int worldX2 = chunkX * 16 + 10; // Local: 10
        int worldX3 = chunkX * 16 + 11; // Local: 11
        int worldY1 = 64;
        int worldY2 = 65;
        int worldY3 = 66;
        int worldZ1 = chunkZ * 16 + 3;  // Local: 3
        int worldZ2 = chunkZ * 16 + 4;  // Local: 4
        int worldZ3 = chunkZ * 16 + 5;  // Local: 5
        
        // Record multiple block changes in the same chunk
        manager.RecordBlockChange(worldX1, worldY1, worldZ1, 9);   // Local: (9, 64, 3)
        manager.RecordBlockChange(worldX2, worldY2, worldZ2, 10);  // Local: (10, 65, 4)
        manager.RecordBlockChange(worldX3, worldY3, worldZ3, 11);  // Local: (11, 66, 5)
        
        // Act
        var blockManager = new BlockManager(new FlatWorldGenerator());
        var chunk = blockManager.GetOrCreateChunk(chunkX, chunkZ);
        
        // Assert
        Assert.Equal(9, chunk.GetBlockStateId(9, 64, 3));
        Assert.Equal(10, chunk.GetBlockStateId(10, 65, 4));
        Assert.Equal(11, chunk.GetBlockStateId(11, 66, 5));
    }

    [Fact]
    public void DiffAppliedAfterGeneration()
    {
        // Arrange
        var manager = ChunkDiffManager.Instance;
        var generator = new FlatWorldGenerator();
        var blockManager = new BlockManager(generator);
        
        int chunkX = 0;
        int chunkZ = 0;
        int worldX = 5; // In chunk 0
        int worldY = 64; // Grass layer in flat world
        int worldZ = 5; // In chunk 0
        
        // Record a block change at the grass layer (should override generated block)
        manager.RecordBlockChange(worldX, worldY, worldZ, 9); // Change to block 9
        
        // Act
        var chunk = blockManager.GetOrCreateChunk(chunkX, chunkZ);
        
        // Assert
        // The diff should override the generated grass block
        Assert.Equal(9, chunk.GetBlockStateId(worldX, worldY, worldZ));
        
        // Other blocks at y=64 should still have generated grass (if no diff)
        Assert.NotEqual(9, chunk.GetBlockStateId(0, 64, 0)); // Should be generated grass (2098)
    }

    [Fact]
    public void DiffPersistsWhenChunkIsRecreated()
    {
        // Arrange
        var manager = ChunkDiffManager.Instance;
        var blockManager = new BlockManager(new FlatWorldGenerator());
        
        int chunkX = 3;
        int chunkZ = 4;
        int worldX = 50; // In chunk 3
        int worldY = 64;
        int worldZ = 70; // In chunk 4
        
        // Record a block change
        manager.RecordBlockChange(worldX, worldY, worldZ, 9);
        
        // Act: Get chunk, then remove it from BlockManager's cache, then get it again
        var chunk1 = blockManager.GetOrCreateChunk(chunkX, chunkZ);
        int localX = worldX - (chunkX * 16); // 50 - 48 = 2
        int localZ = worldZ - (chunkZ * 16); // 70 - 64 = 6
        
        // Verify diff was applied
        Assert.Equal(9, chunk1.GetBlockStateId(localX, worldY, localZ));
        
        // Note: We can't easily remove from BlockManager's ConcurrentDictionary in tests,
        // but we can verify that the diff persists by creating a new BlockManager instance
        var blockManager2 = new BlockManager(new FlatWorldGenerator());
        var chunk2 = blockManager2.GetOrCreateChunk(chunkX, chunkZ);
        
        // Assert: Diff should still be applied (since it's in the singleton manager)
        Assert.Equal(9, chunk2.GetBlockStateId(localX, worldY, localZ));
    }

    [Fact]
    public void BlockChangeOverridesGeneratedBlock()
    {
        // Arrange
        var manager = ChunkDiffManager.Instance;
        var blockManager = new BlockManager(new FlatWorldGenerator());
        
        int chunkX = 0;
        int chunkZ = 0;
        
        // In flat world, block at (0, 64, 0) should be grass (block state ID 2098)
        // Change it to air (0)
        manager.RecordBlockChange(0, 64, 0, 0);
        
        // Act
        var chunk = blockManager.GetOrCreateChunk(chunkX, chunkZ);
        
        // Assert
        Assert.Equal(0, chunk.GetBlockStateId(0, 64, 0)); // Should be air, not grass
    }

    [Fact]
    public void MultipleBlockChangesInSamePosition()
    {
        // Arrange
        var manager = ChunkDiffManager.Instance;
        // Use unique chunk coordinates to avoid conflicts with other tests
        int chunkX = 997;
        int chunkZ = 997;
        int worldX = chunkX * 16 + 9; // World X in chunk 997
        int worldY = 64;
        int worldZ = chunkZ * 16 + 3; // World Z in chunk 997
        
        // Clear any existing state for this chunk
        manager.ClearDiff(chunkX, chunkZ);
        
        // Record multiple changes to same position (last one wins)
        // Must record BEFORE getting chunk, since GetOrCreateChunk caches chunks
        manager.RecordBlockChange(worldX, worldY, worldZ, 9);
        manager.RecordBlockChange(worldX, worldY, worldZ, 10);
        manager.RecordBlockChange(worldX, worldY, worldZ, 11);
        
        // Act: Create block manager and get chunk (diff should be applied)
        var blockManager = new BlockManager(new FlatWorldGenerator());
        var chunk = blockManager.GetOrCreateChunk(chunkX, chunkZ);
        
        // Assert
        int localX = worldX - (chunkX * 16); // 9
        int localZ = worldZ - (chunkZ * 16); // 3
        Assert.Equal(11, chunk.GetBlockStateId(localX, worldY, localZ)); // Last change wins
    }

    [Fact]
    public void DiffWorksAcrossDifferentChunks()
    {
        // Arrange
        var manager = ChunkDiffManager.Instance;
        var blockManager = new BlockManager(new FlatWorldGenerator());
        
        // Changes in different chunks
        manager.RecordBlockChange(25, 64, 35, 9);   // Chunk (1, 2)
        manager.RecordBlockChange(50, 64, 70, 10);  // Chunk (3, 4)
        manager.RecordBlockChange(80, 64, 160, 11); // Chunk (5, 10)
        
        // Act
        var chunk1 = blockManager.GetOrCreateChunk(1, 2);
        var chunk2 = blockManager.GetOrCreateChunk(3, 4);
        var chunk3 = blockManager.GetOrCreateChunk(5, 10);
        
        // Assert
        Assert.Equal(9, chunk1.GetBlockStateId(9, 64, 3));
        Assert.Equal(10, chunk2.GetBlockStateId(2, 64, 6));
        Assert.Equal(11, chunk3.GetBlockStateId(0, 64, 0));
    }
}



