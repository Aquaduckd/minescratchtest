namespace MineSharp.Network.Handlers;

/// <summary>
/// Represents an active block breaking session for a player.
/// Tracks the progress of breaking a block with animation stages.
/// </summary>
public class BlockBreakingSession
{
    public Guid PlayerUuid { get; }
    public int EntityId { get; }
    public int BlockX { get; }
    public int BlockY { get; }
    public int BlockZ { get; }
    public int TotalTicks { get; }
    public int CurrentTick { get; set; }
    public byte CurrentStage { get; set; }
    public CancellationTokenSource CancellationToken { get; }
    public DateTime StartTime { get; }
    public string BlockName { get; }
    public string ToolName { get; }
    public double ToolSpeed { get; }
    public double BlockHardness { get; }

    public BlockBreakingSession(
        Guid playerUuid,
        int entityId,
        int blockX,
        int blockY,
        int blockZ,
        int totalTicks,
        string blockName,
        string toolName,
        double toolSpeed,
        double blockHardness)
    {
        PlayerUuid = playerUuid;
        EntityId = entityId;
        BlockX = blockX;
        BlockY = blockY;
        BlockZ = blockZ;
        TotalTicks = totalTicks;
        CurrentTick = 0;
        CurrentStage = 0;
        CancellationToken = new CancellationTokenSource();
        StartTime = DateTime.UtcNow;
        BlockName = blockName;
        ToolName = toolName;
        ToolSpeed = toolSpeed;
        BlockHardness = blockHardness;
    }

    /// <summary>
    /// Checks if the breaking session is complete (current tick >= total ticks).
    /// </summary>
    public bool IsComplete => CurrentTick >= TotalTicks;

    /// <summary>
    /// Gets the progress as a value from 0.0 to 1.0.
    /// </summary>
    public double Progress => TotalTicks > 0 ? Math.Min(1.0, (double)CurrentTick / TotalTicks) : 1.0;
}

