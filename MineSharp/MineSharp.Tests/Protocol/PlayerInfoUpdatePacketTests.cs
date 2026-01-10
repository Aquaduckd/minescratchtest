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

    [Fact]
    public void BuildPlayerInfoUpdatePacket_IncludesAddPlayerAndUpdateListedActions()
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

        // Parse the packet to verify structure
        var reader = new ProtocolReader(packet);
        
        // Skip packet length (VarInt)
        int packetLength = reader.ReadVarInt();
        
        // Read packet ID
        int packetId = reader.ReadVarInt();
        Assert.Equal(0x44, packetId); // Player Info Update packet ID
        
        // Read Fixed BitSet (Actions)
        // Should have bits 0x01 (Add Player) and 0x08 (Update Listed) set = 0x09
        byte actions = reader.ReadByte(); // Fixed BitSet with 6 bits = 1 byte
        Assert.Equal(0x09, actions); // 0x01 | 0x08 = 0x09
        
        // Read players count
        int playersCount = reader.ReadVarInt();
        Assert.Equal(1, playersCount);
        
        // Read player UUID
        Guid uuid = reader.ReadUuid();
        Assert.Equal(players[0].Item1, uuid);
        
        // Read Add Player action data (bit 0)
        string name = reader.ReadString();
        Assert.Equal("TestPlayer", name);
        
        int propertiesCount = reader.ReadVarInt();
        Assert.Equal(0, propertiesCount); // Empty properties array
        
        // Read Update Listed action data (bit 3)
        bool listed = reader.ReadBool();
        Assert.True(listed); // Should be true for tab list visibility
    }

    [Fact]
    public void BuildPlayerInfoUpdatePacket_MultiplePlayers_IncludesListedForAll()
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

        // Parse the packet to verify structure
        var reader = new ProtocolReader(packet);
        
        // Skip packet length
        reader.ReadVarInt();
        
        // Read packet ID
        int packetId = reader.ReadVarInt();
        Assert.Equal(0x44, packetId);
        
        // Verify actions include both Add Player and Update Listed
        byte actions = reader.ReadByte();
        Assert.Equal(0x09, actions); // 0x01 | 0x08 = 0x09
        
        // Read players count
        int playersCount = reader.ReadVarInt();
        Assert.Equal(2, playersCount);
        
        // Read first player
        Guid uuid1 = reader.ReadUuid();
        Assert.Equal(players[0].Item1, uuid1);
        string name1 = reader.ReadString();
        Assert.Equal("Player1", name1);
        reader.ReadVarInt(); // Skip properties count
        bool listed1 = reader.ReadBool();
        Assert.True(listed1);
        
        // Read second player
        Guid uuid2 = reader.ReadUuid();
        Assert.Equal(players[1].Item1, uuid2);
        string name2 = reader.ReadString();
        Assert.Equal("Player2", name2);
        reader.ReadVarInt(); // Skip properties count
        bool listed2 = reader.ReadBool();
        Assert.True(listed2);
    }

    [Fact]
    public void BuildPlayerInfoUpdatePacket_FixedBitSet_HasCorrectBits()
    {
        // Arrange
        var players = new List<(Guid, string)>
        {
            (Guid.NewGuid(), "TestPlayer")
        };

        // Act
        byte[] packet = PacketBuilder.BuildPlayerInfoUpdatePacket(players);

        // Assert
        // Find the Fixed BitSet byte after packet ID
        var reader = new ProtocolReader(packet);
        reader.ReadVarInt(); // Skip length
        reader.ReadVarInt(); // Skip packet ID (0x44)
        
        // Read Fixed BitSet (1 byte for 6 actions)
        byte actions = reader.ReadByte();
        
        // Verify bit 0 (Add Player = 0x01) is set
        Assert.True((actions & 0x01) != 0, "Add Player action (bit 0) should be set");
        
        // Verify bit 3 (Update Listed = 0x08) is set
        Assert.True((actions & 0x08) != 0, "Update Listed action (bit 3) should be set");
        
        // Verify no other bits are set
        Assert.Equal(0x09, actions); // Only bits 0 and 3 should be set
    }
}

public class PlayerInfoRemovePacketTests
{
    [Fact]
    public void BuildPlayerInfoRemovePacket_SinglePlayer()
    {
        // Arrange
        var playerUuids = new List<Guid>
        {
            Guid.Parse("6a9aec1a-6df2-41d5-ab8b-404a8dfe85ed")
        };

        // Act
        byte[] packet = PacketBuilder.BuildPlayerInfoRemovePacket(playerUuids);

        // Assert
        Assert.NotNull(packet);
        Assert.True(packet.Length > 0);
    }

    [Fact]
    public void BuildPlayerInfoRemovePacket_MultiplePlayers()
    {
        // Arrange
        var playerUuids = new List<Guid>
        {
            Guid.Parse("6a9aec1a-6df2-41d5-ab8b-404a8dfe85ed"),
            Guid.Parse("670fb6ce-0b55-448f-a9a9-ed3f1da93508")
        };

        // Act
        byte[] packet = PacketBuilder.BuildPlayerInfoRemovePacket(playerUuids);

        // Assert
        Assert.NotNull(packet);
        Assert.True(packet.Length > 0);
    }

    [Fact]
    public void BuildPlayerInfoRemovePacket_ThrowsOnEmptyList()
    {
        // Arrange
        var playerUuids = new List<Guid>();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => PacketBuilder.BuildPlayerInfoRemovePacket(playerUuids));
    }

    [Fact]
    public void BuildPlayerInfoRemovePacket_ThrowsOnNullList()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => PacketBuilder.BuildPlayerInfoRemovePacket(null!));
    }

    [Fact]
    public void BuildPlayerInfoRemovePacket_IncludesCorrectPacketId()
    {
        // Arrange
        var playerUuids = new List<Guid>
        {
            Guid.Parse("6a9aec1a-6df2-41d5-ab8b-404a8dfe85ed")
        };

        // Act
        byte[] packet = PacketBuilder.BuildPlayerInfoRemovePacket(playerUuids);

        // Assert
        Assert.NotNull(packet);
        
        // Parse the packet to verify structure
        var reader = new ProtocolReader(packet);
        
        // Skip packet length (VarInt)
        reader.ReadVarInt();
        
        // Read packet ID
        int packetId = reader.ReadVarInt();
        Assert.Equal(0x43, packetId); // Player Info Remove packet ID
    }

    [Fact]
    public void BuildPlayerInfoRemovePacket_IncludesUuids()
    {
        // Arrange
        var playerUuids = new List<Guid>
        {
            Guid.Parse("6a9aec1a-6df2-41d5-ab8b-404a8dfe85ed"),
            Guid.Parse("670fb6ce-0b55-448f-a9a9-ed3f1da93508")
        };

        // Act
        byte[] packet = PacketBuilder.BuildPlayerInfoRemovePacket(playerUuids);

        // Assert
        Assert.NotNull(packet);
        
        // Parse the packet to verify structure
        var reader = new ProtocolReader(packet);
        
        // Skip packet length
        reader.ReadVarInt();
        
        // Read packet ID
        int packetId = reader.ReadVarInt();
        Assert.Equal(0x43, packetId);
        
        // Read UUIDs count
        int uuidsCount = reader.ReadVarInt();
        Assert.Equal(2, uuidsCount);
        
        // Read first UUID
        Guid uuid1 = reader.ReadUuid();
        Assert.Equal(playerUuids[0], uuid1);
        
        // Read second UUID
        Guid uuid2 = reader.ReadUuid();
        Assert.Equal(playerUuids[1], uuid2);
    }
}

