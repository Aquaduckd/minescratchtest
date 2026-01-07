using System.Text.Json;

namespace MineSharp.Data;

/// <summary>
/// Manages registry data loading and access.
/// </summary>
public class RegistryManager
{
    private Dictionary<string, Dictionary<string, JsonElement>>? _registries;
    private List<string>? _biomes;
    private List<string>? _damageTypes;
    private Dictionary<string, Dictionary<string, JsonElement>>? _registryData;

    public void LoadRegistries(string dataPath)
    {
        var registriesFile = Path.Combine(dataPath, "registries.json");
        var biomesFile = Path.Combine(dataPath, "biomes.json");
        var damageTypesFile = Path.Combine(dataPath, "damage_types.json");
        var registryDataFile = Path.Combine(dataPath, "registry_data.json");

        // Load registries.json (has protocol_id for each entry)
        if (File.Exists(registriesFile))
        {
            var registriesJson = DataLoader.LoadJson<Dictionary<string, JsonElement>>(registriesFile);
            _registries = new Dictionary<string, Dictionary<string, JsonElement>>();
            
            foreach (var kvp in registriesJson)
            {
                if (kvp.Value.TryGetProperty("entries", out var entries))
                {
                    var entryDict = new Dictionary<string, JsonElement>();
                    foreach (var entry in entries.EnumerateObject())
                    {
                        entryDict[entry.Name] = entry.Value;
                    }
                    _registries[kvp.Key] = entryDict;
                }
            }
        }

        // Load biomes.json (simple array)
        if (File.Exists(biomesFile))
        {
            _biomes = DataLoader.LoadJson<List<string>>(biomesFile);
        }

        // Load damage_types.json (simple array)
        if (File.Exists(damageTypesFile))
        {
            _damageTypes = DataLoader.LoadJson<List<string>>(damageTypesFile);
        }

        // Load registry_data.json (has nested structure for variant registries)
        if (File.Exists(registryDataFile))
        {
            _registryData = DataLoader.LoadJson<Dictionary<string, Dictionary<string, JsonElement>>>(registryDataFile);
        }
    }

