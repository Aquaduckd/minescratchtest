using System;
using MineSharp.Core.Protocol;
using Xunit;

namespace MineSharp.Tests.Protocol;

public class EntityPacketBuilderTests
{
    [Fact]
    public void BuildSpawnEntityPacket_CreatesValidPacket()
    {
        // Arrange
        int entityId = 1;
        Guid uuid = Guid.NewGuid();
        int entityType = 100; // Example entity type ID
        double x = 10.5;
        double y = 64.0;
        double z = 20.3;
        float pitch = 0.0f;
        float yaw = 45.0f;
        float headYaw = 45.0f;

        // Act
        byte[] packet = PacketBuilder.BuildSpawnEntityPacket(
            entityId, uuid, entityType, x, y, z,
            velocityX: 0.0, velocityY: 0.0, velocityZ: 0.0,
            pitch, yaw, headYaw, data: 0);

        // Assert
        Assert.NotNull(packet);
        Assert.True(packet.Length > 0);
        // Packet should start with length prefix, then packet ID 0x01
        Assert.True(packet.Length >= 2);
    }

    [Fact]
    public void BuildSpawnEntityPacket_WithZeroVelocity_CreatesValidPacket()
    {
        // Arrange
        int entityId = 1;
        Guid uuid = Guid.NewGuid();
        int entityType = 100;

        // Act
        byte[] packet = PacketBuilder.BuildSpawnEntityPacket(
            entityId, uuid, entityType, 0.0, 64.0, 0.0);

        // Assert
        Assert.NotNull(packet);
        Assert.True(packet.Length > 0);
    }

    [Fact]
    public void BuildRemoveEntitiesPacket_CreatesValidPacket()
    {
        // Arrange
        int[] entityIds = { 1, 2, 3 };

        // Act
        byte[] packet = PacketBuilder.BuildRemoveEntitiesPacket(entityIds);

        // Assert
        Assert.NotNull(packet);
        Assert.True(packet.Length > 0);
    }

    [Fact]
    public void BuildRemoveEntitiesPacket_WithSingleEntity_CreatesValidPacket()
    {
        // Arrange
        int[] entityIds = { 5 };

        // Act
        byte[] packet = PacketBuilder.BuildRemoveEntitiesPacket(entityIds);

        // Assert
        Assert.NotNull(packet);
        Assert.True(packet.Length > 0);
    }

