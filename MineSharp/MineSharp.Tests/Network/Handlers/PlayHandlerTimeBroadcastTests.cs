using MineSharp.Core.Protocol;
using MineSharp.Data;
using MineSharp.Network;
using MineSharp.Network.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace MineSharp.Tests.Network.Handlers;

public class PlayHandlerTimeBroadcastTests
{
    [Fact]
    public async Task BroadcastUpdateTimeAsync_WithNoConnections_ShouldNotThrow()
    {
        // Arrange
        var world = new MineSharp.World.World();
        Func<IEnumerable<ClientConnection>> getAllConnections = () => Enumerable.Empty<ClientConnection>();
        var playHandler = new PlayHandler(world, getAllConnections);
        
        // Act & Assert - Should not throw
        await playHandler.BroadcastUpdateTimeAsync();
    }

    [Fact]
    public async Task BroadcastUpdateTimeAsync_WithNullWorld_ShouldNotThrow()
    {
        // Arrange
        Func<IEnumerable<ClientConnection>> getAllConnections = () => Enumerable.Empty<ClientConnection>();
        var playHandler = new PlayHandler(null, getAllConnections);
        
        // Act & Assert - Should not throw
        await playHandler.BroadcastUpdateTimeAsync();
    }

    [Fact]
    public async Task BroadcastUpdateTimeAsync_WithNullConnectionGetter_ShouldNotThrow()
    {
        // Arrange
        var world = new MineSharp.World.World();
        var playHandler = new PlayHandler(world, null);
        
        // Act & Assert - Should not throw
        await playHandler.BroadcastUpdateTimeAsync();
    }

    [Fact]
    public async Task BroadcastUpdateTimeAsync_WithWorld_ShouldGetCurrentTime()
    {
        // Arrange
        var world = new MineSharp.World.World();
        
        // Advance time a few ticks
        world.Tick(TimeSpan.FromMilliseconds(50));
        world.Tick(TimeSpan.FromMilliseconds(50));
        world.Tick(TimeSpan.FromMilliseconds(50));
        
        Func<IEnumerable<ClientConnection>> getAllConnections = () => Enumerable.Empty<ClientConnection>();
        var playHandler = new PlayHandler(world, getAllConnections);
        
        // Act
        await playHandler.BroadcastUpdateTimeAsync();
        
        // Assert - Verify time was advanced
        Assert.Equal(3, world.TimeManager.WorldAge);
        Assert.Equal(6003, world.TimeManager.TimeOfDay); // Started at 6000 (noon)
    }
}

