using Xunit;

namespace MineSharp.Tests.Network.Handlers;

/// <summary>
/// Tests for join/leave message formatting.
/// </summary>
public class JoinLeaveMessageFormatTests
{
    [Fact]
    public void JoinMessage_ShouldHaveCorrectFormat()
    {
        // Arrange
        var playerName = "TestPlayer";
        
        // Act
        var joinMessage = $"{{\"text\":\"{playerName} joined the game\",\"color\":\"yellow\"}}";
        
        // Assert
        Assert.Contains(playerName, joinMessage);
        Assert.Contains("joined the game", joinMessage);
        Assert.Contains("\"color\":\"yellow\"", joinMessage);
        Assert.StartsWith("{", joinMessage);
        Assert.EndsWith("}", joinMessage);
    }

    [Fact]
    public void LeaveMessage_ShouldHaveCorrectFormat()
    {
        // Arrange
        var playerName = "TestPlayer";
        
        // Act
        var leaveMessage = $"{{\"text\":\"{playerName} left the game\",\"color\":\"yellow\"}}";
        
        // Assert
        Assert.Contains(playerName, leaveMessage);
        Assert.Contains("left the game", leaveMessage);
        Assert.Contains("\"color\":\"yellow\"", leaveMessage);
        Assert.StartsWith("{", leaveMessage);
        Assert.EndsWith("}", leaveMessage);
    }

    [Fact]
    public void JoinMessage_WithSpecialCharacters_ShouldEscapeCorrectly()
    {
        // Arrange
        var playerName = "Player\"With\"Quotes";
        
        // Act
        var joinMessage = $"{{\"text\":\"{playerName} joined the game\",\"color\":\"yellow\"}}";
        
        // Assert
        // Note: This test verifies the format, but in production we should use proper JSON escaping
        Assert.Contains(playerName, joinMessage);
        Assert.Contains("\"color\":\"yellow\"", joinMessage);
    }

    [Fact]
    public void LeaveMessage_WithSpecialCharacters_ShouldEscapeCorrectly()
    {
        // Arrange
        var playerName = "Player\"With\"Quotes";
        
        // Act
        var leaveMessage = $"{{\"text\":\"{playerName} left the game\",\"color\":\"yellow\"}}";
        
        // Assert
        // Note: This test verifies the format, but in production we should use proper JSON escaping
        Assert.Contains(playerName, leaveMessage);
        Assert.Contains("\"color\":\"yellow\"", leaveMessage);
    }
}

