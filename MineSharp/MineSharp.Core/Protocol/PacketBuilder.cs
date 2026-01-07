using MineSharp.Core.DataTypes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MineSharp.Core.Protocol;

/// <summary>
/// Builds outgoing packets to send to clients.
/// </summary>
public class PacketBuilder
{
    private static int GetBitLength(int value)
    {
        if (value == 0) return 1;
        int bits = 0;
        while (value > 0)
        {
            bits++;
            value >>= 1;
        }
        return bits;
    }
    public static byte[] BuildLoginSuccessPacket(Guid uuid, string username, List<object> properties)
    {
        // Build packet data (without length prefix)
        var packetWriter = new ProtocolWriter();
        packetWriter.WriteVarInt(0x02); // Login Success packet ID
        
        // Write Game Profile
        packetWriter.WriteUuid(uuid);
        packetWriter.WriteString(username, 16);
        
        // Write properties array (empty for offline mode)
        packetWriter.WriteVarInt(properties?.Count ?? 0);
        // TODO: Write properties if needed (for online mode)
        
        // Build final packet with length prefix
        var packetData = packetWriter.ToArray();
        var finalWriter = new ProtocolWriter();
        finalWriter.WriteVarInt(packetData.Length);
        finalWriter.WriteBytes(packetData);
        
        return finalWriter.ToArray();
    }

    public static byte[] BuildKnownPacksPacket(List<(string Namespace, string PackId, string Version)>? packs = null)
    {
        if (packs == null || packs.Count == 0)
        {
            packs = new List<(string, string, string)> { ("minecraft", "core", "1.21.10") };
        }

        var packetWriter = new ProtocolWriter();
        packetWriter.WriteVarInt(0x0E); // Clientbound Known Packs packet ID
        
        // Known Packs array
        packetWriter.WriteVarInt(packs.Count);
        foreach (var pack in packs)
        {
            packetWriter.WriteString(pack.Namespace, 32767);
            packetWriter.WriteString(pack.PackId, 32767);
            packetWriter.WriteString(pack.Version, 32767);
        }
        
        // Build final packet with length prefix
        var packetData = packetWriter.ToArray();
        var finalWriter = new ProtocolWriter();
        finalWriter.WriteVarInt(packetData.Length);
        finalWriter.WriteBytes(packetData);
        
        return finalWriter.ToArray();
    }

    public static byte[] BuildRegistryDataPacket(string registryId, List<(string EntryId, byte[]? NbtData)> entries)
    {
        var packetWriter = new ProtocolWriter();
        packetWriter.WriteVarInt(0x07); // Registry Data packet ID
        
        // Registry ID (Identifier)
        packetWriter.WriteString(registryId, 32767);
        
        // Entries array
        packetWriter.WriteVarInt(entries.Count);
        foreach (var entry in entries)
        {
            // Entry ID (Identifier)
            packetWriter.WriteString(entry.EntryId, 32767);
            
            // Data (Prefixed Optional NBT)
            // Prefixed Optional: boolean (present?) + data if present
            if (entry.NbtData != null && entry.NbtData.Length > 0)
            {
                packetWriter.WriteBool(true);
                packetWriter.WriteBytes(entry.NbtData);
            }
            else
            {
                packetWriter.WriteBool(false); // Omit NBT data
            }
        }
        
        // Build final packet with length prefix
        var packetData = packetWriter.ToArray();
        var finalWriter = new ProtocolWriter();
        finalWriter.WriteVarInt(packetData.Length);
        finalWriter.WriteBytes(packetData);
        
        return finalWriter.ToArray();
    }

    public static byte[] BuildFinishConfigurationPacket()
    {
        // Finish Configuration is packet ID 0x03 with no fields
        var packetWriter = new ProtocolWriter();
        packetWriter.WriteVarInt(0x03); // Finish Configuration packet ID
        
        var packetData = packetWriter.ToArray();
        var finalWriter = new ProtocolWriter();
        finalWriter.WriteVarInt(packetData.Length);
        finalWriter.WriteBytes(packetData);
        
        return finalWriter.ToArray();
    }