    [Fact]
    public void BuildRemoveEntitiesPacket_ThrowsWhenEmpty()
    {
        // Arrange
        int[] entityIds = Array.Empty<int>();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => PacketBuilder.BuildRemoveEntitiesPacket(entityIds));
    }

    [Fact]
    public void BuildRemoveEntitiesPacket_ThrowsWhenNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => PacketBuilder.BuildRemoveEntitiesPacket(null!));
    }

    [Fact]
    public void BuildUpdateEntityPositionPacket_CreatesValidPacket()
    {
        // Arrange
        int entityId = 1;
        short deltaX = 100; // (0.0244 blocks * 4096)
        short deltaY = 200;
        short deltaZ = 150;
        bool onGround = true;

        // Act
        byte[] packet = PacketBuilder.BuildUpdateEntityPositionPacket(
            entityId, deltaX, deltaY, deltaZ, onGround);

        // Assert
        Assert.NotNull(packet);
        Assert.True(packet.Length > 0);
    }

    [Fact]
    public void BuildUpdateEntityPositionAndRotationPacket_CreatesValidPacket()
    {
        // Arrange
        int entityId = 1;
        short deltaX = 100;
        short deltaY = 200;
        short deltaZ = 150;
        float yaw = 90.0f;
        float pitch = 45.0f;
        bool onGround = true;

        // Act
        byte[] packet = PacketBuilder.BuildUpdateEntityPositionAndRotationPacket(
            entityId, deltaX, deltaY, deltaZ, yaw, pitch, onGround);

        // Assert
        Assert.NotNull(packet);
        Assert.True(packet.Length > 0);
    }

    [Fact]
    public void BuildUpdateEntityRotationPacket_CreatesValidPacket()
    {
        // Arrange
        int entityId = 1;
        float yaw = 180.0f;
        float pitch = -45.0f;
        bool onGround = false;

        // Act
        byte[] packet = PacketBuilder.BuildUpdateEntityRotationPacket(
            entityId, yaw, pitch, onGround);

        // Assert
        Assert.NotNull(packet);
        Assert.True(packet.Length > 0);
    }

    [Fact]
    public void BuildTeleportEntityPacket_CreatesValidPacket()
    {
        // Arrange
        int entityId = 1;
        double x = 100.5;
        double y = 65.0;
        double z = 200.3;
        float yaw = 270.0f;
        float pitch = 30.0f;
        bool onGround = true;

        // Act
        byte[] packet = PacketBuilder.BuildTeleportEntityPacket(
            entityId, x, y, z, yaw, pitch, onGround);

        // Assert
        Assert.NotNull(packet);
        Assert.True(packet.Length > 0);
    }

    [Fact]
    public void BuildTeleportEntityPacket_WithLargeCoordinates_CreatesValidPacket()
    {
        // Arrange
        int entityId = 1;
        double x = 10000.0;
        double y = 100.0;
        double z = 5000.0;

        // Act
        byte[] packet = PacketBuilder.BuildTeleportEntityPacket(
            entityId, x, y, z, 0.0f, 0.0f, true);

        // Assert
        Assert.NotNull(packet);
        Assert.True(packet.Length > 0);
    }

    [Fact]
    public void BuildSpawnEntityPacket_DifferentEntityIds_CreatesDifferentPackets()
    {
        // Arrange
        Guid uuid = Guid.NewGuid();
        int entityType = 100;

        // Act
        byte[] packet1 = PacketBuilder.BuildSpawnEntityPacket(1, uuid, entityType, 0, 64, 0);
        byte[] packet2 = PacketBuilder.BuildSpawnEntityPacket(2, uuid, entityType, 0, 64, 0);

        // Assert
        Assert.NotEqual(packet1, packet2);
    }

    [Fact]
    public void BuildSpawnEntityPacket_DifferentPositions_CreatesDifferentPackets()
    {
        // Arrange
        Guid uuid = Guid.NewGuid();
        int entityId = 1;
        int entityType = 100;

        // Act
        byte[] packet1 = PacketBuilder.BuildSpawnEntityPacket(entityId, uuid, entityType, 0, 64, 0);
        byte[] packet2 = PacketBuilder.BuildSpawnEntityPacket(entityId, uuid, entityType, 10, 65, 20);

        // Assert
        Assert.NotEqual(packet1, packet2);
    }

    [Fact]
    public void BuildRotateHeadPacket_CreatesValidPacket()
    {
        // Arrange
        int entityId = 1;
        float headYaw = 45.0f;

        // Act
        byte[] packet = PacketBuilder.BuildRotateHeadPacket(entityId, headYaw);

        // Assert
        Assert.NotNull(packet);
        Assert.True(packet.Length > 0);
        // Packet should start with length prefix, then packet ID 0x51
        Assert.True(packet.Length >= 2);
    }

    [Fact]
    public void BuildRotateHeadPacket_WithZeroHeadYaw_CreatesValidPacket()
    {
        // Arrange
        int entityId = 1;
        float headYaw = 0.0f;

        // Act
        byte[] packet = PacketBuilder.BuildRotateHeadPacket(entityId, headYaw);

        // Assert
        Assert.NotNull(packet);
        Assert.True(packet.Length > 0);
    }

    [Fact]
    public void BuildRotateHeadPacket_WithNegativeHeadYaw_CreatesValidPacket()
    {
        // Arrange
        int entityId = 1;
        float headYaw = -45.0f;

        // Act
        byte[] packet = PacketBuilder.BuildRotateHeadPacket(entityId, headYaw);

        // Assert
        Assert.NotNull(packet);
        Assert.True(packet.Length > 0);
    }

    [Fact]
    public void BuildRotateHeadPacket_WithLargeHeadYaw_CreatesValidPacket()
    {
        // Arrange
        int entityId = 1;
        float headYaw = 360.0f;

        // Act
        byte[] packet = PacketBuilder.BuildRotateHeadPacket(entityId, headYaw);

        // Assert
        Assert.NotNull(packet);
        Assert.True(packet.Length > 0);
    }

    [Fact]
    public void BuildRotateHeadPacket_DifferentEntityIds_CreatesDifferentPackets()
    {
        // Arrange
        float headYaw = 45.0f;
        int entityId1 = 1;
        int entityId2 = 2;

        // Act
        byte[] packet1 = PacketBuilder.BuildRotateHeadPacket(entityId1, headYaw);
        byte[] packet2 = PacketBuilder.BuildRotateHeadPacket(entityId2, headYaw);

        // Assert
        Assert.NotNull(packet1);
        Assert.NotNull(packet2);
        // Packets should be different due to different entity IDs
        Assert.NotEqual(packet1, packet2);
    }

    [Fact]
    public void BuildRotateHeadPacket_DifferentHeadYaw_CreatesDifferentPackets()
    {
        // Arrange
        int entityId = 1;
        float headYaw1 = 45.0f;
        float headYaw2 = 90.0f;

        // Act
        byte[] packet1 = PacketBuilder.BuildRotateHeadPacket(entityId, headYaw1);
        byte[] packet2 = PacketBuilder.BuildRotateHeadPacket(entityId, headYaw2);

        // Assert
        Assert.NotNull(packet1);
        Assert.NotNull(packet2);
        // Packets should be different due to different head yaw values
        Assert.NotEqual(packet1, packet2);
    }
}