    public List<(string EntryId, byte[]? NbtData)> GetRegistryEntries(string registryId)
    {
        // Special handling for biome registry
        if (registryId == "minecraft:worldgen/biome")
        {
            if (_biomes != null && _biomes.Count > 0)
            {
                return _biomes.Select(b => (b, (byte[]?)null)).ToList();
            }
            // Fallback: at least include plains
            return new List<(string, byte[]?)> { ("minecraft:plains", null) };
        }

        // Special handling for damage_type registry
        if (registryId == "minecraft:damage_type")
        {
            if (_damageTypes != null && _damageTypes.Count > 0)
            {
                return _damageTypes.Select(d => (d, (byte[]?)null)).ToList();
            }
            // Fallback: at least include in_fire (required)
            return new List<(string, byte[]?)> { ("minecraft:in_fire", null) };
        }

        // Try registries.json first (has protocol_id)
        if (_registries != null && _registries.TryGetValue(registryId, out var entries))
        {
            return entries.Keys.Select(e => (e, (byte[]?)null)).ToList();
        }

        // Try registry_data.json (has nested structure for variants)
        if (_registryData != null && _registryData.TryGetValue(registryId, out var variantEntries))
        {
            return variantEntries.Keys.Select(e => (e, (byte[]?)null)).ToList();
        }

        // Fallback for registries not in JSON
        var fallbacks = new Dictionary<string, List<(string, byte[]?)>>
        {
            ["minecraft:dimension_type"] = new List<(string, byte[]?)> { ("minecraft:overworld", null) },
            ["minecraft:cat_variant"] = new List<(string, byte[]?)>
            {
                ("minecraft:persian", null), ("minecraft:british_shorthair", null),
                ("minecraft:siamese", null), ("minecraft:ragdoll", null),
                ("minecraft:jellie", null), ("minecraft:black", null),
                ("minecraft:red", null), ("minecraft:tabby", null),
                ("minecraft:all_black", null), ("minecraft:calico", null),
                ("minecraft:white", null)
            },
            ["minecraft:frog_variant"] = new List<(string, byte[]?)>
            {
                ("minecraft:temperate", null), ("minecraft:warm", null), ("minecraft:cold", null)
            },
            ["minecraft:chicken_variant"] = new List<(string, byte[]?)>
            {
                ("minecraft:cold", null), ("minecraft:temperate", null), ("minecraft:warm", null)
            },
            ["minecraft:cow_variant"] = new List<(string, byte[]?)>
            {
                ("minecraft:cold", null), ("minecraft:temperate", null), ("minecraft:warm", null)
            },
            ["minecraft:pig_variant"] = new List<(string, byte[]?)>
            {
                ("minecraft:cold", null), ("minecraft:temperate", null), ("minecraft:warm", null)
            },
            ["minecraft:wolf_sound_variant"] = new List<(string, byte[]?)>
            {
                ("minecraft:angry", null), ("minecraft:big", null), ("minecraft:classic", null),
                ("minecraft:cute", null), ("minecraft:grumpy", null), ("minecraft:puglin", null),
                ("minecraft:sad", null)
            },
            ["minecraft:painting_variant"] = new List<(string, byte[]?)>
            {
                ("minecraft:kebab", null), ("minecraft:aztec", null), ("minecraft:alban", null),
                ("minecraft:aztec2", null), ("minecraft:bomb", null), ("minecraft:plant", null),
                ("minecraft:wasteland", null), ("minecraft:pool", null), ("minecraft:courbet", null),
                ("minecraft:sea", null), ("minecraft:sunset", null), ("minecraft:creebet", null),
                ("minecraft:wanderer", null), ("minecraft:graham", null), ("minecraft:match", null),
                ("minecraft:bust", null), ("minecraft:stage", null), ("minecraft:void", null),
                ("minecraft:skull_and_roses", null), ("minecraft:wither", null), ("minecraft:fighters", null),
                ("minecraft:pointer", null), ("minecraft:pigscene", null), ("minecraft:burning_skull", null),
                ("minecraft:skeleton", null), ("minecraft:donkey_kong", null)
            },
            ["minecraft:wolf_variant"] = new List<(string, byte[]?)>
            {
                ("minecraft:striped", null), ("minecraft:chestnut", null), ("minecraft:rusty", null),
                ("minecraft:spotted", null), ("minecraft:snowy", null), ("minecraft:black", null),
                ("minecraft:ashen", null), ("minecraft:pale", null), ("minecraft:woods", null)
            }
        };

        return fallbacks.GetValueOrDefault(registryId, new List<(string, byte[]?)>());
    }

    /// <summary>
    /// Gets the protocol ID for a specific entry in a registry.
    /// </summary>
    /// <param name="registryId">The registry ID (e.g., "minecraft:entity_type")</param>
    /// <param name="entryId">The entry ID (e.g., "minecraft:player")</param>
    /// <returns>The protocol ID if found, null otherwise</returns>
    public int? GetRegistryEntryProtocolId(string registryId, string entryId)
    {
        if (_registries != null && _registries.TryGetValue(registryId, out var entries))
        {
            if (entries.TryGetValue(entryId, out var entry))
            {
                if (entry.TryGetProperty("protocol_id", out var protocolIdElement))
                {
                    return protocolIdElement.GetInt32();
                }
            }
        }
        return null;
    }

    public List<string> GetRequiredRegistryIds()
    {
        return new List<string>
        {
            "minecraft:dimension_type",
            "minecraft:cat_variant",
            "minecraft:chicken_variant",
            "minecraft:cow_variant",
            "minecraft:frog_variant",
            "minecraft:painting_variant",
            "minecraft:pig_variant",
            "minecraft:wolf_variant",
            "minecraft:wolf_sound_variant",
            "minecraft:worldgen/biome",
            "minecraft:damage_type"
        };
    }
}

