using MineSharp.Network.Handlers;
using Xunit;

namespace MineSharp.Tests.Network.Handlers;

/// <summary>
/// Tests for message text extraction from JSON (used in logging).
/// </summary>
public class SystemChatMessageExtractionTests
{
    [Fact]
    public void ExtractTextFromJson_WithSimpleMessage_ShouldExtractText()
    {
        // Arrange
        var messageJson = "{\"text\":\"Hello, world!\"}";
        var playHandler = new PlayHandler();
        
        // Act - Use reflection to access private method for testing
        var method = typeof(PlayHandler).GetMethod("ExtractTextFromJson", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);
        
        var result = method.Invoke(playHandler, new object[] { messageJson }) as string;
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal("Hello, world!", result);
    }

    [Fact]
    public void ExtractTextFromJson_WithFormattedMessage_ShouldExtractText()
    {
        // Arrange
        var messageJson = "{\"text\":\"PlayerName joined the game\",\"color\":\"yellow\"}";
        var playHandler = new PlayHandler();
        
        // Act
        var method = typeof(PlayHandler).GetMethod("ExtractTextFromJson", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);
        
        var result = method.Invoke(playHandler, new object[] { messageJson }) as string;
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal("PlayerName joined the game", result);
    }

    [Fact]
    public void ExtractTextFromJson_WithInvalidJson_ShouldReturnFallback()
    {
        // Arrange
        var messageJson = "not valid json";
        var playHandler = new PlayHandler();
        
        // Act
        var method = typeof(PlayHandler).GetMethod("ExtractTextFromJson", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);
        
        var result = method.Invoke(playHandler, new object[] { messageJson }) as string;
        
        // Assert
        Assert.NotNull(result);
        // Should return a fallback (truncated version or original)
        Assert.True(result.Length > 0);
    }
}