    public static byte[] BuildLoginPlayPacket(
        int entityId = 1,
        bool isHardcore = false,
        List<string>? dimensionNames = null,
        int maxPlayers = 20,
        int viewDistance = 10,
        int simulationDistance = 10,
        bool reducedDebugInfo = false,
        bool enableRespawnScreen = true,
        bool doLimitedCrafting = false,
        int dimensionType = 0, // 0 = overworld in dimension_type registry
        string dimensionName = "minecraft:overworld",
        long hashedSeed = 0,
        byte gameMode = 0, // 0=Survival
        sbyte previousGameMode = -1, // -1=Undefined
        bool isDebug = false,
        bool isFlat = false,
        bool hasDeathLocation = false,
        int portalCooldown = 0,
        int seaLevel = 63,
        bool enforcesSecureChat = false)
    {
        if (dimensionNames == null || dimensionNames.Count == 0)
        {
            dimensionNames = new List<string> { "minecraft:overworld" };
        }

        var packetWriter = new ProtocolWriter();
        packetWriter.WriteVarInt(0x30); // Login (play) packet ID
        
        // Entity ID
        packetWriter.WriteInt(entityId);
        
        // Is hardcore
        packetWriter.WriteBool(isHardcore);
        
        // Dimension Names (array of identifiers)
        packetWriter.WriteVarInt(dimensionNames.Count);
        foreach (var dimName in dimensionNames)
        {
            packetWriter.WriteString(dimName, 32767);
        }
        
        // Max Players
        packetWriter.WriteVarInt(maxPlayers);
        
        // View Distance
        packetWriter.WriteVarInt(viewDistance);
        
        // Simulation Distance
        packetWriter.WriteVarInt(simulationDistance);
        
        // Reduced Debug Info
        packetWriter.WriteBool(reducedDebugInfo);
        
        // Enable respawn screen
        packetWriter.WriteBool(enableRespawnScreen);
        
        // Do limited crafting
        packetWriter.WriteBool(doLimitedCrafting);
        
        // Dimension Type
        packetWriter.WriteVarInt(dimensionType);
        
        // Dimension Name
        packetWriter.WriteString(dimensionName, 32767);
        
        // Hashed seed
        packetWriter.WriteLong(hashedSeed);
        
        // Game mode
        packetWriter.WriteByte(gameMode);
        
        // Previous Game mode
        packetWriter.WriteByte((byte)previousGameMode);
        
        // Is Debug
        packetWriter.WriteBool(isDebug);
        
        // Is Flat
        packetWriter.WriteBool(isFlat);
        
        // Has death location
        packetWriter.WriteBool(hasDeathLocation);
        
        // Death dimension name and location (only if has_death_location is true)
        // Skipped for minimal implementation
        
        // Portal cooldown
        packetWriter.WriteVarInt(portalCooldown);
        
        // Sea level
        packetWriter.WriteVarInt(seaLevel);
        
        // Enforces Secure Chat
        packetWriter.WriteBool(enforcesSecureChat);
        
        // Build final packet with length prefix
        var packetData = packetWriter.ToArray();
        var finalWriter = new ProtocolWriter();
        finalWriter.WriteVarInt(packetData.Length);
        finalWriter.WriteBytes(packetData);
        
        return finalWriter.ToArray();
    }


