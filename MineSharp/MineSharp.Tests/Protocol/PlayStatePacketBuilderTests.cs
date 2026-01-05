using MineSharp.Core.Protocol;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace MineSharp.Tests.Protocol;

public class PlayStatePacketBuilderTests
{
    private class PacketLogEntry
    {
        [JsonPropertyName("packet_name")]
        public string? PacketName { get; set; }

        [JsonPropertyName("direction")]
        public string Direction { get; set; } = string.Empty;

        [JsonPropertyName("state")]
        public string State { get; set; } = string.Empty;

        [JsonPropertyName("packet_id")]
        public int PacketId { get; set; }

        [JsonPropertyName("packet_data_hex")]
        public string PacketDataHex { get; set; } = string.Empty;

        [JsonPropertyName("parsed_data")]
        public JsonElement? ParsedData { get; set; }
    }

    private static List<PacketLogEntry>? LoadPacketLog()
    {
        var logPath = Path.Combine("..", "..", "..", "..", "packet_logs", "packet_example.json");
        if (!File.Exists(logPath))
        {
            logPath = Path.Combine("..", "..", "packet_logs", "packet_example.json");
        }

        if (!File.Exists(logPath))
        {
            return null;
        }

        var json = File.ReadAllText(logPath);
        return JsonSerializer.Deserialize<List<PacketLogEntry>>(json);
    }

    private static byte[] HexToBytes(string hex)
    {
        var cleanHex = hex.Replace(" ", "").Replace("-", "");
        var bytes = new byte[cleanHex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(cleanHex.Substring(i * 2, 2), 16);
        }
        return bytes;
    }

    [Fact]
    public void BuildSynchronizePlayerPositionPacket_MatchesExpectedFromPacketLog()
    {
        var packets = LoadPacketLog();
        if (packets == null || packets.Count == 0)
            return;

        // Find Synchronize Player Position packet
        var positionPacket = packets.FirstOrDefault(p =>
            p.PacketName == "Synchronize Player Position" &&
            p.Direction == "clientbound" &&
            p.State == "PLAY");

        if (positionPacket == null)
            return;

        // Extract values from parsed_data
        double x = 0.0, y = 65.0, z = 0.0;
        float yaw = 0.0f, pitch = 0.0f;
        int teleportId = 0;

        if (positionPacket.ParsedData.HasValue)
        {
            var parsed = positionPacket.ParsedData.Value;
            if (parsed.TryGetProperty("x", out var xProp)) x = xProp.GetDouble();
            if (parsed.TryGetProperty("y", out var yProp)) y = yProp.GetDouble();
            if (parsed.TryGetProperty("z", out var zProp)) z = zProp.GetDouble();
            if (parsed.TryGetProperty("yaw", out var yawProp)) yaw = yawProp.GetSingle();
            if (parsed.TryGetProperty("pitch", out var pitchProp)) pitch = pitchProp.GetSingle();
            if (parsed.TryGetProperty("teleport_id", out var teleportProp)) teleportId = teleportProp.GetInt32();
        }

        // Build packet
        var actualPacket = PacketBuilder.BuildSynchronizePlayerPositionPacket(x, y, z, yaw, pitch, 0, teleportId);

        // Convert expected hex to bytes
        var expectedBytes = HexToBytes(positionPacket.PacketDataHex);

        // Compare
        Assert.Equal(expectedBytes.Length, actualPacket.Length);
        Assert.Equal(expectedBytes, actualPacket);
    }

