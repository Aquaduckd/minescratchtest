using System;
using MineSharp.Game;
using Xunit;

namespace MineSharp.Tests.Game;

public class PlayerSneakingTests
{
    [Fact]
    public void Player_InitialSneakingState_IsFalse()
    {
        // Arrange & Act
        var player = new Player(Guid.NewGuid(), 1);

        // Assert
        Assert.False(player.IsSneaking);
    }

    [Fact]
    public void Player_UpdateSneakingState_ToTrue_ReturnsTrue()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), 1);
        Assert.False(player.IsSneaking);

        // Act
        bool changed = player.UpdateSneakingState(true);

        // Assert
        Assert.True(changed);
        Assert.True(player.IsSneaking);
    }

    [Fact]
    public void Player_UpdateSneakingState_ToFalse_WhenAlreadyFalse_ReturnsFalse()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), 1);
        Assert.False(player.IsSneaking);

        // Act
        bool changed = player.UpdateSneakingState(false);

        // Assert
        Assert.False(changed);
        Assert.False(player.IsSneaking);
    }

    [Fact]
    public void Player_UpdateSneakingState_ToFalse_WhenTrue_ReturnsTrue()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), 1);
        player.UpdateSneakingState(true);
        Assert.True(player.IsSneaking);

        // Act
        bool changed = player.UpdateSneakingState(false);

        // Assert
        Assert.True(changed);
        Assert.False(player.IsSneaking);
    }

    [Fact]
    public void Player_UpdateSneakingState_ToTrue_WhenAlreadyTrue_ReturnsFalse()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), 1);
        player.UpdateSneakingState(true);
        Assert.True(player.IsSneaking);

        // Act
        bool changed = player.UpdateSneakingState(true);

        // Assert
        Assert.False(changed);
        Assert.True(player.IsSneaking);
    }

    [Fact]
    public void Player_UpdateSneakingState_TogglesMultipleTimes()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), 1);

        // Act & Assert - Toggle multiple times
        Assert.True(player.UpdateSneakingState(true));
        Assert.True(player.IsSneaking);

        Assert.False(player.UpdateSneakingState(true)); // Already true
        Assert.True(player.IsSneaking);

        Assert.True(player.UpdateSneakingState(false));
        Assert.False(player.IsSneaking);

        Assert.False(player.UpdateSneakingState(false)); // Already false
        Assert.False(player.IsSneaking);

        Assert.True(player.UpdateSneakingState(true));
        Assert.True(player.IsSneaking);
    }
}

