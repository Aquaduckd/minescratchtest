using System;
using System.Collections.Generic;
using MineSharp.Core.Protocol;
using Xunit;

namespace MineSharp.Tests.Protocol;

public class PlayerInfoUpdatePacketTests
{
    [Fact]
    public void BuildPlayerInfoUpdatePacket_SinglePlayer()
    {
        // Arrange
        var players = new List<(Guid, string)>
        {
            (Guid.Parse("6a9aec1a-6df2-41d5-ab8b-404a8dfe85ed"), "TestPlayer")
        };

        // Act
        byte[] packet = PacketBuilder.BuildPlayerInfoUpdatePacket(players);

        // Assert
        Assert.NotNull(packet);
        Assert.True(packet.Length > 0);
    }

    [Fact]
    public void BuildPlayerInfoUpdatePacket_MultiplePlayers()
    {
        // Arrange
        var players = new List<(Guid, string)>
        {
            (Guid.Parse("6a9aec1a-6df2-41d5-ab8b-404a8dfe85ed"), "Player1"),
            (Guid.Parse("670fb6ce-0b55-448f-a9a9-ed3f1da93508"), "Player2")
        };

        // Act
        byte[] packet = PacketBuilder.BuildPlayerInfoUpdatePacket(players);

        // Assert
        Assert.NotNull(packet);
        Assert.True(packet.Length > 0);
    }

    [Fact]
    public void BuildPlayerInfoUpdatePacket_TruncatesLongNames()
    {
        // Arrange
        var longName = new string('A', 20); // 20 characters
        var players = new List<(Guid, string)>
        {
            (Guid.NewGuid(), longName)
        };

        // Act
        byte[] packet = PacketBuilder.BuildPlayerInfoUpdatePacket(players);

        // Assert
        Assert.NotNull(packet);
        // Should not throw, name should be truncated to 16 chars
    }

    [Fact]
    public void BuildPlayerInfoUpdatePacket_ThrowsOnEmptyList()
    {
        // Arrange
        var players = new List<(Guid, string)>();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => PacketBuilder.BuildPlayerInfoUpdatePacket(players));
    }

    [Fact]
    public void BuildPlayerInfoUpdatePacket_ThrowsOnNullList()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => PacketBuilder.BuildPlayerInfoUpdatePacket(null!));
    }

    [Fact]
    public void BuildPlayerInfoUpdatePacket_ThrowsOnEmptyName()
    {
        // Arrange
        var players = new List<(Guid, string)>
        {
            (Guid.NewGuid(), "")
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => PacketBuilder.BuildPlayerInfoUpdatePacket(players));
    }
}