    [Fact]
    public void BuildSynchronizePlayerPositionPacket_RoundTrip_CanBeParsed()
    {
        double x = 100.5, y = 65.0, z = -200.25;
        float yaw = 45.0f, pitch = -10.0f;
        int flags = 0;
        int teleportId = 123;

        // Build packet
        var packet = PacketBuilder.BuildSynchronizePlayerPositionPacket(x, y, z, yaw, pitch, flags, teleportId);

        // Parse it back (skip length prefix, start from packet ID)
        var reader = new ProtocolReader(packet);
        var packetLength = reader.ReadVarInt();
        var packetId = reader.ReadVarInt();
        
        Assert.Equal(0x46, packetId); // Synchronize Player Position packet ID
        
        var parsedTeleportId = reader.ReadVarInt();
        var parsedX = reader.ReadDouble();
        var parsedY = reader.ReadDouble();
        var parsedZ = reader.ReadDouble();
        var parsedVelX = reader.ReadDouble();
        var parsedVelY = reader.ReadDouble();
        var parsedVelZ = reader.ReadDouble();
        var parsedYaw = reader.ReadFloat();
        var parsedPitch = reader.ReadFloat();
        var parsedFlags = reader.ReadInt();
        
        Assert.Equal(teleportId, parsedTeleportId);
        Assert.Equal(x, parsedX, 5);
        Assert.Equal(y, parsedY, 5);
        Assert.Equal(z, parsedZ, 5);
        Assert.Equal(0.0, parsedVelX, 5);
        Assert.Equal(0.0, parsedVelY, 5);
        Assert.Equal(0.0, parsedVelZ, 5);
        Assert.Equal(yaw, parsedYaw, 5);
        Assert.Equal(pitch, parsedPitch, 5);
        Assert.Equal(flags, parsedFlags);
    }

    [Fact]
    public void BuildUpdateTimePacket_MatchesExpectedFromPacketLog()
    {
        var packets = LoadPacketLog();
        if (packets == null || packets.Count == 0)
            return;

        // Find Update Time packet
        var timePacket = packets.FirstOrDefault(p =>
            p.PacketName == "Update Time" &&
            p.Direction == "clientbound" &&
            p.State == "PLAY");

        if (timePacket == null)
            return;

        // Extract values from parsed_data
        long worldAge = 0, timeOfDay = 6000;
        bool timeIncreasing = true;

        if (timePacket.ParsedData.HasValue)
        {
            var parsed = timePacket.ParsedData.Value;
            if (parsed.TryGetProperty("world_age", out var ageProp)) worldAge = ageProp.GetInt64();
            if (parsed.TryGetProperty("time_of_day", out var timeProp)) timeOfDay = timeProp.GetInt64();
            if (parsed.TryGetProperty("time_increasing", out var increasingProp)) timeIncreasing = increasingProp.GetBoolean();
        }

        // Build packet
        var actualPacket = PacketBuilder.BuildUpdateTimePacket(worldAge, timeOfDay, timeIncreasing);

        // Convert expected hex to bytes
        var expectedBytes = HexToBytes(timePacket.PacketDataHex);

        // Compare
        Assert.Equal(expectedBytes.Length, actualPacket.Length);
        Assert.Equal(expectedBytes, actualPacket);
    }

    [Fact]
    public void BuildUpdateTimePacket_RoundTrip_CanBeParsed()
    {
        long worldAge = 12345;
        long timeOfDay = 18000; // Noon
        bool timeIncreasing = false;

        // Build packet
        var packet = PacketBuilder.BuildUpdateTimePacket(worldAge, timeOfDay, timeIncreasing);

        // Parse it back (skip length prefix, start from packet ID)
        var reader = new ProtocolReader(packet);
        var packetLength = reader.ReadVarInt();
        var packetId = reader.ReadVarInt();
        
        Assert.Equal(0x6F, packetId); // Update Time packet ID
        
        var parsedWorldAge = reader.ReadLong();
        var parsedTimeOfDay = reader.ReadLong();
        var parsedTimeIncreasing = reader.ReadBool();
        
        Assert.Equal(worldAge, parsedWorldAge);
        Assert.Equal(timeOfDay, parsedTimeOfDay);
        Assert.Equal(timeIncreasing, parsedTimeIncreasing);
    }