    public static byte[] BuildChunkDataPacket(int chunkX, int chunkZ, dynamic blockManager)
    {
        var packetWriter = new ProtocolWriter();
        packetWriter.WriteVarInt(0x2C); // Chunk Data and Update Light packet ID
        
        // Chunk coordinates
        packetWriter.WriteInt(chunkX);
        packetWriter.WriteInt(chunkZ);
        
        // Heightmaps: Generate MOTION_BLOCKING heightmap
        // Heightmap format: Prefixed Array of Heightmap
        // Each heightmap: Type (VarInt) + Data (Prefixed Array of Long)
        // Bits per entry = 9 (for overworld: -64 to 320 = 384 blocks, ceil(log2(385)) = 9)
        
        // Send MOTION_BLOCKING heightmap (Type = 4)
        packetWriter.WriteVarInt(1); // Number of heightmaps
        
        // Heightmap Type: MOTION_BLOCKING = 4
        packetWriter.WriteVarInt(4);
        
        // Generate heightmap data (256 entries, one per column)
        int[] heightmapEntries = blockManager.GenerateHeightmap(chunkX, chunkZ);
        
        // Pack heightmap into longs using 9 bits per entry
        const int heightmapBitsPerEntry = 9;
        const int entriesPerLong = 64 / heightmapBitsPerEntry; // 7 entries per long
        int numLongs = (256 + entriesPerLong - 1) / entriesPerLong; // Ceiling division
        
        // Write length (number of longs)
        packetWriter.WriteVarInt(numLongs);
        
        // Pack heightmap data
        for (int longIdx = 0; longIdx < numLongs; longIdx++)
        {
            ulong longValue = 0;
            for (int entryIdx = 0; entryIdx < entriesPerLong; entryIdx++)
            {
                int dataIdx = longIdx * entriesPerLong + entryIdx;
                if (dataIdx < 256)
                {
                    int entryValue = heightmapEntries[dataIdx];
                    int bitOffset = entryIdx * heightmapBitsPerEntry;
                    // Mask to ensure value fits in 9 bits
                    ulong maskedValue = (ulong)(entryValue & ((1 << heightmapBitsPerEntry) - 1));
                    longValue |= (maskedValue << bitOffset);
                }
            }
            packetWriter.WriteLong((long)longValue);
        }
        
        // Generate chunk sections
        var chunkDataWriter = new ProtocolWriter();
        
        // Overworld has 24 sections (y=-64 to 320)
        // Each section is 16 blocks tall
        for (int sectionIdx = 0; sectionIdx < 24; sectionIdx++)
        {
            // Get section data from BlockManager
            var sectionData = blockManager.GetChunkSectionForProtocol(chunkX, chunkZ, sectionIdx);
            short blockCount = sectionData.Item1;
            List<int> palette = sectionData.Item2;
            List<int> paletteIndices = sectionData.Item3;
            
            // Write block count
            chunkDataWriter.WriteShort(blockCount);
            
            // Determine bits per entry based on palette size
            if (palette.Count == 1)
            {
                // Single-value palette (0 bits per entry)
                chunkDataWriter.WriteByte(0);
                chunkDataWriter.WriteVarInt(palette[0]);
            }
            else
            {
                // Multiple values - use indirect palette
                // Calculate bits per entry (need at least ceil(log2(palette_size)))
                // Use bit_length equivalent: find the highest bit needed to represent (palette.Count - 1)
                int bitsPerEntry = Math.Max(4, GetBitLength(palette.Count - 1));
                if (bitsPerEntry > 8)
                {
                    bitsPerEntry = 8; // Cap at 8 bits
                }
                
                // Write block states PalettedContainer (Indirect)
                chunkDataWriter.WritePalettedContainerIndirect(
                    bitsPerEntry: bitsPerEntry,
                    palette: palette,
                    dataArray: paletteIndices
                );
            }
            
            // Biomes: Single-value palette (plains = 0)
            chunkDataWriter.WriteByte(0); // 0 bits per entry
            chunkDataWriter.WriteVarInt(0); // Biome ID 0 = plains
        }
        
        // Get the chunk data (uncompressed)
        byte[] chunkDataRaw = chunkDataWriter.ToArray();
        
        // Write Data as Prefixed Array of Byte (VarInt length + bytes)
        packetWriter.WriteVarInt(chunkDataRaw.Length);
        packetWriter.WriteBytes(chunkDataRaw);
        
        // Block Entities: Empty
        packetWriter.WriteVarInt(0);
        
        // Light Data
        const int numSections = 24;
        const int numLightBits = numSections + 2; // 26 bits
        
        // Calculate min and max height from heightmap to determine which sections need sky light
        int minHeight = heightmapEntries.Length > 0 ? heightmapEntries.Min() : 64;
        int maxHeight = heightmapEntries.Length > 0 ? heightmapEntries.Max() : 64;
        
        // Determine if this is flat world (all heights are the same) or terrain
        bool isFlatWorld = minHeight == maxHeight && minHeight == 65; // 65 = 64 + 1 (heightmap is surface + 1)
        
        // Sky Light Mask: Sections that need sky light
        var skyLightMaskBits = new List<bool>(numLightBits);
        var skyLightSections = new List<int>(); // Track which sections we're sending data for
        for (int i = 0; i < numLightBits; i++)
        {
            skyLightMaskBits.Add(false);
        }
        
        if (isFlatWorld)
        {
            // Flat world: sections from ground section (y=64) upward need sky light
            int groundY = 64;
            int groundSection = (groundY + 64) / 16; // Section containing ground_y
            for (int sectionIdx = groundSection; sectionIdx < numSections; sectionIdx++)
            {
                int bitIdx = sectionIdx + 1; // Bit 0 is for section below world
                if (bitIdx < numLightBits)
                {
                    skyLightMaskBits[bitIdx] = true;
                    skyLightSections.Add(sectionIdx);
                }
            }
        }
        else
        {
            // Terrain: sections from min height to max height (and above) need sky light
            // Valleys (at min height) are still exposed to sky
            int minSection = (minHeight + 64) / 16; // Section containing min height
            for (int sectionIdx = minSection; sectionIdx < numSections; sectionIdx++)
            {
                int bitIdx = sectionIdx + 1; // Bit 0 is for section below world
                if (bitIdx < numLightBits)
                {
                    skyLightMaskBits[bitIdx] = true;
                    skyLightSections.Add(sectionIdx);
                }
            }
        }
        packetWriter.WriteBitset(skyLightMaskBits);
        
        // Block Light Mask: Empty (no block light sources)
        var blockLightMaskBits = new List<bool>(numLightBits);
        for (int i = 0; i < numLightBits; i++)
        {
            blockLightMaskBits.Add(false);
        }
        packetWriter.WriteBitset(blockLightMaskBits);
        
        // Empty Sky Light Mask: Sections below minimum height have no sky light
        var emptySkyLightMaskBits = new List<bool>(numLightBits);
        for (int i = 0; i < numLightBits; i++)
        {
            emptySkyLightMaskBits.Add(false);
        }
        
        int minSectionForEmpty = isFlatWorld ? 8 : (minHeight + 64) / 16; // Section containing min height
        for (int sectionIdx = 0; sectionIdx < minSectionForEmpty; sectionIdx++)
        {
            int bitIdx = sectionIdx + 1;
            if (bitIdx < numLightBits)
            {
                emptySkyLightMaskBits[bitIdx] = true;
            }
        }
        packetWriter.WriteBitset(emptySkyLightMaskBits);
        
        // Empty Block Light Mask: All sections (no block light)
        var emptyBlockLightMaskBits = new List<bool>(numLightBits);
        for (int i = 0; i < numLightBits; i++)
        {
            emptyBlockLightMaskBits.Add(true);
        }
        packetWriter.WriteBitset(emptyBlockLightMaskBits);
        
        // Sky Light Arrays: One array per bit set in sky light mask
        // Each array is 2048 bytes = 4096 light values (4 bits each, 0-15)
        packetWriter.WriteVarInt(skyLightSections.Count);
        
        foreach (int sectionIdx in skyLightSections)
        {
            int sectionYMin = -64 + (sectionIdx * 16);
            int sectionYMax = sectionYMin + 15;
            
            // Generate light array (4096 values = 16x16x16 blocks)
            byte[] lightArray = new byte[2048]; // 2048 bytes = 4096 nibbles
            
            for (int y = 0; y < 16; y++)
            {
                int worldY = sectionYMin + y;
                for (int z = 0; z < 16; z++)
                {
                    for (int x = 0; x < 16; x++)
                    {
                        // Calculate index in 4096-element array
                        int blockIdx = y * 256 + z * 16 + x;
                        
                        // Calculate byte and nibble index
                        int byteIdx = blockIdx / 2;
                        bool isHighNibble = (blockIdx % 2) == 0;
                        
                        // Get height at this (x, z) position from heightmap
                        // Heightmap is indexed as [z * 16 + x]
                        int heightAtPos = heightmapEntries[z * 16 + x];
                        
                        // Determine light value based on height map
                        // Sky light propagates downward from surface, decreasing by 1 per block
                        int lightValue;
                        if (worldY >= heightAtPos)
                        {
                            // Above or at surface: full sky light
                            lightValue = 15;
                        }
                        else
                        {
                            // Below surface: light decreases by 1 per block downward
                            int distanceBelow = heightAtPos - worldY;
                            // Light level = 15 - distance, clamped to 0-15
                            lightValue = Math.Max(0, 15 - distanceBelow);
                        }
                        
                        // Pack into byte (high nibble first, then low nibble)
                        if (isHighNibble)
                        {
                            lightArray[byteIdx] = (byte)((lightValue << 4) & 0xF0);
                        }
                        else
                        {
                            lightArray[byteIdx] |= (byte)(lightValue & 0x0F);
                        }
                    }
                }
            }
            
            // Write array length (2048) and data
            packetWriter.WriteVarInt(lightArray.Length);
            packetWriter.WriteBytes(lightArray);
        }
        
        // Block Light Arrays: Empty (no block light sources)
        packetWriter.WriteVarInt(0);
        
        // Build final packet with length prefix
        var packetData = packetWriter.ToArray();
        var finalWriter = new ProtocolWriter();
        finalWriter.WriteVarInt(packetData.Length);
        finalWriter.WriteBytes(packetData);
        
        return finalWriter.ToArray();
    }

