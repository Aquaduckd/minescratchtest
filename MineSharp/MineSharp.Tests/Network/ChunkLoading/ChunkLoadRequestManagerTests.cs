using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MineSharp.Network.ChunkLoading;
using Xunit;

namespace MineSharp.Tests.Network.ChunkLoading;

public class ChunkLoadRequestManagerTests
{
    [Fact]
    public void UpdateDesiredChunks_CreatesRequestsForNewChunks()
    {
        // Arrange
        var manager = new ChunkLoadRequestManager(debounceMs: 0);
        var desiredChunks = new HashSet<(int X, int Z)> { (0, 0), (1, 0) };

        // Act
        manager.UpdateDesiredChunks(desiredChunks);
        manager.ProcessPendingUpdates(0, 0);

        // Assert
        var request0 = manager.GetRequest(0, 0);
        var request1 = manager.GetRequest(1, 0);
        
        Assert.NotNull(request0);
        Assert.NotNull(request1);
        Assert.Equal(ChunkLoadState.Pending, request0!.State);
        Assert.Equal(ChunkLoadState.Pending, request1!.State);
    }

    [Fact]
    public void UpdateDesiredChunks_CancelsRequestsForRemovedChunks()
    {
        // Arrange
        var manager = new ChunkLoadRequestManager(debounceMs: 0);
        var initialChunks = new HashSet<(int X, int Z)> { (0, 0), (1, 0) };
        manager.UpdateDesiredChunks(initialChunks);
        manager.ProcessPendingUpdates(0, 0);

        // Act
        var newChunks = new HashSet<(int X, int Z)> { (0, 0) }; // Removed (1, 0)
        manager.UpdateDesiredChunks(newChunks);
        manager.ProcessPendingUpdates(0, 0);

        // Assert
        var request0 = manager.GetRequest(0, 0);
        var request1 = manager.GetRequest(1, 0);
        
        Assert.NotNull(request0);
        Assert.Equal(ChunkLoadState.Pending, request0!.State); // Still desired
        
        Assert.NotNull(request1);
        Assert.Equal(ChunkLoadState.Cancelled, request1!.State); // Cancelled
    }

    [Fact]
    public void ProcessPendingUpdates_DebouncesUpdates()
    {
        // Arrange
        var manager = new ChunkLoadRequestManager(debounceMs: 100);
        var chunks1 = new HashSet<(int X, int Z)> { (0, 0) };
        var chunks2 = new HashSet<(int X, int Z)> { (1, 0) };

        // Act
        manager.UpdateDesiredChunks(chunks1);
        bool processed1 = manager.ProcessPendingUpdates(0, 0);
        
        manager.UpdateDesiredChunks(chunks2);
        bool processed2 = manager.ProcessPendingUpdates(0, 0); // Too soon

        // Assert
        Assert.True(processed1, "First update should process immediately (no debounce needed)");
        Assert.False(processed2, "Second update should be debounced");
        
        // Wait for debounce period
        Thread.Sleep(150);
        bool processed3 = manager.ProcessPendingUpdates(0, 0);
        Assert.True(processed3, "Update should process after debounce period");
        
        var request1 = manager.GetRequest(1, 0);
        Assert.NotNull(request1);
    }

    [Fact]
    public void GetQueuedRequests_ReturnsOnlyQueuedOrPendingRequests()
    {
        // Arrange
        var manager = new ChunkLoadRequestManager(debounceMs: 0);
        var desiredChunks = new HashSet<(int X, int Z)> { (0, 0), (1, 0), (2, 0) };
        manager.UpdateDesiredChunks(desiredChunks);
        manager.ProcessPendingUpdates(0, 0);

        // Mark one as loaded, one as failed
        var request0 = manager.GetRequest(0, 0)!;
        manager.UpdateRequest(request0.WithState(ChunkLoadState.Loaded));
        
        var request1 = manager.GetRequest(1, 0)!;
        manager.UpdateRequest(request1.WithState(ChunkLoadState.Failed));

        // Act
        var queued = manager.GetQueuedRequests().ToList();

        // Assert
        Assert.Single(queued);
        Assert.Equal((2, 0), (queued[0].ChunkX, queued[0].ChunkZ));
        Assert.Equal(ChunkLoadState.Pending, queued[0].State);
    }

    [Fact]
    public void GetQueuedRequests_ReturnsRequestsSortedByPriority()
    {
        // Arrange
        var manager = new ChunkLoadRequestManager(debounceMs: 0);
        var desiredChunks = new HashSet<(int X, int Z)> { (0, 0), (10, 10) }; // (0,0) is closer
        manager.UpdateDesiredChunks(desiredChunks);
        manager.ProcessPendingUpdates(0, 0);

        // Act
        var queued = manager.GetQueuedRequests().ToList();

        // Assert
        Assert.Equal(2, queued.Count);
        // Closer chunk should have higher priority (sorted descending)
        Assert.True(queued[0].Priority > queued[1].Priority);
        Assert.Equal((0, 0), (queued[0].ChunkX, queued[0].ChunkZ));
    }

    [Fact]
    public void UpdateRequest_UpdatesExistingRequest()
    {
        // Arrange
        var manager = new ChunkLoadRequestManager(debounceMs: 0);
        var desiredChunks = new HashSet<(int X, int Z)> { (0, 0) };
        manager.UpdateDesiredChunks(desiredChunks);
        manager.ProcessPendingUpdates(0, 0);
        
        var original = manager.GetRequest(0, 0)!;

        // Act
        var updated = original.MarkStarted();
        manager.UpdateRequest(updated);

        // Assert
        var retrieved = manager.GetRequest(0, 0);
        Assert.NotNull(retrieved);
        Assert.Equal(ChunkLoadState.Loading, retrieved!.State);
        Assert.NotNull(retrieved.StartedAt);
    }

    [Fact]
    public void GetDesiredChunks_ReturnsThreadSafeCopy()
    {
        // Arrange
        var manager = new ChunkLoadRequestManager(debounceMs: 0);
        var desiredChunks = new HashSet<(int X, int Z)> { (0, 0), (1, 0) };
        manager.UpdateDesiredChunks(desiredChunks);
        manager.ProcessPendingUpdates(0, 0);

        // Act
        var copy1 = manager.GetDesiredChunks();
        var copy2 = manager.GetDesiredChunks();

        // Assert
        Assert.NotSame(copy1, copy2);
        Assert.Equal(2, copy1.Count);
        Assert.Equal(2, copy2.Count);
        Assert.Contains((0, 0), copy1);
        Assert.Contains((1, 0), copy1);
    }

    [Fact]
    public void ProcessPendingUpdates_ReactivatesCancelledChunks()
    {
        // Arrange
        var manager = new ChunkLoadRequestManager(debounceMs: 0);
        var initialChunks = new HashSet<(int X, int Z)> { (0, 0) };
        manager.UpdateDesiredChunks(initialChunks);
        manager.ProcessPendingUpdates(0, 0);
        
        // Cancel it
        var emptyChunks = new HashSet<(int X, int Z)>();
        manager.UpdateDesiredChunks(emptyChunks);
        manager.ProcessPendingUpdates(0, 0);
        
        var cancelled = manager.GetRequest(0, 0);
        Assert.Equal(ChunkLoadState.Cancelled, cancelled!.State);

        // Act - Re-add the chunk
        manager.UpdateDesiredChunks(initialChunks);
        manager.ProcessPendingUpdates(0, 0);

        // Assert
        var reactivated = manager.GetRequest(0, 0);
        Assert.NotNull(reactivated);
        Assert.Equal(ChunkLoadState.Pending, reactivated!.State);
    }
}

