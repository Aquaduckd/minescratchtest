namespace MineSharp.World.ChunkDiffs;

/// <summary>
/// Represents a single block modification within a chunk.
/// </summary>
public class BlockChange
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public int BlockStateId { get; set; }

    public BlockChange(int x, int y, int z, int blockStateId)
    {
        X = x;
        Y = y;
        Z = z;
        BlockStateId = blockStateId;
    }
}