    public static byte[] BuildSetCenterChunkPacket(int chunkX, int chunkZ)
    {
        // Build packet data (without length prefix)
        var packetWriter = new ProtocolWriter();
        packetWriter.WriteVarInt(0x5C); // Set Center Chunk packet ID
        
        // Chunk X (VarInt)
        packetWriter.WriteVarInt(chunkX);
        
        // Chunk Z (VarInt)
        packetWriter.WriteVarInt(chunkZ);
        
        // Build final packet with length prefix
        var packetData = packetWriter.ToArray();
        var finalWriter = new ProtocolWriter();
        finalWriter.WriteVarInt(packetData.Length);
        finalWriter.WriteBytes(packetData);
        
        return finalWriter.ToArray();
    }

    public byte[] BuildUpdateLightPacket(int chunkX, int chunkZ, object lightData)
    {
        // TODO: Implement update light packet building
        throw new NotImplementedException();
    }

    public static byte[] BuildSynchronizePlayerPositionPacket(
        double x = 0.0,
        double y = 64.0,
        double z = 0.0,
        float yaw = 0.0f,
        float pitch = 0.0f,
        int flags = 0, // 0 = all absolute
        int teleportId = 0)
    {
        var packetWriter = new ProtocolWriter();
        packetWriter.WriteVarInt(0x46); // Synchronize Player Position packet ID
        
        // Teleport ID
        packetWriter.WriteVarInt(teleportId);
        
        // Position (Double)
        packetWriter.WriteDouble(x);
        packetWriter.WriteDouble(y);
        packetWriter.WriteDouble(z);
        
        // Velocity (Double) - usually 0 for spawn
        packetWriter.WriteDouble(0.0);
        packetWriter.WriteDouble(0.0);
        packetWriter.WriteDouble(0.0);
        
        // Yaw and Pitch (Float)
        packetWriter.WriteFloat(yaw);
        packetWriter.WriteFloat(pitch);
        
        // Flags (Int) - 0 = all absolute
        packetWriter.WriteInt(flags);
        
        // Build final packet with length prefix
        var packetData = packetWriter.ToArray();
        var finalWriter = new ProtocolWriter();
        finalWriter.WriteVarInt(packetData.Length);
        finalWriter.WriteBytes(packetData);
        
        return finalWriter.ToArray();
    }

