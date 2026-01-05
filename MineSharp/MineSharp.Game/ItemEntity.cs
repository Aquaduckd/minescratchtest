using MineSharp.Core.DataTypes;

namespace MineSharp.Game;

/// <summary>
/// Represents an item entity (dropped item).
/// </summary>
public class ItemEntity : Entity
{
    public int ItemId { get; set; }
    public int Count { get; set; }
    public DateTime SpawnTime { get; set; }
    public DateTime LastUpdateTime { get; set; }
    public int PickupDelay { get; set; }
    public int Age { get; set; }

    public ItemEntity(int entityId, int itemId, int count, Vector3 position, Vector3 velocity) 
        : base(entityId)
    {
        ItemId = itemId;
        Count = count;
        Position = position;
        Velocity = velocity;
        SpawnTime = DateTime.UtcNow;
        LastUpdateTime = DateTime.UtcNow;
        PickupDelay = 10;  // 10 ticks = 0.5 seconds
        Age = 0;
    }

    public override void Update(TimeSpan deltaTime)
    {
        // TODO: Implement item entity update (gravity, collision, etc.)
        throw new NotImplementedException();
    }

    public bool CanPickup()
    {
        // TODO: Implement pickup check
        throw new NotImplementedException();
    }
}

