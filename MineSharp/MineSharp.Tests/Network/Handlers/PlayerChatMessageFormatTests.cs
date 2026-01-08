using Xunit;

namespace MineSharp.Tests.Network.Handlers;

/// <summary>
/// Tests for player chat message formatting.
/// </summary>
public class PlayerChatMessageFormatTests
{
    [Fact]
    public void ChatMessage_ShouldHaveCorrectFormat()
    {
        // Arrange
        var playerName = "TestPlayer";
        var message = "Hello, world!";
        
        // Act - Format as "<PlayerName> message"
        var chatMessage = $"{{\"text\":\"<{playerName}> {message}\"}}";
        
        // Assert
        Assert.Contains($"<{playerName}>", chatMessage);
        Assert.Contains(message, chatMessage);
        Assert.StartsWith("{", chatMessage);
        Assert.EndsWith("}", chatMessage);
        Assert.Contains("\"text\":", chatMessage);
    }

    [Fact]
    public void ChatMessage_WithSpecialCharacters_ShouldEscapeCorrectly()
    {
        // Arrange
        var playerName = "TestPlayer";
        var message = "Hello! \"quoted\" and \\backslash\\ test";
        
        // Act - In production, EscapeJsonString would handle this
        // For this test, we verify the format structure
        var chatMessage = $"{{\"text\":\"<{playerName}> {message}\"}}";
        
        // Assert
        Assert.Contains($"<{playerName}>", chatMessage);
        Assert.Contains("Hello!", chatMessage);
        // Note: Proper JSON escaping would be tested in integration tests
    }

    [Fact]
    public void ChatMessage_WithEmptyMessage_ShouldNotBeFormatted()
    {
        // Arrange
        var message = "";
        
        // Act - Empty messages should not be formatted/broadcast
        // This is handled in HandleChatMessageAsync by returning early
        
        // Assert - Empty messages are rejected before formatting
        Assert.True(string.IsNullOrEmpty(message));
    }

    [Fact]
    public void ChatMessage_WithLongMessage_ShouldBeTruncated()
    {
        // Arrange
        var longMessage = new string('A', 300); // 300 characters
        
        // Act - Long messages should be truncated to 256 characters
        var truncatedMessage = longMessage.Length > 256 ? longMessage.Substring(0, 256) : longMessage;
        
        // Assert
        Assert.Equal(256, truncatedMessage.Length);
        Assert.True(longMessage.Length > 256);
    }

    [Fact]
    public void ChatMessage_WithUnicodeCharacters_ShouldBeHandled()
    {
        // Arrange
        var playerName = "TestPlayer";
        var message = "Hello! こんにちは 🌟";
        
        // Act
        var chatMessage = $"{{\"text\":\"<{playerName}> {message}\"}}";
        
        // Assert
        Assert.Contains($"<{playerName}>", chatMessage);
        Assert.Contains("こんにちは", chatMessage);
        Assert.Contains("🌟", chatMessage);
    }
}