    public static byte[] BuildUpdateTimePacket(
        long worldAge = 0,
        long timeOfDay = 6000, // Noon
        bool timeIncreasing = true)
    {
        var packetWriter = new ProtocolWriter();
        packetWriter.WriteVarInt(0x6F); // Update Time packet ID
        
        // World Age (Long)
        packetWriter.WriteLong(worldAge);
        
        // Time of Day (Long)
        packetWriter.WriteLong(timeOfDay);
        
        // Time of Day Increasing (Boolean)
        packetWriter.WriteBool(timeIncreasing);
        
        // Build final packet with length prefix
        var packetData = packetWriter.ToArray();
        var finalWriter = new ProtocolWriter();
        finalWriter.WriteVarInt(packetData.Length);
        finalWriter.WriteBytes(packetData);
        
        return finalWriter.ToArray();
    }

    public static byte[] BuildGameEventPacket(
        byte eventId = 13, // 13 = "Start waiting for level chunks"
        float value = 0.0f)
    {
        var packetWriter = new ProtocolWriter();
        packetWriter.WriteVarInt(0x26); // Game Event packet ID
        
        // Event (Unsigned Byte)
        packetWriter.WriteByte(eventId);
        
        // Value (Float)
        packetWriter.WriteFloat(value);
        
        // Build final packet with length prefix
        var packetData = packetWriter.ToArray();
        var finalWriter = new ProtocolWriter();
        finalWriter.WriteVarInt(packetData.Length);
        finalWriter.WriteBytes(packetData);
        
        return finalWriter.ToArray();
    }

