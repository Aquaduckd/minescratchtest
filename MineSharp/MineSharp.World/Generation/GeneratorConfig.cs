namespace MineSharp.World.Generation;

/// <summary>
/// Configuration dictionary for generator-specific settings.
/// </summary>
public class GeneratorConfig : Dictionary<string, object>
{
}

/// <summary>
/// Schema definition for generator configuration.
/// </summary>
public class GeneratorConfigSchema
{
    public Dictionary<string, ConfigProperty> Properties { get; set; } = new();
}

/// <summary>
/// Property definition for generator configuration schema.
/// </summary>
public class ConfigProperty
{
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public object? DefaultValue { get; set; }
}

