using MineSharp.Data;

namespace MineSharp.Game;

/// <summary>
/// Calculates block breaking times based on tool, block hardness, and player state.
/// Uses the formula from Minecraft's Breaking documentation.
/// </summary>
public static class BlockBreakingCalculator
{
    /// <summary>
    /// Calculates the number of ticks needed to break a block with a given tool.
    /// Returns int.MaxValue for unbreakable blocks, 0 for instant breaking.
    /// </summary>
    /// <param name="blockName">Block identifier (e.g., "minecraft:stone")</param>
    /// <param name="toolName">Tool identifier (e.g., "minecraft:iron_pickaxe", or "hand" for no tool)</param>
    /// <param name="registryManager">Registry manager for accessing block hardness and tool speeds</param>
    /// <returns>Number of ticks needed to break the block, or int.MaxValue for unbreakable</returns>
    public static int CalculateBreakTicks(string blockName, string toolName, RegistryManager registryManager)
    {
        // Check if block is unbreakable
        if (registryManager.IsBlockUnbreakable(blockName))
        {
            return int.MaxValue;
        }

        // Get block hardness
        var hardness = registryManager.GetBlockHardness(blockName);
        if (!hardness.HasValue || hardness.Value <= 0)
        {
            // Unknown block or invalid hardness - assume default (e.g., air = 0)
            if (hardness == 0)
            {
                return 0; // Instant break for air
            }
            return 20; // Default to 1 second for unknown blocks
        }

        // Get tool speed for this specific block (defaults to 1.0 for hand)
        // This handles block-specific speeds (e.g., shears on wool = 5.0)
        double toolSpeed = registryManager.GetToolSpeedForBlock(toolName, blockName);

        // Check if tool can harvest the block
        bool canHarvest = registryManager.CanToolMineBlock(toolName, blockName);

        // Calculate damage per tick
        // Formula: damage = speedMultiplier / blockHardness
        // If can harvest: damage /= 30, else: damage /= 100
        double damage = toolSpeed / hardness.Value;
        if (canHarvest)
        {
            damage /= 30;
        }
        else
        {
            damage /= 100;
        }

        // If damage >= 1, block breaks instantly
        if (damage >= 1.0)
        {
            return 0; // Instant break
        }

        // Calculate ticks: ceil(1 / damage)
        // Minimum 1 tick (can't break in 0 ticks if damage < 1)
        int ticks = (int)Math.Ceiling(1.0 / damage);
        return Math.Max(1, ticks); // Ensure at least 1 tick
    }

    /// <summary>
    /// Checks if a tool can harvest a block (affects break time calculation).
    /// Returns true if the tool is the correct type for the block.
    /// </summary>
    /// <param name="blockName">Block identifier</param>
    /// <param name="toolName">Tool identifier</param>
    /// <param name="registryManager">Registry manager for accessing mineable tags</param>
    /// <returns>True if the tool can harvest the block efficiently</returns>
    public static bool CanToolHarvestBlock(string blockName, string toolName, RegistryManager registryManager)
    {
        return registryManager.CanToolMineBlock(toolName, blockName);
    }

    /// <summary>
    /// Gets the tool speed multiplier for a given tool and block.
    /// Returns 1.0 for hand/no tool.
    /// Uses block-specific speeds when available (e.g., shears on wool).
    /// </summary>
    /// <param name="toolName">Tool identifier (e.g., "minecraft:iron_pickaxe")</param>
    /// <param name="blockName">Block identifier (e.g., "minecraft:wool")</param>
    /// <param name="registryManager">Registry manager for accessing tool speeds</param>
    /// <returns>Speed multiplier</returns>
    public static double GetToolSpeed(string toolName, string blockName, RegistryManager registryManager)
    {
        return registryManager.GetToolSpeedForBlock(toolName, blockName);
    }
}