    public static byte[] BuildKeepAlivePacket(long keepAliveId)
    {
        // Build packet data (without length prefix)
        var packetWriter = new ProtocolWriter();
        packetWriter.WriteVarInt(0x2B); // Keep Alive packet ID (clientbound)
        
        // Keep Alive ID (Long)
        packetWriter.WriteLong(keepAliveId);
        
        // Build final packet with length prefix
        var packetData = packetWriter.ToArray();
        var finalWriter = new ProtocolWriter();
        finalWriter.WriteVarInt(packetData.Length);
        finalWriter.WriteBytes(packetData);
        
        return finalWriter.ToArray();
    }

    /// <summary>
    /// Builds a Spawn Entity packet (0x01).
    /// Used to spawn entities including players.
    /// </summary>
    public static byte[] BuildSpawnEntityPacket(
        int entityId,
        Guid entityUuid,
        int entityType,
        double x,
        double y,
        double z,
        double velocityX = 0.0,
        double velocityY = 0.0,
        double velocityZ = 0.0,
        float pitch = 0.0f,
        float yaw = 0.0f,
        float headYaw = 0.0f,
        int data = 0)
    {
        var packetWriter = new ProtocolWriter();
        packetWriter.WriteVarInt(0x01); // Spawn Entity packet ID
        
        // Entity ID
        packetWriter.WriteVarInt(entityId);
        
        // Entity UUID
        packetWriter.WriteUuid(entityUuid);
        
        // Entity Type (VarInt)
        packetWriter.WriteVarInt(entityType);
        
        // Position (Double)
        packetWriter.WriteDouble(x);
        packetWriter.WriteDouble(y);
        packetWriter.WriteDouble(z);
        
        // Velocity (LpVec3)
        packetWriter.WriteLpVec3(velocityX, velocityY, velocityZ);
        
        // Pitch (Angle)
        packetWriter.WriteAngle(pitch);
        
        // Yaw (Angle)
        packetWriter.WriteAngle(yaw);
        
        // Head Yaw (Angle)
        packetWriter.WriteAngle(headYaw);
        
        // Data (VarInt)
        packetWriter.WriteVarInt(data);
        
        // Build final packet with length prefix
        var packetData = packetWriter.ToArray();
        var finalWriter = new ProtocolWriter();
        finalWriter.WriteVarInt(packetData.Length);
        finalWriter.WriteBytes(packetData);
        
        return finalWriter.ToArray();
    }

