using MineSharp.World;
using Xunit;

namespace MineSharp.Tests.World;

public class TimeManagerTests
{
    [Fact]
    public void Constructor_WithDefaultValues_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var timeManager = new TimeManager();
        
        // Assert
        Assert.Equal(0, timeManager.WorldAge);
        Assert.Equal(6000, timeManager.TimeOfDay); // Default noon
        Assert.True(timeManager.TimeIncreasing);
    }

    [Fact]
    public void Constructor_WithCustomValues_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var timeManager = new TimeManager(initialTimeOfDay: 12000, timeIncreasing: false);
        
        // Assert
        Assert.Equal(0, timeManager.WorldAge);
        Assert.Equal(12000, timeManager.TimeOfDay);
        Assert.False(timeManager.TimeIncreasing);
    }

    [Fact]
    public void Tick_WhenTimeIncreasing_ShouldIncrementWorldAge()
    {
        // Arrange
        var timeManager = new TimeManager(initialTimeOfDay: 6000, timeIncreasing: true);
        var initialWorldAge = timeManager.WorldAge;
        
        // Act
        timeManager.Tick();
        
        // Assert
        Assert.Equal(initialWorldAge + 1, timeManager.WorldAge);
    }

    [Fact]
    public void Tick_WhenTimeIncreasing_ShouldIncrementTimeOfDay()
    {
        // Arrange
        var timeManager = new TimeManager(initialTimeOfDay: 6000, timeIncreasing: true);
        var initialTimeOfDay = timeManager.TimeOfDay;
        
        // Act
        timeManager.Tick();
        
        // Assert
        Assert.Equal(initialTimeOfDay + 1, timeManager.TimeOfDay);
    }

    [Fact]
    public void Tick_WhenTimeNotIncreasing_ShouldNotIncrementTimeOfDay()
    {
        // Arrange
        var timeManager = new TimeManager(initialTimeOfDay: 6000, timeIncreasing: false);
        var initialTimeOfDay = timeManager.TimeOfDay;
        
        // Act
        timeManager.Tick();
        
        // Assert
        Assert.Equal(initialTimeOfDay, timeManager.TimeOfDay);
        // WorldAge should still increment even when time is paused
        Assert.Equal(1, timeManager.WorldAge);
    }

    [Fact]
    public void Tick_WhenTimeOfDayReaches24000_ShouldWrapToZero()
    {
        // Arrange
        var timeManager = new TimeManager(initialTimeOfDay: 23999, timeIncreasing: true);
        
        // Act
        timeManager.Tick();
        
        // Assert
        Assert.Equal(0, timeManager.TimeOfDay);
    }

    [Fact]
    public void Tick_WhenTimeOfDayIs24000_ShouldWrapToZero()
    {
        // Arrange
        var timeManager = new TimeManager(initialTimeOfDay: 24000, timeIncreasing: true);
        
        // Act
        timeManager.Tick();
        
        // Assert
        Assert.Equal(0, timeManager.TimeOfDay);
    }

    [Fact]
    public void SetTimeOfDay_WithValidValue_ShouldUpdateTimeOfDay()
    {
        // Arrange
        var timeManager = new TimeManager(initialTimeOfDay: 6000);
        
        // Act
        timeManager.SetTimeOfDay(12000);
        
        // Assert
        Assert.Equal(12000, timeManager.TimeOfDay);
    }

    [Fact]
    public void SetTimeOfDay_WithValueBelowZero_ShouldClampToZero()
    {
        // Arrange
        var timeManager = new TimeManager(initialTimeOfDay: 6000);
        
        // Act
        timeManager.SetTimeOfDay(-100);
        
        // Assert
        Assert.Equal(0, timeManager.TimeOfDay);
    }

    [Fact]
    public void SetTimeOfDay_WithValueAbove24000_ShouldClampTo24000()
    {
        // Arrange
        var timeManager = new TimeManager(initialTimeOfDay: 6000);
        
        // Act
        timeManager.SetTimeOfDay(25000);
        
        // Assert
        Assert.Equal(24000, timeManager.TimeOfDay);
    }

    [Fact]
    public void SetTimeIncreasing_WithTrue_ShouldUpdateTimeIncreasing()
    {
        // Arrange
        var timeManager = new TimeManager(initialTimeOfDay: 6000, timeIncreasing: false);
        
        // Act
        timeManager.SetTimeIncreasing(true);
        
        // Assert
        Assert.True(timeManager.TimeIncreasing);
    }

    [Fact]
    public void SetTimeIncreasing_WithFalse_ShouldUpdateTimeIncreasing()
    {
        // Arrange
        var timeManager = new TimeManager(initialTimeOfDay: 6000, timeIncreasing: true);
        
        // Act
        timeManager.SetTimeIncreasing(false);
        
        // Assert
        Assert.False(timeManager.TimeIncreasing);
    }

    [Fact]
    public void Tick_MultipleTicks_ShouldIncrementCorrectly()
    {
        // Arrange
        var timeManager = new TimeManager(initialTimeOfDay: 0, timeIncreasing: true);
        
        // Act
        for (int i = 0; i < 100; i++)
        {
            timeManager.Tick();
        }
        
        // Assert
        Assert.Equal(100, timeManager.WorldAge);
        Assert.Equal(100, timeManager.TimeOfDay);
    }

    [Fact]
    public void Tick_WhenTimeWrapsMultipleTimes_ShouldHandleCorrectly()
    {
        // Arrange
        var timeManager = new TimeManager(initialTimeOfDay: 23950, timeIncreasing: true);
        
        // Act - Tick 100 times, which should wrap once and then continue
        for (int i = 0; i < 100; i++)
        {
            timeManager.Tick();
        }
        
        // Assert
        // WorldAge should be 100
        Assert.Equal(100, timeManager.WorldAge);
        // TimeOfDay calculation:
        // - Start at 23950
        // - After 50 ticks: 23950 + 50 = 24000 (wraps to 0 on the 50th tick when it reaches 24000)
        // - After 100 ticks total: 0 + (100 - 50) = 50
        Assert.Equal(50, timeManager.TimeOfDay);
    }

    [Fact]
    public void Tick_WhenTimeNotIncreasing_ShouldStillIncrementWorldAge()
    {
        // Arrange
        var timeManager = new TimeManager(initialTimeOfDay: 6000, timeIncreasing: false);
        
        // Act
        timeManager.Tick();
        timeManager.Tick();
        timeManager.Tick();
        
        // Assert
        Assert.Equal(3, timeManager.WorldAge);
        Assert.Equal(6000, timeManager.TimeOfDay); // Should not change
    }
}

