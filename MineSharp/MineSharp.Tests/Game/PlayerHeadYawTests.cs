using System;
using MineSharp.Game;
using Xunit;

namespace MineSharp.Tests.Game;

public class PlayerHeadYawTests
{
    [Fact]
    public void Player_HeadYaw_InitializedToZero()
    {
        // Arrange
        var uuid = Guid.NewGuid();
        int entityId = 1;

        // Act
        var player = new Player(uuid, entityId);

        // Assert
        Assert.Equal(0.0f, player.HeadYaw);
    }

    [Fact]
    public void Player_UpdateRotation_UpdatesHeadYawToMatchBodyYaw()
    {
        // Arrange
        var uuid = Guid.NewGuid();
        int entityId = 1;
        var player = new Player(uuid, entityId);
        float yaw = 45.0f;
        float pitch = 30.0f;

        // Act
        player.UpdateRotation(yaw, pitch);

        // Assert
        Assert.Equal(yaw, player.Yaw);
        Assert.Equal(pitch, player.Pitch);
        Assert.Equal(yaw, player.HeadYaw); // Head yaw should match body yaw
    }

    [Fact]
    public void Player_UpdateHeadYaw_UpdatesHeadYawIndependently()
    {
        // Arrange
        var uuid = Guid.NewGuid();
        int entityId = 1;
        var player = new Player(uuid, entityId);
        float bodyYaw = 45.0f;
        float pitch = 30.0f;
        float headYaw = 60.0f;

        // Act
        player.UpdateRotation(bodyYaw, pitch);
        player.UpdateHeadYaw(headYaw);

        // Assert
        Assert.Equal(bodyYaw, player.Yaw);
        Assert.Equal(pitch, player.Pitch);
        Assert.Equal(headYaw, player.HeadYaw); // Head yaw can be different from body yaw
    }

    [Fact]
    public void Player_UpdateHeadYaw_CanBeDifferentFromBodyYaw()
    {
        // Arrange
        var uuid = Guid.NewGuid();
        int entityId = 1;
        var player = new Player(uuid, entityId);
        float bodyYaw = 0.0f;
        float headYaw = 90.0f;

        // Act
        player.UpdateRotation(bodyYaw, 0.0f);
        player.UpdateHeadYaw(headYaw);

        // Assert
        Assert.Equal(bodyYaw, player.Yaw);
        Assert.Equal(headYaw, player.HeadYaw);
        Assert.NotEqual(player.Yaw, player.HeadYaw);
    }

    [Fact]
    public void Player_UpdateRotation_MultipleTimes_HeadYawFollowsBodyYaw()
    {
        // Arrange
        var uuid = Guid.NewGuid();
        int entityId = 1;
        var player = new Player(uuid, entityId);

        // Act
        player.UpdateRotation(0.0f, 0.0f);
        Assert.Equal(0.0f, player.HeadYaw);

        player.UpdateRotation(45.0f, 0.0f);
        Assert.Equal(45.0f, player.HeadYaw);

        player.UpdateRotation(90.0f, 0.0f);
        Assert.Equal(90.0f, player.HeadYaw);

        player.UpdateRotation(180.0f, 0.0f);
        Assert.Equal(180.0f, player.HeadYaw);

        // Assert
        Assert.Equal(180.0f, player.Yaw);
        Assert.Equal(180.0f, player.HeadYaw);
    }

    [Fact]
    public void Player_UpdateHeadYaw_HandlesNegativeValues()
    {
        // Arrange
        var uuid = Guid.NewGuid();
        int entityId = 1;
        var player = new Player(uuid, entityId);
        float negativeHeadYaw = -45.0f;

        // Act
        player.UpdateHeadYaw(negativeHeadYaw);

        // Assert
        Assert.Equal(negativeHeadYaw, player.HeadYaw);
    }

    [Fact]
    public void Player_UpdateHeadYaw_HandlesLargeValues()
    {
        // Arrange
        var uuid = Guid.NewGuid();
        int entityId = 1;
        var player = new Player(uuid, entityId);
        float largeHeadYaw = 720.0f; // Two full rotations

        // Act
        player.UpdateHeadYaw(largeHeadYaw);

        // Assert
        Assert.Equal(largeHeadYaw, player.HeadYaw);
    }
}

