namespace MineSharp.Core.Models;

/// <summary>
/// Represents a game profile for player authentication.
/// </summary>
public class GameProfile
{
    public Guid Uuid { get; set; }
    public string Username { get; set; } = string.Empty;
    public List<Property> Properties { get; set; } = new();

    public class Property
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string? Signature { get; set; }
    }
}

