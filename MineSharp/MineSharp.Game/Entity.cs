using MineSharp.Core.DataTypes;

namespace MineSharp.Game;

/// <summary>
/// Base class for all entities in the world.
/// </summary>
public abstract class Entity
{
    public int EntityId { get; set; }
    public Vector3 Position { get; set; }
    public Vector3 Velocity { get; set; }
    public bool OnGround { get; set; }

    protected Entity(int entityId)
    {
        EntityId = entityId;
        Position = new Vector3(0, 0, 0);
        Velocity = new Vector3(0, 0, 0);
        OnGround = false;
    }

    public virtual void Update(TimeSpan deltaTime)
    {
        // TODO: Implement entity update
        throw new NotImplementedException();
    }
}

