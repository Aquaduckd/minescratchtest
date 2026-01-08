using MineSharp.Core.DataTypes;
using MineSharp.Core.Protocol;
using Xunit;

namespace MineSharp.Tests.Protocol;

public class BlockPacketBuilderTests
{
    [Fact]
    public void BuildBlockUpdatePacket_RoundTrip_CanBeParsed()
    {
        int blockX = 10;
        int blockY = 64;
        int blockZ = 20;
        int blockStateId = 2098; // Grass block

        // Build packet
        var packet = PacketBuilder.BuildBlockUpdatePacket(blockX, blockY, blockZ, blockStateId);

        // Parse it back (skip length prefix, start from packet ID)
        var reader = new ProtocolReader(packet);
        var packetLength = reader.ReadVarInt();
        var packetId = reader.ReadVarInt();
        
        Assert.Equal(0x08, packetId); // Block Update packet ID
        
        // Read position (encoded as Long)
        var positionLong = reader.ReadLong();
        var position = Position.FromLong(positionLong);
        
        // Read block state ID
        var parsedBlockStateId = reader.ReadVarInt();
        
        Assert.Equal(blockX, position.X);
        Assert.Equal(blockY, position.Y);
        Assert.Equal(blockZ, position.Z);
        Assert.Equal(blockStateId, parsedBlockStateId);
    }

    [Fact]
    public void BuildBlockUpdatePacket_WithNegativeCoordinates_CanBeParsed()
    {
        int blockX = -100;
        int blockY = -64;
        int blockZ = -200;
        int blockStateId = 0; // Air

        // Build packet
        var packet = PacketBuilder.BuildBlockUpdatePacket(blockX, blockY, blockZ, blockStateId);

        // Parse it back
        var reader = new ProtocolReader(packet);
        var packetLength = reader.ReadVarInt();
        var packetId = reader.ReadVarInt();
        
        Assert.Equal(0x08, packetId);
        
        var positionLong = reader.ReadLong();
        var position = Position.FromLong(positionLong);
        var parsedBlockStateId = reader.ReadVarInt();
        
        Assert.Equal(blockX, position.X);
        Assert.Equal(blockY, position.Y);
        Assert.Equal(blockZ, position.Z);
        Assert.Equal(blockStateId, parsedBlockStateId);
    }

    [Fact]
    public void BuildSetBlockDestroyStagePacket_RoundTrip_CanBeParsed()
    {
        int entityId = 123;
        int blockX = 10;
        int blockY = 64;
        int blockZ = 20;
        byte destroyStage = 5; // Middle of breaking animation

        // Build packet
        var packet = PacketBuilder.BuildSetBlockDestroyStagePacket(entityId, blockX, blockY, blockZ, destroyStage);

        // Parse it back (skip length prefix, start from packet ID)
        var reader = new ProtocolReader(packet);
        var packetLength = reader.ReadVarInt();
        var packetId = reader.ReadVarInt();
        
        Assert.Equal(0x05, packetId); // Set Block Destroy Stage packet ID
        
        // Read entity ID
        var parsedEntityId = reader.ReadVarInt();
        
        // Read position (encoded as Long)
        var positionLong = reader.ReadLong();
        var position = Position.FromLong(positionLong);
        
        // Read destroy stage
        var parsedDestroyStage = reader.ReadByte();
        
        Assert.Equal(entityId, parsedEntityId);
        Assert.Equal(blockX, position.X);
        Assert.Equal(blockY, position.Y);
        Assert.Equal(blockZ, position.Z);
        Assert.Equal(destroyStage, parsedDestroyStage);
    }

    [Fact]
    public void BuildSetBlockDestroyStagePacket_WithClearAnimation_CanBeParsed()
    {
        int entityId = 456;
        int blockX = 0;
        int blockY = 65;
        int blockZ = 0;
        byte destroyStage = 10; // Clear animation

        // Build packet
        var packet = PacketBuilder.BuildSetBlockDestroyStagePacket(entityId, blockX, blockY, blockZ, destroyStage);

        // Parse it back
        var reader = new ProtocolReader(packet);
        var packetLength = reader.ReadVarInt();
        var packetId = reader.ReadVarInt();
        
        Assert.Equal(0x05, packetId);
        
        var parsedEntityId = reader.ReadVarInt();
        var positionLong = reader.ReadLong();
        var position = Position.FromLong(positionLong);
        var parsedDestroyStage = reader.ReadByte();
        
        Assert.Equal(entityId, parsedEntityId);
        Assert.Equal(blockX, position.X);
        Assert.Equal(blockY, position.Y);
        Assert.Equal(blockZ, position.Z);
        Assert.Equal(destroyStage, parsedDestroyStage);
    }

    [Fact]
    public void BuildSetBlockDestroyStagePacket_WithAllAnimationStages_CanBeParsed()
    {
        int entityId = 789;
        int blockX = 5;
        int blockY = 64;
        int blockZ = 5;

        // Test all valid animation stages (0-9)
        for (byte stage = 0; stage <= 9; stage++)
        {
            var packet = PacketBuilder.BuildSetBlockDestroyStagePacket(entityId, blockX, blockY, blockZ, stage);
            
            var reader = new ProtocolReader(packet);
            reader.ReadVarInt(); // Skip length
            var packetId = reader.ReadVarInt();
            
            Assert.Equal(0x05, packetId);
            
            var parsedEntityId = reader.ReadVarInt();
            var positionLong = reader.ReadLong();
            var position = Position.FromLong(positionLong);
            var parsedStage = reader.ReadByte();
            
            Assert.Equal(entityId, parsedEntityId);
            Assert.Equal(blockX, position.X);
            Assert.Equal(blockY, position.Y);
            Assert.Equal(blockZ, position.Z);
            Assert.Equal(stage, parsedStage);
        }
    }

