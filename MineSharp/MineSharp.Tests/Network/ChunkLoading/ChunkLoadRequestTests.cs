using System;
using MineSharp.Network.ChunkLoading;
using Xunit;

namespace MineSharp.Tests.Network.ChunkLoading;

public class ChunkLoadRequestTests
{
    [Fact]
    public void ChunkLoadRequest_WithState_ReturnsNewInstanceWithUpdatedState()
    {
        // Arrange
        var original = new ChunkLoadRequest(0, 0, ChunkLoadState.Pending, 100, DateTime.UtcNow);

        // Act
        var updated = original.WithState(ChunkLoadState.Queued);

        // Assert
        Assert.NotSame(original, updated);
        Assert.Equal(ChunkLoadState.Pending, original.State);
        Assert.Equal(ChunkLoadState.Queued, updated.State);
        Assert.Equal(original.ChunkX, updated.ChunkX);
        Assert.Equal(original.ChunkZ, updated.ChunkZ);
    }

    [Fact]
    public void ChunkLoadRequest_WithPriority_ReturnsNewInstanceWithUpdatedPriority()
    {
        // Arrange
        var original = new ChunkLoadRequest(0, 0, ChunkLoadState.Pending, 100, DateTime.UtcNow);

        // Act
        var updated = original.WithPriority(200);

        // Assert
        Assert.NotSame(original, updated);
        Assert.Equal(100, original.Priority);
        Assert.Equal(200, updated.Priority);
        Assert.Equal(original.State, updated.State);
    }

    [Fact]
    public void ChunkLoadRequest_MarkStarted_SetsStateToLoadingAndStartedAt()
    {
        // Arrange
        var original = new ChunkLoadRequest(0, 0, ChunkLoadState.Queued, 100, DateTime.UtcNow);

        // Act
        var started = original.MarkStarted();

        // Assert
        Assert.Equal(ChunkLoadState.Loading, started.State);
        Assert.NotNull(started.StartedAt);
        Assert.True(started.StartedAt!.Value <= DateTime.UtcNow);
        Assert.Null(original.StartedAt);
    }

    [Fact]
    public void ChunkLoadRequest_MarkFailed_SetsStateToFailedAndErrorMessage()
    {
        // Arrange
        var original = new ChunkLoadRequest(0, 0, ChunkLoadState.Loading, 100, DateTime.UtcNow);

        // Act
        var failed = original.MarkFailed("Network error");

        // Assert
        Assert.Equal(ChunkLoadState.Failed, failed.State);
        Assert.Equal("Network error", failed.ErrorMessage);
        Assert.Equal(ChunkLoadState.Loading, original.State);
    }

    [Fact]
    public void ChunkLoadRequest_MarkForRetry_IncrementsRetryCountAndSetsStateToRetrying()
    {
        // Arrange
        var original = new ChunkLoadRequest(0, 0, ChunkLoadState.Failed, 100, DateTime.UtcNow, null, 2);

        // Act
        var retry = original.MarkForRetry();

        // Assert
        Assert.Equal(ChunkLoadState.Retrying, retry.State);
        Assert.Equal(2, original.RetryCount);
        Assert.Equal(3, retry.RetryCount);
        Assert.NotNull(retry.LastRetryAt);
        Assert.True(retry.LastRetryAt!.Value <= DateTime.UtcNow);
    }

    [Fact]
    public void ChunkLoadRequest_Immutable_OriginalUnchangedAfterModification()
    {
        // Arrange
        var original = new ChunkLoadRequest(5, 10, ChunkLoadState.Pending, 100, DateTime.UtcNow);

        // Act
        var updated1 = original.WithState(ChunkLoadState.Queued);
        var updated2 = updated1.WithPriority(200);
        var updated3 = updated2.MarkStarted();

        // Assert
        Assert.Equal(ChunkLoadState.Pending, original.State);
        Assert.Equal(100, original.Priority);
        Assert.Null(original.StartedAt);
        
        Assert.Equal(ChunkLoadState.Queued, updated1.State);
        Assert.Equal(ChunkLoadState.Loading, updated3.State);
        Assert.Equal(200, updated3.Priority);
    }
}

