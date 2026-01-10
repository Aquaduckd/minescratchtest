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
    private Dictionary<int, string>? _itemProtocolIdToName;
    private Dictionary<string, int>? _blockDefaultStateIdByName;
    
    // Block breaking data
    private Dictionary<string, double>? _blockHardness; // block name -> hardness (-1 for unbreakable)
    private Dictionary<string, Dictionary<string, double>>? _toolSpeeds; // material -> tool_type -> speed
    private Dictionary<string, List<string>>? _mineableTags; // tool tag -> list of block names
    private Dictionary<int, string>? _blockStateIdToName; // block state ID -> block name

    public void LoadRegistries(string dataPath)
    {
        var registriesFile = Path.Combine(dataPath, "registries.json");
        var biomesFile = Path.Combine(dataPath, "biomes.json");
        var damageTypesFile = Path.Combine(dataPath, "damage_types.json");
        var registryDataFile = Path.Combine(dataPath, "registry_data.json");
        var blocksFile = Path.Combine(dataPath, "blocks.json");
        
        // Block breaking data files
        var blockHardnessFile = Path.Combine(dataPath, "block_hardness.json");
        var toolSpeedsFile = Path.Combine(dataPath, "tool_speeds_simplified.json");
        var mineableTagsFile = Path.Combine(dataPath, "mineable_tags.json");

        // Load registries.json (has protocol_id for each entry)
        if (File.Exists(registriesFile))
        {
            var registriesJson = DataLoader.LoadJson<Dictionary<string, JsonElement>>(registriesFile);
            _registries = new Dictionary<string, Dictionary<string, JsonElement>>();
            _itemProtocolIdToName = new Dictionary<int, string>();
            
            foreach (var kvp in registriesJson)
            {
                if (kvp.Value.TryGetProperty("entries", out var entries))
                {
                    var entryDict = new Dictionary<string, JsonElement>();
                    foreach (var entry in entries.EnumerateObject())
                    {
                        entryDict[entry.Name] = entry.Value;

                        // Build reverse lookup for items: protocol_id -> item identifier
                        if (kvp.Key == "minecraft:item")
                        {
                            if (entry.Value.TryGetProperty("protocol_id", out var pidEl))
                            {
                                int pid = pidEl.GetInt32();
                                if (!_itemProtocolIdToName.ContainsKey(pid))
                                {
                                    _itemProtocolIdToName[pid] = entry.Name;
                                }
                            }
                        }
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

        // Load blocks.json and precompute default state ids for each block
        if (File.Exists(blocksFile))
        {
            try
            {
                var blocksJson = DataLoader.LoadJson<Dictionary<string, JsonElement>>(blocksFile);
                _blockDefaultStateIdByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                foreach (var kvp in blocksJson)
                {
                    var blockName = kvp.Key; // e.g. "minecraft:cobblestone"
                    var blockObj = kvp.Value;
                    if (blockObj.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }
                    if (!blockObj.TryGetProperty("states", out var statesArray) || statesArray.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    int? defaultId = null;
                    int? firstId = null;
                    foreach (var state in statesArray.EnumerateArray())
                    {
                        if (state.TryGetProperty("id", out var idEl))
                        {
                            int id = idEl.GetInt32();
                            firstId ??= id;
                            if (state.TryGetProperty("default", out var defEl) && defEl.ValueKind == JsonValueKind.True)
                            {
                                defaultId = id;
                                break;
                            }
                        }
                    }

                    if (defaultId.HasValue)
                    {
                        _blockDefaultStateIdByName[blockName] = defaultId.Value;
                    }
                    else if (firstId.HasValue)
                    {
                        _blockDefaultStateIdByName[blockName] = firstId.Value;
                    }
                }
            }
            catch (Exception)
            {
                // If parsing fails, leave map null; callers must handle missing mapping
            }
        }
        
        // Load block_hardness.json
        if (File.Exists(blockHardnessFile))
        {
            try
            {
                _blockHardness = DataLoader.LoadJson<Dictionary<string, double>>(blockHardnessFile);
            }
            catch (Exception)
            {
                // If parsing fails, leave map null; callers must handle missing mapping
            }
        }
        
        // Load tool_speeds_simplified.json
        if (File.Exists(toolSpeedsFile))
        {
            try
            {
                _toolSpeeds = DataLoader.LoadJson<Dictionary<string, Dictionary<string, double>>>(toolSpeedsFile);
            }
            catch (Exception)
            {
                // If parsing fails, leave map null; callers must handle missing mapping
            }
        }
        
        // Load mineable_tags.json
        if (File.Exists(mineableTagsFile))
        {
            try
            {
                _mineableTags = DataLoader.LoadJson<Dictionary<string, List<string>>>(mineableTagsFile);
            }
            catch (Exception)
            {
                // If parsing fails, leave map null; callers must handle missing mapping
            }
        }
        
        // Build block state ID -> block name mapping from blocks.json
        if (File.Exists(blocksFile) && _blockDefaultStateIdByName != null)
        {
            try
            {
                var blocksJson = DataLoader.LoadJson<Dictionary<string, JsonElement>>(blocksFile);
                _blockStateIdToName = new Dictionary<int, string>();
                
                foreach (var kvp in blocksJson)
                {
                    var blockName = kvp.Key; // e.g. "minecraft:cobblestone"
                    var blockObj = kvp.Value;
                    if (blockObj.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }
                    if (!blockObj.TryGetProperty("states", out var statesArray) || statesArray.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }
                    
                    // Map all state IDs for this block to the block name
                    foreach (var state in statesArray.EnumerateArray())
                    {
                        if (state.TryGetProperty("id", out var idEl))
                        {
                            int stateId = idEl.GetInt32();
                            if (!_blockStateIdToName.ContainsKey(stateId))
                            {
                                _blockStateIdToName[stateId] = blockName;
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // If parsing fails, leave map null; callers must handle missing mapping
            }
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

    /// <summary>
    /// Gets the registry entry name for a given protocol ID within a registry.
    /// Currently implemented for the minecraft:item registry.
    /// </summary>
    public string? GetItemNameByProtocolId(int protocolId)
    {
        if (_itemProtocolIdToName != null && _itemProtocolIdToName.TryGetValue(protocolId, out var name))
        {
            return name;
        }
        return null;
    }

    /// <summary>
    /// Gets the default block state ID for a given block identifier (e.g., "minecraft:cobblestone").
    /// Returns null if the block is unknown or block state data was not loaded.
    /// </summary>
    public int? GetDefaultBlockStateIdByBlockName(string blockName)
    {
        if (_blockDefaultStateIdByName != null && _blockDefaultStateIdByName.TryGetValue(blockName, out var id))
        {
            return id;
        }
        return null;
    }

    /// <summary>
    /// Resolves an item protocol ID to a default block state ID, if the item corresponds to a placeable block.
    /// Returns null if the item does not map to a block.
    /// </summary>
    public int? ResolveBlockStateIdForItemProtocolId(int itemProtocolId)
    {
        // Lookup item identifier from protocol ID
        var itemName = GetItemNameByProtocolId(itemProtocolId);
        if (string.IsNullOrEmpty(itemName))
        {
            return null;
        }

        // Many block items share identical identifiers with blocks (e.g., "minecraft:stone")
        var blockStateId = GetDefaultBlockStateIdByBlockName(itemName);
        return blockStateId;
    }

    /// <summary>
    /// Gets the hardness value for a block.
    /// Returns null if the block is not found in the hardness data.
    /// Note: -1 indicates unbreakable (infinite hardness).
    /// </summary>
    public double? GetBlockHardness(string blockName)
    {
        if (_blockHardness != null && _blockHardness.TryGetValue(blockName, out var hardness))
        {
            return hardness;
        }
        return null;
    }

    /// <summary>
    /// Checks if a block is unbreakable (hardness = -1).
    /// Returns false if the block is not found or has a valid hardness value.
    /// </summary>
    public bool IsBlockUnbreakable(string blockName)
    {
        var hardness = GetBlockHardness(blockName);
        return hardness.HasValue && hardness.Value == -1;
    }

    /// <summary>
    /// Gets the tool speed multiplier for a given material and tool type.
    /// Returns null if the material/tool combination is not found.
    /// </summary>
    /// <param name="material">Tool material (e.g., "iron", "diamond", "wooden")</param>
    /// <param name="toolType">Tool type (e.g., "pickaxe", "axe", "shovel", "hoe")</param>
    /// <returns>Speed multiplier, or null if not found</returns>
    public double? GetToolSpeed(string material, string toolType)
    {
        if (_toolSpeeds != null && _toolSpeeds.TryGetValue(material, out var toolTypeSpeeds))
        {
            if (toolTypeSpeeds.TryGetValue(toolType, out var speed))
            {
                return speed;
            }
        }
        return null;
    }

    /// <summary>
    /// Gets the tool speed from an item name (e.g., "minecraft:iron_pickaxe" -> 6.0).
    /// Parses the item name to extract material and tool type.
    /// Returns 1.0 for hand/no tool if item name doesn't match a tool pattern.
    /// </summary>
    /// <param name="itemName">Item identifier (e.g., "minecraft:iron_pickaxe")</param>
    /// <returns>Speed multiplier (defaults to 1.0 for hand)</returns>
    public double GetToolSpeedFromItemName(string itemName)
    {
        if (string.IsNullOrEmpty(itemName))
        {
            return 1.0; // Hand speed
        }

        // Remove "minecraft:" prefix if present
        var name = itemName.StartsWith("minecraft:") ? itemName.Substring(10) : itemName;
        
        // Try to extract material and tool type from name (e.g., "iron_pickaxe" -> material="iron", type="pickaxe")
        var parts = name.Split('_', 2);
        if (parts.Length == 2)
        {
            var material = parts[0]; // e.g., "iron"
            var toolType = parts[1]; // e.g., "pickaxe"
            
            var speed = GetToolSpeed(material, toolType);
            if (speed.HasValue)
            {
                return speed.Value;
            }
        }
        
        // Special case: shears
        if (name == "shears" || itemName == "minecraft:shears")
        {
            // Shears have varying speeds, but default to 2.0 for general use
            return 2.0;
        }
        
        // Default to hand speed
        return 1.0;
    }

    /// <summary>
    /// Checks if a tool can mine a specific block based on mineable tags.
    /// Returns true if the tool is in the mineable tags for that block.
    /// </summary>
    /// <param name="toolName">Tool identifier (e.g., "minecraft:iron_pickaxe")</param>
    /// <param name="blockName">Block identifier (e.g., "minecraft:stone")</param>
    /// <returns>True if the tool can mine the block, false otherwise</returns>
    public bool CanToolMineBlock(string toolName, string blockName)
    {
        if (_mineableTags == null || string.IsNullOrEmpty(toolName) || string.IsNullOrEmpty(blockName))
        {
            return false;
        }

        // Determine tool type from tool name
        string toolType;
        if (toolName.Contains("pickaxe"))
        {
            toolType = "mineable/pickaxe";
        }
        else if (toolName.Contains("axe"))
        {
            toolType = "mineable/axe";
        }
        else if (toolName.Contains("shovel"))
        {
            toolType = "mineable/shovel";
        }
        else if (toolName.Contains("hoe"))
        {
            toolType = "mineable/hoe";
        }
        else
        {
            // Hand or other tools - check if block is in any mineable tag
            // For MVP, assume hand can "mine" any block (though slowly)
            return true;
        }

        // Check if block is in the mineable tag for this tool type
        if (_mineableTags.TryGetValue(toolType, out var mineableBlocks))
        {
            return mineableBlocks.Contains(blockName);
        }

        return false;
    }

    /// <summary>
    /// Gets the block name from a block state ID.
    /// Returns null if the state ID is not found in the mapping.
    /// </summary>
    /// <param name="blockStateId">Block state ID</param>
    /// <returns>Block identifier (e.g., "minecraft:stone"), or null if not found</returns>
    public string? GetBlockName(int blockStateId)
    {
        if (_blockStateIdToName != null && _blockStateIdToName.TryGetValue(blockStateId, out var blockName))
        {
            return blockName;
        }
        return null;
    }

    /// <summary>
    /// Gets the list of blocks that can be mined by a specific tool type.
    /// Returns an empty list if the tool type is not found.
    /// </summary>
    /// <param name="toolType">Tool tag (e.g., "mineable/pickaxe")</param>
    /// <returns>List of block identifiers that can be mined by this tool</returns>
    public List<string> GetBlocksMineableByTool(string toolType)
    {
        if (_mineableTags != null && _mineableTags.TryGetValue(toolType, out var blocks))
        {
            return new List<string>(blocks);
        }
        return new List<string>();
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

