using MineSharp.Core.Protocol;
using Xunit;

namespace MineSharp.Tests.Protocol;

public class SetEntityMetadataPacketBuilderTests
{
    [Fact]
    public void BuildSetEntityMetadataPacket_Sneaking_IncludesCorrectFlags()
    {
        // Arrange
        int entityId = 123;
        bool isSneaking = true;

        // Act
        byte[] packet = PacketBuilder.BuildSetEntityMetadataPacket(entityId, isSneaking);

        // Assert
        Assert.NotNull(packet);
        Assert.True(packet.Length > 0);

        // Parse the packet to verify structure
        var reader = new ProtocolReader(packet);
        
        // Skip packet length
        reader.ReadVarInt();
        
        // Read packet ID
        int packetId = reader.ReadVarInt();
        Assert.Equal(0x61, packetId); // Set Entity Metadata packet ID
        
        // Read entity ID
        int readEntityId = reader.ReadVarInt();
        Assert.Equal(entityId, readEntityId);
        
        // Read metadata index 0 (Entity flags)
        byte index0 = reader.ReadByte();
        Assert.Equal(0, index0);
        int type0 = reader.ReadVarInt();
        Assert.Equal(0, type0); // Byte type
        byte flags = reader.ReadByte();
        Assert.Equal(0x02, flags); // Sneak bit (0x02) should be set
        
        // Read metadata index 6 (Pose - Living Entity metadata)
        byte index6 = reader.ReadByte();
        Assert.Equal(6, index6); // Pose is at index 6 for Living Entity
        int type6 = reader.ReadVarInt();
        Assert.Equal(20, type6); // Type 20 = Pose metadata type
        int pose = reader.ReadVarInt();
        Assert.Equal(5, pose); // SNEAKING = 5
        
        // Read terminator
        byte terminator = reader.ReadByte();
        Assert.Equal(0xFF, terminator);
    }

    [Fact]
    public void BuildSetEntityMetadataPacket_NotSneaking_IncludesCorrectFlags()
    {
        // Arrange
        int entityId = 456;
        bool isSneaking = false;

        // Act
        byte[] packet = PacketBuilder.BuildSetEntityMetadataPacket(entityId, isSneaking);

        // Assert
        Assert.NotNull(packet);
        Assert.True(packet.Length > 0);

        // Parse the packet to verify structure
        var reader = new ProtocolReader(packet);
        
        // Skip packet length
        reader.ReadVarInt();
        
        // Read packet ID
        int packetId = reader.ReadVarInt();
        Assert.Equal(0x61, packetId);
        
        // Read entity ID
        int readEntityId = reader.ReadVarInt();
        Assert.Equal(entityId, readEntityId);
        
        // Read metadata index 0 (Entity flags)
        byte index0 = reader.ReadByte();
        Assert.Equal(0, index0);
        int type0 = reader.ReadVarInt();
        Assert.Equal(0, type0); // Byte type
        byte flags = reader.ReadByte();
        Assert.Equal(0x00, flags); // No flags set when not sneaking
        
        // Read metadata index 6 (Pose - Living Entity metadata)
        byte index6 = reader.ReadByte();
        Assert.Equal(6, index6); // Pose is at index 6 for Living Entity
        int type6 = reader.ReadVarInt();
        Assert.Equal(20, type6); // Type 20 = Pose metadata type
        int pose = reader.ReadVarInt();
        Assert.Equal(0, pose); // STANDING = 0
        
        // Read terminator
        byte terminator = reader.ReadByte();
        Assert.Equal(0xFF, terminator);
    }

    [Fact]
    public void BuildSetEntityMetadataPacket_MultipleEntities_DifferentEntityIds()
    {
        // Arrange
        int entityId1 = 100;
        int entityId2 = 200;

        // Act
        byte[] packet1 = PacketBuilder.BuildSetEntityMetadataPacket(entityId1, true);
        byte[] packet2 = PacketBuilder.BuildSetEntityMetadataPacket(entityId2, true);

        // Assert
        Assert.NotNull(packet1);
        Assert.NotNull(packet2);
        
        // Parse both packets and verify entity IDs
        var reader1 = new ProtocolReader(packet1);
        reader1.ReadVarInt(); // Skip length
        reader1.ReadVarInt(); // Skip packet ID
        Assert.Equal(entityId1, reader1.ReadVarInt());
        
        var reader2 = new ProtocolReader(packet2);
        reader2.ReadVarInt(); // Skip length
        reader2.ReadVarInt(); // Skip packet ID
        Assert.Equal(entityId2, reader2.ReadVarInt());
    }
}

