using System;
using System.IO;
using MineSharp.Data;
using MineSharp.Game;
using Xunit;

namespace MineSharp.Tests.Game;

public class BlockBreakingCalculatorTests
{
    private RegistryManager CreateRegistryManagerWithData()
    {
        var registryManager = new RegistryManager();
        // Find extracted_data directory relative to project root
        // Tests run from bin/Debug/net6.0, so we need to go up to project root
        var testDir = AppContext.BaseDirectory; // bin/Debug/net6.0
        var projectRoot = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", ".."));
        var dataPath = Path.Combine(projectRoot, "extracted_data");
        
        if (Directory.Exists(dataPath) && File.Exists(Path.Combine(dataPath, "block_hardness.json")))
        {
            registryManager.LoadRegistries(dataPath);
        }
        else
        {
            // Try alternative: from current directory (if running from project root)
            var altPath = Path.GetFullPath("extracted_data");
            if (Directory.Exists(altPath) && File.Exists(Path.Combine(altPath, "block_hardness.json")))
            {
                registryManager.LoadRegistries(altPath);
            }
        }

        return registryManager;
    }

    [Fact]
    public void CalculateBreakTicks_WithIronPickaxeAndStone_ReturnsCorrectTicks()
    {
        // Arrange
        var registryManager = CreateRegistryManagerWithData();
        string blockName = "minecraft:stone"; // Hardness = 1.5
        string toolName = "minecraft:iron_pickaxe"; // Speed = 6.0

        // Act
        int ticks = BlockBreakingCalculator.CalculateBreakTicks(blockName, toolName, registryManager);

        // Assert
        // Expected: damage = 6.0 / 1.5 = 4.0, damage /= 30 (can harvest) = 0.133...
        // ticks = ceil(1 / 0.133) = ceil(7.5) = 8
        Assert.Equal(8, ticks);
    }

    [Fact]
    public void CalculateBreakTicks_WithHandAndDirt_ReturnsCorrectTicks()
    {
        // Arrange
        var registryManager = CreateRegistryManagerWithData();
        string blockName = "minecraft:dirt"; // Hardness = 0.5
        string toolName = "hand"; // Speed = 1.0

        // Act
        int ticks = BlockBreakingCalculator.CalculateBreakTicks(blockName, toolName, registryManager);

        // Assert
        // Expected: damage = 1.0 / 0.5 = 2.0, damage /= 30 (can harvest) = 0.066...
        // ticks = ceil(1 / 0.066) = ceil(15) = 15
        Assert.Equal(15, ticks);
    }

    [Fact]
    public void CalculateBreakTicks_WithHandAndStone_ReturnsCorrectTicks()
    {
        // Arrange
        var registryManager = CreateRegistryManagerWithData();
        string blockName = "minecraft:stone"; // Hardness = 1.5
        string toolName = "hand"; // Speed = 1.0

        // Act
        int ticks = BlockBreakingCalculator.CalculateBreakTicks(blockName, toolName, registryManager);

        // Assert
        // Expected: damage = 1.0 / 1.5 = 0.666..., damage /= 30 (can harvest, hand can mine stone) = 0.022...
        // ticks = ceil(1 / 0.022) = ceil(45.45) = 46
        Assert.Equal(46, ticks);
    }

    [Fact]
    public void CalculateBreakTicks_WithBedrock_ReturnsMaxValue()
    {
        // Arrange
        var registryManager = CreateRegistryManagerWithData();
        string blockName = "minecraft:bedrock"; // Hardness = -1 (unbreakable)
        string toolName = "minecraft:diamond_pickaxe";

        // Act
        int ticks = BlockBreakingCalculator.CalculateBreakTicks(blockName, toolName, registryManager);

        // Assert
        Assert.Equal(int.MaxValue, ticks);
    }

    [Fact]
    public void CalculateBreakTicks_WithAir_ReturnsZero()
    {
        // Arrange
        var registryManager = CreateRegistryManagerWithData();
        string blockName = "minecraft:air"; // Hardness = 0.0
        string toolName = "hand";

        // Act
        int ticks = BlockBreakingCalculator.CalculateBreakTicks(blockName, toolName, registryManager);

        // Assert
        Assert.Equal(0, ticks); // Instant break for air
    }

    [Fact]
    public void CalculateBreakTicks_InstantBreak_ReturnsZero()
    {
        // Arrange
        var registryManager = CreateRegistryManagerWithData();
        // Very soft block with fast tool should break instantly
        string blockName = "minecraft:azalea"; // Hardness = 0.0
        string toolName = "hand"; // Speed = 1.0

        // Act
        int ticks = BlockBreakingCalculator.CalculateBreakTicks(blockName, toolName, registryManager);

        // Assert
        Assert.Equal(0, ticks); // Instant break
    }

    [Fact]
    public void CanToolHarvestBlock_WithPickaxeAndStone_ReturnsTrue()
    {
        // Arrange
        var registryManager = CreateRegistryManagerWithData();
        string blockName = "minecraft:stone";
        string toolName = "minecraft:iron_pickaxe";

        // Act
        bool canHarvest = BlockBreakingCalculator.CanToolHarvestBlock(blockName, toolName, registryManager);

        // Assert
        Assert.True(canHarvest);
    }

    [Fact]
    public void CanToolHarvestBlock_WithShovelAndStone_ReturnsFalse()
    {
        // Arrange
        var registryManager = CreateRegistryManagerWithData();
        string blockName = "minecraft:stone";
        string toolName = "minecraft:iron_shovel";

        // Act
        bool canHarvest = BlockBreakingCalculator.CanToolHarvestBlock(blockName, toolName, registryManager);

        // Assert
        Assert.False(canHarvest); // Shovel cannot harvest stone
    }

    [Fact]
    public void GetToolSpeed_WithIronPickaxe_Returns6()
    {
        // Arrange
        var registryManager = CreateRegistryManagerWithData();
        string toolName = "minecraft:iron_pickaxe";

        // Act
        double speed = BlockBreakingCalculator.GetToolSpeed(toolName, registryManager);

        // Assert
        Assert.Equal(6.0, speed);
    }

    [Fact]
    public void GetToolSpeed_WithDiamondPickaxe_Returns8()
    {
        // Arrange
        var registryManager = CreateRegistryManagerWithData();
        string toolName = "minecraft:diamond_pickaxe";

        // Act
        double speed = BlockBreakingCalculator.GetToolSpeed(toolName, registryManager);

        // Assert
        Assert.Equal(8.0, speed);
    }

    [Fact]
    public void GetToolSpeed_WithHand_Returns1()
    {
        // Arrange
        var registryManager = CreateRegistryManagerWithData();
        string toolName = "hand";

        // Act
        double speed = BlockBreakingCalculator.GetToolSpeed(toolName, registryManager);

        // Assert
        Assert.Equal(1.0, speed);
    }

    [Fact]
    public void GetToolSpeed_WithWoodenPickaxe_Returns2()
    {
        // Arrange
        var registryManager = CreateRegistryManagerWithData();
        string toolName = "minecraft:wooden_pickaxe";

        // Act
        double speed = BlockBreakingCalculator.GetToolSpeed(toolName, registryManager);

        // Assert
        Assert.Equal(2.0, speed);
    }

    [Fact]
    public void GetToolSpeed_WithGoldenPickaxe_Returns12()
    {
        // Arrange
        var registryManager = CreateRegistryManagerWithData();
        string toolName = "minecraft:golden_pickaxe";

        // Act
        double speed = BlockBreakingCalculator.GetToolSpeed(toolName, registryManager);

        // Assert
        Assert.Equal(12.0, speed);
    }
}