    [Fact]
    public void BuildWorldEventPacket_RoundTrip_CanBeParsed()
    {
        int blockX = 10;
        int blockY = 64;
        int blockZ = 20;
        int eventId = 2001; // Block break + block break sound
        int data = 2098; // Block state ID (grass block)
        bool disableRelativeVolume = false;

        // Build packet
        var packet = PacketBuilder.BuildWorldEventPacket(blockX, blockY, blockZ, eventId, data, disableRelativeVolume);

        // Parse it back (skip length prefix, start from packet ID)
        var reader = new ProtocolReader(packet);
        var packetLength = reader.ReadVarInt();
        var packetId = reader.ReadVarInt();
        
        Assert.Equal(0x2D, packetId); // World Event packet ID
        
        // Read event ID
        var parsedEventId = reader.ReadInt();
        
        // Read position (encoded as Long)
        var positionLong = reader.ReadLong();
        var position = Position.FromLong(positionLong);
        
        // Read data
        var parsedData = reader.ReadInt();
        
        // Read disable relative volume flag
        var parsedDisableRelativeVolume = reader.ReadBool();
        
        Assert.Equal(eventId, parsedEventId);
        Assert.Equal(blockX, position.X);
        Assert.Equal(blockY, position.Y);
        Assert.Equal(blockZ, position.Z);
        Assert.Equal(data, parsedData);
        Assert.Equal(disableRelativeVolume, parsedDisableRelativeVolume);
    }

    [Fact]
    public void BuildWorldEventPacket_WithBlockBreakEvent_CanBeParsed()
    {
        int blockX = 0;
        int blockY = 65;
        int blockZ = 0;
        int eventId = 2001; // Block break + block break sound
        int data = 0; // Air (block was broken)
        bool disableRelativeVolume = false;

        // Build packet
        var packet = PacketBuilder.BuildWorldEventPacket(blockX, blockY, blockZ, eventId, data, disableRelativeVolume);

        // Parse it back
        var reader = new ProtocolReader(packet);
        reader.ReadVarInt(); // Skip length
        var packetId = reader.ReadVarInt();
        
        Assert.Equal(0x2D, packetId);
        
        var parsedEventId = reader.ReadInt();
        var positionLong = reader.ReadLong();
        var position = Position.FromLong(positionLong);
        var parsedData = reader.ReadInt();
        var parsedDisableRelativeVolume = reader.ReadBool();
        
        Assert.Equal(eventId, parsedEventId);
        Assert.Equal(blockX, position.X);
        Assert.Equal(blockY, position.Y);
        Assert.Equal(blockZ, position.Z);
        Assert.Equal(data, parsedData);
        Assert.Equal(disableRelativeVolume, parsedDisableRelativeVolume);
    }

    [Fact]
    public void BuildWorldEventPacket_WithDisableRelativeVolume_CanBeParsed()
    {
        int blockX = 100;
        int blockY = 64;
        int blockZ = 200;
        int eventId = 1023; // Wither spawned (uses disable relative volume)
        int data = 0;
        bool disableRelativeVolume = true;

        // Build packet
        var packet = PacketBuilder.BuildWorldEventPacket(blockX, blockY, blockZ, eventId, data, disableRelativeVolume);

        // Parse it back
        var reader = new ProtocolReader(packet);
        reader.ReadVarInt(); // Skip length
        var packetId = reader.ReadVarInt();
        
        Assert.Equal(0x2D, packetId);
        
        var parsedEventId = reader.ReadInt();
        var positionLong = reader.ReadLong();
        var position = Position.FromLong(positionLong);
        var parsedData = reader.ReadInt();
        var parsedDisableRelativeVolume = reader.ReadBool();
        
        Assert.Equal(eventId, parsedEventId);
        Assert.Equal(blockX, position.X);
        Assert.Equal(blockY, position.Y);
        Assert.Equal(blockZ, position.Z);
        Assert.Equal(data, parsedData);
        Assert.True(parsedDisableRelativeVolume);
    }

    [Fact]
    public void BuildWorldEventPacket_WithNegativeCoordinates_CanBeParsed()
    {
        int blockX = -50;
        int blockY = -32;
        int blockZ = -100;
        int eventId = 2001;
        int data = 2105; // Dirt block
        bool disableRelativeVolume = false;

        // Build packet
        var packet = PacketBuilder.BuildWorldEventPacket(blockX, blockY, blockZ, eventId, data, disableRelativeVolume);

        // Parse it back
        var reader = new ProtocolReader(packet);
        reader.ReadVarInt(); // Skip length
        var packetId = reader.ReadVarInt();
        
        Assert.Equal(0x2D, packetId);
        
        var parsedEventId = reader.ReadInt();
        var positionLong = reader.ReadLong();
        var position = Position.FromLong(positionLong);
        var parsedData = reader.ReadInt();
        var parsedDisableRelativeVolume = reader.ReadBool();
        
        Assert.Equal(eventId, parsedEventId);
        Assert.Equal(blockX, position.X);
        Assert.Equal(blockY, position.Y);
        Assert.Equal(blockZ, position.Z);
        Assert.Equal(data, parsedData);
        Assert.Equal(disableRelativeVolume, parsedDisableRelativeVolume);
    }
}

