using System;
using System.Threading;
using MineSharp.Network.Handlers;
using Xunit;

namespace MineSharp.Tests.Network.Handlers;

public class BlockBreakingSessionManagerTests
{
    [Fact]
    public void StartSession_CreatesNewSession()
    {
        // Arrange
        var manager = new BlockBreakingSessionManager();
        var playerUuid = Guid.NewGuid();
        int entityId = 1;
        int blockX = 0, blockY = 64, blockZ = 0;
        int totalTicks = 20;
        string blockName = "minecraft:stone";
        string toolName = "minecraft:iron_pickaxe";
        double toolSpeed = 6.0;
        double blockHardness = 1.5;

        // Act
        var session = manager.StartSession(
            playerUuid, entityId, blockX, blockY, blockZ,
            totalTicks, blockName, toolName, toolSpeed, blockHardness);

        // Assert
        Assert.NotNull(session);
        Assert.Equal(playerUuid, session.PlayerUuid);
        Assert.Equal(entityId, session.EntityId);
        Assert.Equal(blockX, session.BlockX);
        Assert.Equal(blockY, session.BlockY);
        Assert.Equal(blockZ, session.BlockZ);
        Assert.Equal(totalTicks, session.TotalTicks);
        Assert.Equal(0, session.CurrentTick);
        Assert.Equal(0, session.CurrentStage);
        Assert.False(session.IsComplete);
        Assert.Equal(0.0, session.Progress);
        Assert.Equal(blockName, session.BlockName);
        Assert.Equal(toolName, session.ToolName);
        Assert.Equal(toolSpeed, session.ToolSpeed);
        Assert.Equal(blockHardness, session.BlockHardness);
        
        // Verify session is retrievable
        var retrieved = manager.GetSession(playerUuid);
        Assert.NotNull(retrieved);
        Assert.Equal(session.PlayerUuid, retrieved.PlayerUuid);
    }

    [Fact]
    public void StartSession_WithExistingSession_CancelsOld()
    {
        // Arrange
        var manager = new BlockBreakingSessionManager();
        var playerUuid = Guid.NewGuid();
        int entityId = 1;

        // Create first session
        var session1 = manager.StartSession(
            playerUuid, entityId, 0, 64, 0,
            20, "minecraft:stone", "hand", 1.0, 1.5);
        
        Assert.NotNull(manager.GetSession(playerUuid));
        var token1 = session1.CancellationToken;

        // Act - Start new session with different block
        var session2 = manager.StartSession(
            playerUuid, entityId, 1, 64, 0,
            15, "minecraft:dirt", "hand", 1.0, 0.5);

        // Assert
        Assert.True(token1.Token.IsCancellationRequested); // Old session cancelled
        Assert.False(session2.CancellationToken.Token.IsCancellationRequested); // New session active
        
        var retrieved = manager.GetSession(playerUuid);
        Assert.NotNull(retrieved);
        Assert.Equal(session2.BlockX, retrieved.BlockX); // Should be new session
        Assert.Equal(1, retrieved.BlockX); // Different block
    }

    [Fact]
    public void CancelSession_RemovesSession()
    {
        // Arrange
        var manager = new BlockBreakingSessionManager();
        var playerUuid = Guid.NewGuid();
        var session = manager.StartSession(
            playerUuid, 1, 0, 64, 0,
            20, "minecraft:stone", "hand", 1.0, 1.5);
        
        Assert.NotNull(manager.GetSession(playerUuid));

        // Act
        bool cancelled = manager.CancelSession(playerUuid);

        // Assert
        Assert.True(cancelled);
        Assert.True(session.CancellationToken.Token.IsCancellationRequested);
        Assert.Null(manager.GetSession(playerUuid));
        Assert.False(manager.HasSession(playerUuid));
    }

    [Fact]
    public void CancelSession_WithNoSession_ReturnsFalse()
    {
        // Arrange
        var manager = new BlockBreakingSessionManager();
        var playerUuid = Guid.NewGuid();

        // Act
        bool cancelled = manager.CancelSession(playerUuid);

        // Assert
        Assert.False(cancelled);
    }

    [Fact]
    public void GetSession_WithActiveSession_ReturnsSession()
    {
        // Arrange
        var manager = new BlockBreakingSessionManager();
        var playerUuid = Guid.NewGuid();
        var session = manager.StartSession(
            playerUuid, 1, 0, 64, 0,
            20, "minecraft:stone", "hand", 1.0, 1.5);

        // Act
        var retrieved = manager.GetSession(playerUuid);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(session.PlayerUuid, retrieved.PlayerUuid);
        Assert.Equal(session.BlockX, retrieved.BlockX);
        Assert.Equal(session.BlockY, retrieved.BlockY);
        Assert.Equal(session.BlockZ, retrieved.BlockZ);
    }

    [Fact]
    public void GetSession_WithNoSession_ReturnsNull()
    {
        // Arrange
        var manager = new BlockBreakingSessionManager();
        var playerUuid = Guid.NewGuid();

        // Act
        var session = manager.GetSession(playerUuid);

        // Assert
        Assert.Null(session);
    }

    [Fact]
    public void HasSession_WithActiveSession_ReturnsTrue()
    {
        // Arrange
        var manager = new BlockBreakingSessionManager();
        var playerUuid = Guid.NewGuid();
        manager.StartSession(
            playerUuid, 1, 0, 64, 0,
            20, "minecraft:stone", "hand", 1.0, 1.5);

        // Act
        bool hasSession = manager.HasSession(playerUuid);

        // Assert
        Assert.True(hasSession);
    }