    /// <summary>
    /// Builds a Player Info Update packet (0x44).
    /// Used to add or update players on the client's player list.
    /// </summary>
    /// <param name="players">List of players to add/update, each with UUID and name</param>
    public static byte[] BuildPlayerInfoUpdatePacket(List<(Guid Uuid, string Name)> players)
    {
        if (players == null)
        {
            throw new ArgumentNullException(nameof(players), "Players list cannot be null");
        }
        if (players.Count == 0)
        {
            throw new ArgumentException("Players list cannot be empty", nameof(players));
        }

        var packetWriter = new ProtocolWriter();
        packetWriter.WriteVarInt(0x44); // Player Info Update packet ID

        // Actions: EnumSet - encoded as Fixed BitSet
        // Player Info Update has 6 actions:
        // 0x01 = Add Player (bit 0)
        // 0x02 = Initialize Chat (bit 1)
        // 0x04 = Update Game Mode (bit 2)
        // 0x08 = Update Listed (bit 3)
        // 0x10 = Update Latency (bit 4)
        // 0x20 = Update Display Name (bit 5)
        // Fixed BitSet size = ceil(6/8) = 1 byte
        const int NUM_PLAYER_INFO_ACTIONS = 6;
        const int ADD_PLAYER_ACTION_BIT = 0x01; // Bit 0
        packetWriter.WriteFixedBitSet(ADD_PLAYER_ACTION_BIT, NUM_PLAYER_INFO_ACTIONS);

        // Players: Prefixed Array
        packetWriter.WriteVarInt(players.Count);
        
        foreach (var (uuid, name) in players)
        {
            // UUID
            packetWriter.WriteUuid(uuid);
            
            // Player Actions array - length determined by which actions are set
            // For Add Player action, we need to write:
            // - Name (String, 16 chars max)
            // - Properties (Prefixed Array, can be empty)
            
            // Name
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Player name cannot be null or empty", nameof(players));
            }
            // Truncate to 16 characters if needed (protocol limit)
            string playerName = name.Length > 16 ? name.Substring(0, 16) : name;
            packetWriter.WriteString(playerName);
            
            // Properties: Prefixed Array (can be empty for offline mode)
            // Empty array means client will use default skin based on UUID
            packetWriter.WriteVarInt(0); // Empty properties array
        }

        // Build final packet with length prefix
        var packetData = packetWriter.ToArray();
        var finalWriter = new ProtocolWriter();
        finalWriter.WriteVarInt(packetData.Length);
        finalWriter.WriteBytes(packetData);
        
