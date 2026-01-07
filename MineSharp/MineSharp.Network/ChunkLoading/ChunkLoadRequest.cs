namespace MineSharp.Network.ChunkLoading;

/// <summary>
/// Represents a request to load a chunk.
/// Immutable once created - state changes create new instances.
/// </summary>
public class ChunkLoadRequest
{
    public int ChunkX { get; }
    public int ChunkZ { get; }
    public ChunkLoadState State { get; }
    public int Priority { get; }
    public DateTime CreatedAt { get; }
    public DateTime? StartedAt { get; }
    public int RetryCount { get; }
    public DateTime? LastRetryAt { get; }
    public string? ErrorMessage { get; }

    public ChunkLoadRequest(
        int chunkX,
        int chunkZ,
        ChunkLoadState state,
        int priority,
        DateTime createdAt,
        DateTime? startedAt = null,
        int retryCount = 0,
        DateTime? lastRetryAt = null,
        string? errorMessage = null)
    {
        ChunkX = chunkX;
        ChunkZ = chunkZ;
        State = state;
        Priority = priority;
        CreatedAt = createdAt;
        StartedAt = startedAt;
        RetryCount = retryCount;
        LastRetryAt = lastRetryAt;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Creates a new request with updated state.
    /// </summary>
    public ChunkLoadRequest WithState(ChunkLoadState newState)
    {
        return new ChunkLoadRequest(
            ChunkX,
            ChunkZ,
            newState,
            Priority,
            CreatedAt,
            StartedAt,
            RetryCount,
            LastRetryAt,
            ErrorMessage);
    }

    /// <summary>
    /// Creates a new request with updated priority.
    /// </summary>
    public ChunkLoadRequest WithPriority(int newPriority)
    {
        return new ChunkLoadRequest(
            ChunkX,
            ChunkZ,
            State,
            newPriority,
            CreatedAt,
            StartedAt,
            RetryCount,
            LastRetryAt,
            ErrorMessage);
    }

    /// <summary>
    /// Creates a new request marking it as started.
    /// </summary>
    public ChunkLoadRequest MarkStarted()
    {
        return new ChunkLoadRequest(
            ChunkX,
            ChunkZ,
            ChunkLoadState.Loading,
            Priority,
            CreatedAt,
            DateTime.UtcNow,
            RetryCount,
            LastRetryAt,
            ErrorMessage);
    }

    /// <summary>
    /// Creates a new request marking it as failed with error message.
    /// </summary>
    public ChunkLoadRequest MarkFailed(string errorMessage)
    {
        return new ChunkLoadRequest(
            ChunkX,
            ChunkZ,
            ChunkLoadState.Failed,
            Priority,
            CreatedAt,
            StartedAt,
            RetryCount,
            LastRetryAt,
            errorMessage);
    }

    /// <summary>
    /// Creates a new request for retry (increments retry count).
    /// </summary>
    public ChunkLoadRequest MarkForRetry()
    {
        return new ChunkLoadRequest(
            ChunkX,
            ChunkZ,
            ChunkLoadState.Retrying,
            Priority,
            CreatedAt,
            StartedAt,
            RetryCount + 1,
            DateTime.UtcNow,
            ErrorMessage);
    }
}

/// <summary>
/// Represents the state of a chunk load request.
/// </summary>
public enum ChunkLoadState
{
    Pending,    // In desired set, not started
    Queued,     // Ready to load, waiting for worker slot
    Loading,    // Actively being sent
    Loaded,     // Successfully sent
    Cancelled,  // Removed from desired set
    Failed,     // Error occurred
    Retrying    // Failed, will retry after cooldown
}

