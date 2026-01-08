using MineSharp.Core;
using MineSharp.Core.Protocol;
using MineSharp.Core.Protocol.PacketTypes;
using Xunit;

namespace MineSharp.Tests.Protocol;

public class ChatMessagePacketParserTests
{
    [Fact]
    public void ParseChatMessage_WithSimpleMessage_ShouldParseCorrectly()
    {
        // Arrange
        var message = "Hello, world!";
        var timestamp = 1234567890L;
        var salt = 9876543210L;
        
        // Build full packet with length prefix and packet ID
        var packetWriter = new ProtocolWriter();
        packetWriter.WriteVarInt(0x08); // Chat Message packet ID
        packetWriter.WriteString(message);
        packetWriter.WriteLong(timestamp);
        packetWriter.WriteLong(salt);
        
        var packetData = packetWriter.ToArray();
        var finalWriter = new ProtocolWriter();
        finalWriter.WriteVarInt(packetData.Length);
        finalWriter.WriteBytes(packetData);
        
        var fullPacket = finalWriter.ToArray();
        
        // Act
        var (packetId, packet) = PacketParser.ParsePacket(fullPacket, ConnectionState.Play);
        
        // Assert
        Assert.Equal(0x08, packetId);
        Assert.NotNull(packet);
        var chatPacket = Assert.IsType<ChatMessagePacket>(packet);
        Assert.Equal(message, chatPacket.Message);
        Assert.Equal(timestamp, chatPacket.Timestamp);
        Assert.Equal(salt, chatPacket.Salt);
    }

    [Fact]
    public void ParseChatMessage_WithEmptyMessage_ShouldParseCorrectly()
    {
        // Arrange
        var message = "";
        var timestamp = 1234567890L;
        var salt = 9876543210L;
        
        // Build full packet with length prefix and packet ID
        var packetWriter = new ProtocolWriter();
        packetWriter.WriteVarInt(0x08); // Chat Message packet ID
        packetWriter.WriteString(message);
        packetWriter.WriteLong(timestamp);
        packetWriter.WriteLong(salt);
        
        var packetData = packetWriter.ToArray();
        var finalWriter = new ProtocolWriter();
        finalWriter.WriteVarInt(packetData.Length);
        finalWriter.WriteBytes(packetData);
        
        var fullPacket = finalWriter.ToArray();
        
        // Act
        var (packetId, packet) = PacketParser.ParsePacket(fullPacket, ConnectionState.Play);
        
        // Assert
        Assert.Equal(0x08, packetId);
        Assert.NotNull(packet);
        var chatPacket = Assert.IsType<ChatMessagePacket>(packet);
        Assert.Equal(message, chatPacket.Message);
        Assert.Equal(timestamp, chatPacket.Timestamp);
        Assert.Equal(salt, chatPacket.Salt);
    }

    [Fact]
    public void ParseChatMessage_WithLongMessage_ShouldParseCorrectly()
    {
        // Arrange
        // Create a message that's close to the 256 character limit
        var message = new string('A', 255);
        var timestamp = 1234567890L;
        var salt = 9876543210L;
        
        // Build full packet with length prefix and packet ID
        var packetWriter = new ProtocolWriter();
        packetWriter.WriteVarInt(0x08); // Chat Message packet ID
        packetWriter.WriteString(message);
        packetWriter.WriteLong(timestamp);
        packetWriter.WriteLong(salt);
        
        var packetData = packetWriter.ToArray();
        var finalWriter = new ProtocolWriter();
        finalWriter.WriteVarInt(packetData.Length);
        finalWriter.WriteBytes(packetData);
        
        var fullPacket = finalWriter.ToArray();
        
        // Act
        var (packetId, packet) = PacketParser.ParsePacket(fullPacket, ConnectionState.Play);
        
        // Assert
        Assert.Equal(0x08, packetId);
        Assert.NotNull(packet);
        var chatPacket = Assert.IsType<ChatMessagePacket>(packet);
        Assert.Equal(message, chatPacket.Message);
        Assert.Equal(255, chatPacket.Message.Length);
        Assert.Equal(timestamp, chatPacket.Timestamp);
        Assert.Equal(salt, chatPacket.Salt);
    }

    [Fact]
    public void ParseChatMessage_WithSpecialCharacters_ShouldParseCorrectly()
    {
        // Arrange
        var message = "Hello! @#$%^&*() Test 123";
        var timestamp = 1234567890L;
        var salt = 9876543210L;
        
        // Build full packet with length prefix and packet ID
        var packetWriter = new ProtocolWriter();
        packetWriter.WriteVarInt(0x08); // Chat Message packet ID
        packetWriter.WriteString(message);
        packetWriter.WriteLong(timestamp);
        packetWriter.WriteLong(salt);
        
        var packetData = packetWriter.ToArray();
        var finalWriter = new ProtocolWriter();
        finalWriter.WriteVarInt(packetData.Length);
        finalWriter.WriteBytes(packetData);
        
        var fullPacket = finalWriter.ToArray();
        
        // Act
        var (packetId, packet) = PacketParser.ParsePacket(fullPacket, ConnectionState.Play);
        
        // Assert
        Assert.Equal(0x08, packetId);
        Assert.NotNull(packet);
        var chatPacket = Assert.IsType<ChatMessagePacket>(packet);
        Assert.Equal(message, chatPacket.Message);
        Assert.Equal(timestamp, chatPacket.Timestamp);
        Assert.Equal(salt, chatPacket.Salt);
    }
}