        return finalWriter.ToArray();
    }

    /// <summary>
    /// Builds a Remove Entities packet (0x4B).
    /// Removes entities from the client.
    /// </summary>
    public static byte[] BuildRemoveEntitiesPacket(int[] entityIds)
    {
        if (entityIds == null || entityIds.Length == 0)
        {
            throw new ArgumentException("Entity IDs array cannot be null or empty", nameof(entityIds));
        }
        
        var packetWriter = new ProtocolWriter();
        packetWriter.WriteVarInt(0x4B); // Remove Entities packet ID
        
        // Entity IDs (Prefixed Array of VarInt)
        packetWriter.WriteVarInt(entityIds.Length);
        foreach (int entityId in entityIds)
        {
            packetWriter.WriteVarInt(entityId);
        }
        
        // Build final packet with length prefix
        var packetData = packetWriter.ToArray();
        var finalWriter = new ProtocolWriter();
        finalWriter.WriteVarInt(packetData.Length);
        finalWriter.WriteBytes(packetData);
        
        return finalWriter.ToArray();
    }

    /// <summary>
    /// Builds an Update Entity Position packet (0x33).
    /// Used for small position changes (delta < 8 blocks).
    /// Position delta is encoded as fixed-point: (currentX * 4096 - prevX * 4096) as Short.
    /// </summary>
    public static byte[] BuildUpdateEntityPositionPacket(
        int entityId,
        short deltaX,
        short deltaY,
        short deltaZ,
        bool onGround)
    {
        var packetWriter = new ProtocolWriter();
        packetWriter.WriteVarInt(0x33); // Update Entity Position packet ID
        
        // Entity ID
        packetWriter.WriteVarInt(entityId);
        
        // Delta X, Y, Z (Short)
        packetWriter.WriteShort(deltaX);
        packetWriter.WriteShort(deltaY);
        packetWriter.WriteShort(deltaZ);
        
        // On Ground (Boolean)
        packetWriter.WriteBool(onGround);
        
        // Build final packet with length prefix
        var packetData = packetWriter.ToArray();
        var finalWriter = new ProtocolWriter();
        finalWriter.WriteVarInt(packetData.Length);
        finalWriter.WriteBytes(packetData);
        
        return finalWriter.ToArray();
    }

    /// <summary>
    /// Builds an Update Entity Position and Rotation packet (0x34).
    /// Used for position and rotation updates together.
    /// Position delta is encoded as fixed-point: (currentX * 4096 - prevX * 4096) as Short.
    /// </summary>
    public static byte[] BuildUpdateEntityPositionAndRotationPacket(
        int entityId,
        short deltaX,
        short deltaY,
        short deltaZ,
        float yaw,
        float pitch,
        bool onGround)
    {
        var packetWriter = new ProtocolWriter();
        packetWriter.WriteVarInt(0x34); // Update Entity Position and Rotation packet ID
        
        // Entity ID
        packetWriter.WriteVarInt(entityId);
        
        // Delta X, Y, Z (Short)
        packetWriter.WriteShort(deltaX);
        packetWriter.WriteShort(deltaY);
        packetWriter.WriteShort(deltaZ);
        
        // Yaw (Angle)
        packetWriter.WriteAngle(yaw);
        
        // Pitch (Angle)
        packetWriter.WriteAngle(pitch);
        
        // On Ground (Boolean)
        packetWriter.WriteBool(onGround);
        
        // Build final packet with length prefix
        var packetData = packetWriter.ToArray();
        var finalWriter = new ProtocolWriter();
        finalWriter.WriteVarInt(packetData.Length);
        finalWriter.WriteBytes(packetData);
        
        return finalWriter.ToArray();
    }

    /// <summary>
    /// Builds an Update Entity Rotation packet (0x36).
    /// Used for rotation-only updates.
    /// </summary>
    public static byte[] BuildUpdateEntityRotationPacket(
        int entityId,
        float yaw,
        float pitch,
        bool onGround)
    {
        var packetWriter = new ProtocolWriter();
        packetWriter.WriteVarInt(0x36); // Update Entity Rotation packet ID
        
        // Entity ID
        packetWriter.WriteVarInt(entityId);
        
        // Yaw (Angle)
        packetWriter.WriteAngle(yaw);
        
        // Pitch (Angle)
        packetWriter.WriteAngle(pitch);
        
        // On Ground (Boolean)
        packetWriter.WriteBool(onGround);
        
        // Build final packet with length prefix
        var packetData = packetWriter.ToArray();
        var finalWriter = new ProtocolWriter();
        finalWriter.WriteVarInt(packetData.Length);
        finalWriter.WriteBytes(packetData);
        
        return finalWriter.ToArray();
    }

    /// <summary>
    /// Builds a Teleport Entity packet (0x7B).
    /// Used for large position changes (>= 8 blocks) or absolute position updates.
    /// </summary>
    public static byte[] BuildTeleportEntityPacket(
        int entityId,
        double x,
        double y,
        double z,
        float yaw,
        float pitch,
        bool onGround)
    {
        var packetWriter = new ProtocolWriter();
        packetWriter.WriteVarInt(0x7B); // Teleport Entity packet ID
        
        // Entity ID
        packetWriter.WriteVarInt(entityId);
        
        // Position (Double)
        packetWriter.WriteDouble(x);
        packetWriter.WriteDouble(y);
        packetWriter.WriteDouble(z);
        
        // Yaw (Angle)
        packetWriter.WriteAngle(yaw);
        
        // Pitch (Angle)
        packetWriter.WriteAngle(pitch);
        
        // On Ground (Boolean)
        packetWriter.WriteBool(onGround);
        
        // Build final packet with length prefix
        var packetData = packetWriter.ToArray();
        var finalWriter = new ProtocolWriter();
        finalWriter.WriteVarInt(packetData.Length);
        finalWriter.WriteBytes(packetData);
        
        return finalWriter.ToArray();
    }

    /// <summary>
    /// Builds a Rotate Head packet (0x51).
    /// Used for head rotation-only updates (head yaw).
    /// </summary>
    public static byte[] BuildRotateHeadPacket(
        int entityId,
        float headYaw)
    {
        var packetWriter = new ProtocolWriter();
        packetWriter.WriteVarInt(0x51); // Rotate Head packet ID

        // Entity ID
        packetWriter.WriteVarInt(entityId);

        // Head Yaw (Angle)
        packetWriter.WriteAngle(headYaw);

        // Build final packet with length prefix
        var packetData = packetWriter.ToArray();
        var finalWriter = new ProtocolWriter();
        finalWriter.WriteVarInt(packetData.Length);
        finalWriter.WriteBytes(packetData);

        return finalWriter.ToArray();
    }
}

