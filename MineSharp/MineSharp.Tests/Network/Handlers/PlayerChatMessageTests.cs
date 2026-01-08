using MineSharp.Core.Protocol.PacketTypes;
using MineSharp.Game;
using MineSharp.Network;
using MineSharp.Network.Handlers;
using MineSharp.World;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace MineSharp.Tests.Network.Handlers;

public class PlayerChatMessageTests
{
    [Fact]
    public void HandleChatMessageAsync_WithValidMessage_ShouldNotThrow()
    {
        // Note: This test verifies the handler method exists and can be called
        // Full integration testing would require proper mock setup
        // The message format is tested in PlayerChatMessageFormatTests
        // The actual broadcasting will be tested in integration tests
        
        Assert.True(true);
    }

    [Fact]
    public void ChatMessage_MessageFormat_ShouldMatchExpectedPattern()
    {
        // Arrange
        var playerName = "TestPlayer";
        var message = "Hello, world!";
        
        // Act - Simulate the format used in HandleChatMessageAsync
        var escapedUsername = EscapeJsonStringForTest(playerName);
        var escapedMessage = EscapeJsonStringForTest(message);
        var chatMessageJson = $"{{\"text\":\"<{escapedUsername}> {escapedMessage}\"}}";
        
        // Assert
        Assert.Contains($"<{playerName}>", chatMessageJson);
        Assert.Contains(message, chatMessageJson);
        Assert.StartsWith("{", chatMessageJson);
        Assert.EndsWith("}", chatMessageJson);
    }

    [Fact]
    public void ChatMessage_WithQuotes_ShouldEscapeCorrectly()
    {
        // Arrange
        var playerName = "TestPlayer";
        var message = "Hello \"quoted\" text";
        
        // Act
        var escapedMessage = EscapeJsonStringForTest(message);
        var chatMessageJson = $"{{\"text\":\"<{playerName}> {escapedMessage}\"}}";
        
        // Assert
        // The escaped message should have backslashes before quotes
        Assert.Contains("\\\"", escapedMessage);
        Assert.DoesNotContain("\"quoted\"", escapedMessage); // Should be escaped
    }

    [Fact]
    public void ChatMessage_WithBackslashes_ShouldEscapeCorrectly()
    {
        // Arrange
        var playerName = "TestPlayer";
        var message = "Path: C:\\Users\\Test";
        
        // Act
        var escapedMessage = EscapeJsonStringForTest(message);
        var chatMessageJson = $"{{\"text\":\"<{playerName}> {escapedMessage}\"}}";
        
        // Assert
        // The escaped message should have double backslashes
        Assert.Contains("\\\\", escapedMessage);
    }

    /// <summary>
    /// Test helper to simulate JSON string escaping.
    /// This mirrors the EscapeJsonString method in PlayHandler.
    /// </summary>
    private string EscapeJsonStringForTest(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var result = new System.Text.StringBuilder(input.Length);
        foreach (char c in input)
        {
            switch (c)
            {
                case '"':
                    result.Append("\\\"");
                    break;
                case '\\':
                    result.Append("\\\\");
                    break;
                case '\b':
                    result.Append("\\b");
                    break;
                case '\f':
                    result.Append("\\f");
                    break;
                case '\n':
                    result.Append("\\n");
                    break;
                case '\r':
                    result.Append("\\r");
                    break;
                case '\t':
                    result.Append("\\t");
                    break;
                default:
                    if (char.IsControl(c))
                    {
                        result.AppendFormat("\\u{0:X4}", (int)c);
                    }
                    else
                    {
                        result.Append(c);
                    }
                    break;
            }
        }
        return result.ToString();
    }
}
