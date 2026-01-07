namespace MineSharp.Network.ChunkLoading;

/// <summary>
/// Calculates priority for chunk load requests.
/// Higher priority = loaded first.
/// </summary>
public static class ChunkLoadPriorityCalculator
{
    /// <summary>
    /// Calculates priority based on distance, stability, age, and retry count.
    /// Higher priority = loaded first.
    /// </summary>
    public static int CalculatePriority(
        int chunkX,
        int chunkZ,
        int playerChunkX,
        int playerChunkZ,
        DateTime createdAt,
        int retryCount,
        bool isStable = true)
    {
        // Base priority starts high (we'll subtract from it)
        const int basePriority = 1000000;
        
        // Distance priority: closer chunks get higher priority
        // Distance 0 = 0 penalty, distance 10 = 1000 penalty
        int distance = CalculateDistance(chunkX, chunkZ, playerChunkX, playerChunkZ);
        int distancePenalty = distance * 100;
        
        // Age boost: chunks that have been desired longer get slight boost
        // But with diminishing returns (logarithmic)
        int ageBoost = CalculateAgeBoost(createdAt);
        
        // Retry penalty: chunks that have failed multiple times get lower priority
        // Each retry adds 500 penalty
        int retryPenalty = retryCount * 500;
        
        // Stability bonus: stable chunks (not recently cancelled) get bonus
        int stabilityBonus = isStable ? 50 : 0;
        
        // Calculate final priority (higher is better)
        int priority = basePriority - distancePenalty - retryPenalty + ageBoost + stabilityBonus;
        
        return priority;
    }

    /// <summary>
    /// Calculates Manhattan distance between two chunk coordinates.
    /// </summary>
    private static int CalculateDistance(int x1, int z1, int x2, int z2)
    {
        return Math.Abs(x1 - x2) + Math.Abs(z1 - z2);
    }

    /// <summary>
    /// Calculates age-based priority boost (diminishing returns).
    /// Chunks desired for longer get slight boost, but it caps out.
    /// </summary>
    private static int CalculateAgeBoost(DateTime createdAt)
    {
        var age = DateTime.UtcNow - createdAt;
        var ageSeconds = (int)age.TotalSeconds;
        
        // Logarithmic boost: log(age + 1) * 10, capped at 100
        // This gives diminishing returns - being desired for 1 second gives ~7 boost,
        // 10 seconds gives ~24 boost, 100 seconds gives ~46 boost, caps at 100
        if (ageSeconds <= 0) return 0;
        
        double logBoost = Math.Log(ageSeconds + 1) * 10;
        int boost = (int)Math.Min(logBoost, 100);
        
        return boost;
    }
}