    [Fact]
    public void BuildGameEventPacket_MatchesExpectedFromPacketLog()
    {
        var packets = LoadPacketLog();
        if (packets == null || packets.Count == 0)
            return;

        // Find Game Event packet
        var eventPacket = packets.FirstOrDefault(p =>
            p.PacketName == "Game Event" &&
            p.Direction == "clientbound" &&
            p.State == "PLAY");

        if (eventPacket == null)
            return;

        // Extract values from parsed_data
        byte eventId = 13;
        float value = 0.0f;

        if (eventPacket.ParsedData.HasValue)
        {
            var parsed = eventPacket.ParsedData.Value;
            if (parsed.TryGetProperty("event", out var eventProp)) eventId = eventProp.GetByte();
            if (parsed.TryGetProperty("value", out var valueProp)) value = valueProp.GetSingle();
        }

        // Build packet
        var actualPacket = PacketBuilder.BuildGameEventPacket(eventId, value);

        // Convert expected hex to bytes
        var expectedBytes = HexToBytes(eventPacket.PacketDataHex);

        // Compare
        Assert.Equal(expectedBytes.Length, actualPacket.Length);
        Assert.Equal(expectedBytes, actualPacket);
    }

    [Fact]
    public void BuildGameEventPacket_RoundTrip_CanBeParsed()
    {
        byte eventId = 13;
        float value = 0.0f;

        // Build packet
        var packet = PacketBuilder.BuildGameEventPacket(eventId, value);

        // Parse it back (skip length prefix, start from packet ID)
        var reader = new ProtocolReader(packet);
        var packetLength = reader.ReadVarInt();
        var packetId = reader.ReadVarInt();
        
        Assert.Equal(0x26, packetId); // Game Event packet ID
        
        var parsedEventId = reader.ReadByte();
        var parsedValue = reader.ReadFloat();
        
        Assert.Equal(eventId, parsedEventId);
        Assert.Equal(value, parsedValue, 5);
    }

    [Fact]
    public void BuildLoginPlayPacket_RoundTrip_CanBeParsed()
    {
        var dimensionNames = new List<string> { "minecraft:overworld" };
        
        // Build packet
        var packet = PacketBuilder.BuildLoginPlayPacket(
            entityId: 1,
            isHardcore: false,
            dimensionNames: dimensionNames,
            maxPlayers: 20,
            viewDistance: 10,
            simulationDistance: 10,
            reducedDebugInfo: false,
            enableRespawnScreen: true,
            doLimitedCrafting: false,
            dimensionType: 0,
            dimensionName: "minecraft:overworld",
            hashedSeed: 0,
            gameMode: 0,
            previousGameMode: -1,
            isDebug: false,
            isFlat: false,
            hasDeathLocation: false,
            portalCooldown: 0,
            seaLevel: 63,
            enforcesSecureChat: false
        );

        // Parse it back (skip length prefix, start from packet ID)
        var reader = new ProtocolReader(packet);
        var packetLength = reader.ReadVarInt();
        var packetId = reader.ReadVarInt();
        
        Assert.Equal(0x30, packetId); // Login (play) packet ID
        
        var parsedEntityId = reader.ReadInt();
        var parsedIsHardcore = reader.ReadBool();
        var parsedDimensionCount = reader.ReadVarInt();
        
        Assert.Equal(1, parsedEntityId);
        Assert.False(parsedIsHardcore);
        Assert.Equal(1, parsedDimensionCount);
        
        var parsedDimensionName = reader.ReadString();
        Assert.Equal("minecraft:overworld", parsedDimensionName);
        
        // Continue parsing other fields to verify structure
        var parsedMaxPlayers = reader.ReadVarInt();
        var parsedViewDistance = reader.ReadVarInt();
        var parsedSimulationDistance = reader.ReadVarInt();
        
        Assert.Equal(20, parsedMaxPlayers);
        Assert.Equal(10, parsedViewDistance);
        Assert.Equal(10, parsedSimulationDistance);
    }
}

