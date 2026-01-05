using MineSharp.Core.Protocol.PacketTypes;
using Xunit;

namespace MineSharp.Tests.Protocol;

/// <summary>
/// Tests for Keep Alive packet structure and validation.
/// Note: Handler tests that require full ClientConnection are deferred until we have
/// proper mocking infrastructure. These tests focus on packet structure validation.
/// </summary>
public class KeepAliveHandlerTests
{
    [Fact]
    public void KeepAlivePacket_Structure_IsValid()
    {
        // Arrange
        var packet = new KeepAlivePacket
        {
            KeepAliveId = 1234567890123L
        };

        // Assert
        Assert.Equal(1234567890123L, packet.KeepAliveId);
    }

    [Fact]
    public void KeepAlivePacket_CanSetAndGetId()
    {
        // Arrange
        var packet = new KeepAlivePacket();
        var keepAliveId = 9876543210987L;

        // Act
        packet.KeepAliveId = keepAliveId;

        // Assert
        Assert.Equal(keepAliveId, packet.KeepAliveId);
    }

    [Fact]
    public void KeepAlivePacket_ZeroId_IsValid()
    {
        // Arrange & Act
        var packet = new KeepAlivePacket
        {
            KeepAliveId = 0L
        };

        // Assert
        Assert.Equal(0L, packet.KeepAliveId);
    }

    [Fact]
    public void KeepAlivePacket_LargeId_IsValid()
    {
        // Arrange & Act
        var packet = new KeepAlivePacket
        {
            KeepAliveId = long.MaxValue
        };

        // Assert
        Assert.Equal(long.MaxValue, packet.KeepAliveId);
    }
}