    [Fact]
    public void HasSession_WithNoSession_ReturnsFalse()
    {
        // Arrange
        var manager = new BlockBreakingSessionManager();
        var playerUuid = Guid.NewGuid();

        // Act
        bool hasSession = manager.HasSession(playerUuid);

        // Assert
        Assert.False(hasSession);
    }

    [Fact]
    public void Session_IsComplete_WhenCurrentTickEqualsTotalTicks()
    {
        // Arrange
        var manager = new BlockBreakingSessionManager();
        var playerUuid = Guid.NewGuid();
        var session = manager.StartSession(
            playerUuid, 1, 0, 64, 0,
            20, "minecraft:stone", "hand", 1.0, 1.5);

        // Act
        session.CurrentTick = 20; // Set to total ticks

        // Assert
        Assert.True(session.IsComplete);
        Assert.Equal(1.0, session.Progress);
    }

    [Fact]
    public void Session_Progress_CalculatesCorrectly()
    {
        // Arrange
        var manager = new BlockBreakingSessionManager();
        var playerUuid = Guid.NewGuid();
        var session = manager.StartSession(
            playerUuid, 1, 0, 64, 0,
            20, "minecraft:stone", "hand", 1.0, 1.5);

        // Act & Assert
        session.CurrentTick = 0;
        Assert.Equal(0.0, session.Progress);

        session.CurrentTick = 10;
        Assert.Equal(0.5, session.Progress);

        session.CurrentTick = 15;
        Assert.Equal(0.75, session.Progress);

        session.CurrentTick = 20;
        Assert.Equal(1.0, session.Progress);

        session.CurrentTick = 25; // Exceeds total
        Assert.Equal(1.0, session.Progress); // Capped at 1.0
    }

    [Fact]
    public void RemoveSession_RemovesSession()
    {
        // Arrange
        var manager = new BlockBreakingSessionManager();
        var playerUuid = Guid.NewGuid();
        manager.StartSession(
            playerUuid, 1, 0, 64, 0,
            20, "minecraft:stone", "hand", 1.0, 1.5);
        
        Assert.True(manager.HasSession(playerUuid));

        // Act
        manager.RemoveSession(playerUuid);

        // Assert
        Assert.False(manager.HasSession(playerUuid));
        Assert.Null(manager.GetSession(playerUuid));
    }

    [Fact]
    public void CleanupCompletedSessions_RemovesCompletedSessions()
    {
        // Arrange
        var manager = new BlockBreakingSessionManager();
        var playerUuid1 = Guid.NewGuid();
        var playerUuid2 = Guid.NewGuid();
        
        var session1 = manager.StartSession(
            playerUuid1, 1, 0, 64, 0,
            20, "minecraft:stone", "hand", 1.0, 1.5);
        
        var session2 = manager.StartSession(
            playerUuid2, 2, 1, 64, 0,
            15, "minecraft:dirt", "hand", 1.0, 0.5);

        // Mark session1 as complete
        session1.CurrentTick = 20;

        // Act
        manager.CleanupCompletedSessions();

        // Assert
        Assert.Null(manager.GetSession(playerUuid1)); // Completed session removed
        Assert.NotNull(manager.GetSession(playerUuid2)); // Active session remains
    }

    [Fact]
    public void CleanupCompletedSessions_RemovesCancelledSessions()
    {
        // Arrange
        var manager = new BlockBreakingSessionManager();
        var playerUuid = Guid.NewGuid();
        
        var session = manager.StartSession(
            playerUuid, 1, 0, 64, 0,
            20, "minecraft:stone", "hand", 1.0, 1.5);
        
        // Cancel the session
        session.CancellationToken.Cancel();

        // Act
        manager.CleanupCompletedSessions();

        // Assert
        Assert.Null(manager.GetSession(playerUuid)); // Cancelled session removed
    }

    [Fact]
    public void ClearAllSessions_CancelsAllSessions()
    {
        // Arrange
        var manager = new BlockBreakingSessionManager();
        var playerUuid1 = Guid.NewGuid();
        var playerUuid2 = Guid.NewGuid();
        
        var session1 = manager.StartSession(
            playerUuid1, 1, 0, 64, 0,
            20, "minecraft:stone", "hand", 1.0, 1.5);
        
        var session2 = manager.StartSession(
            playerUuid2, 2, 1, 64, 0,
            15, "minecraft:dirt", "hand", 1.0, 0.5);

        // Act
        manager.ClearAllSessions();

        // Assert
        Assert.True(session1.CancellationToken.Token.IsCancellationRequested);
        Assert.True(session2.CancellationToken.Token.IsCancellationRequested);
        Assert.Null(manager.GetSession(playerUuid1));
        Assert.Null(manager.GetSession(playerUuid2));
    }

    [Fact]
    public void MultipleSessions_AreIndependent()
    {
        // Arrange
        var manager = new BlockBreakingSessionManager();
        var playerUuid1 = Guid.NewGuid();
        var playerUuid2 = Guid.NewGuid();

        // Act
        var session1 = manager.StartSession(
            playerUuid1, 1, 0, 64, 0,
            20, "minecraft:stone", "hand", 1.0, 1.5);
        
        var session2 = manager.StartSession(
            playerUuid2, 2, 1, 64, 0,
            15, "minecraft:dirt", "hand", 1.0, 0.5);

        // Assert
        Assert.NotNull(manager.GetSession(playerUuid1));
        Assert.NotNull(manager.GetSession(playerUuid2));
        Assert.NotEqual(session1.PlayerUuid, session2.PlayerUuid);
        Assert.NotEqual(session1.BlockX, session2.BlockX);
    }
}

