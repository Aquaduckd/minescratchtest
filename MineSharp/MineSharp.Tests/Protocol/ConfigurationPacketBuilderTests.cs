using MineSharp.Core.Protocol;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace MineSharp.Tests.Protocol;

public class ConfigurationPacketBuilderTests
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
    public void BuildKnownPacksPacket_MatchesExpectedFromPacketLog()
    {
        var packets = LoadPacketLog();
        if (packets == null || packets.Count == 0)
            return;

        // Find Known Packs packet
        var knownPacksPacket = packets.FirstOrDefault(p =>
            p.PacketName == "Known Packs" &&
            p.Direction == "clientbound" &&
            p.State == "CONFIGURATION");

        if (knownPacksPacket == null)
            return;

        // Build packet with default values
        var actualPacket = PacketBuilder.BuildKnownPacksPacket();

        // Convert expected hex to bytes
        var expectedBytes = HexToBytes(knownPacksPacket.PacketDataHex);

        // Compare
        Assert.Equal(expectedBytes.Length, actualPacket.Length);
        Assert.Equal(expectedBytes, actualPacket);
    }

    [Fact]
    public void BuildKnownPacksPacket_RoundTrip_CanBeParsed()
    {
        var packs = new List<(string, string, string)> { ("minecraft", "core", "1.21.10") };

        // Build packet
        var packet = PacketBuilder.BuildKnownPacksPacket(packs);

        // Parse it back (skip length prefix, start from packet ID)
        var reader = new ProtocolReader(packet);
        var packetLength = reader.ReadVarInt();
        var packetId = reader.ReadVarInt();
        
        Assert.Equal(0x0E, packetId); // Known Packs packet ID
        
        var packCount = reader.ReadVarInt();
        Assert.Equal(1, packCount);
        
        var namespace_ = reader.ReadString();
        var packId = reader.ReadString();
        var version = reader.ReadString();
        
        Assert.Equal("minecraft", namespace_);
        Assert.Equal("core", packId);
        Assert.Equal("1.21.10", version);
    }

    [Fact]
    public void BuildRegistryDataPacket_MatchesExpectedFromPacketLog()
    {
        var packets = LoadPacketLog();
        if (packets == null || packets.Count == 0)
            return;

        // Find a simple Registry Data packet (dimension_type with 1 entry)
        var registryPacket = packets.FirstOrDefault(p =>
            p.PacketName != null &&
            p.PacketName.StartsWith("Registry Data (minecraft:dimension_type)") &&
            p.Direction == "clientbound" &&
            p.State == "CONFIGURATION");

        if (registryPacket == null)
            return;

        // Build packet
        var entries = new List<(string EntryId, byte[]? NbtData)>
        {
            ("minecraft:overworld", null)
        };
        var actualPacket = PacketBuilder.BuildRegistryDataPacket("minecraft:dimension_type", entries);

        // Convert expected hex to bytes
        var expectedBytes = HexToBytes(registryPacket.PacketDataHex);

        // Compare
        Assert.Equal(expectedBytes.Length, actualPacket.Length);
        Assert.Equal(expectedBytes, actualPacket);
    }

    [Fact]
    public void BuildRegistryDataPacket_RoundTrip_CanBeParsed()
    {
        var entries = new List<(string EntryId, byte[]? NbtData)>
        {
            ("minecraft:overworld", null),
            ("minecraft:the_nether", null),
            ("minecraft:the_end", null)
        };

        // Build packet
        var packet = PacketBuilder.BuildRegistryDataPacket("minecraft:dimension_type", entries);

        // Parse it back (skip length prefix, start from packet ID)
        var reader = new ProtocolReader(packet);
        var packetLength = reader.ReadVarInt();
        var packetId = reader.ReadVarInt();
        
        Assert.Equal(0x07, packetId); // Registry Data packet ID
        
        var registryId = reader.ReadString();
        Assert.Equal("minecraft:dimension_type", registryId);
        
        var entryCount = reader.ReadVarInt();
        Assert.Equal(3, entryCount);
        
        var parsedEntries = new List<string>();
        for (int i = 0; i < entryCount; i++)
        {
            var entryId = reader.ReadString();
            var hasNbt = reader.ReadBool();
            if (hasNbt)
            {
                // Skip NBT data for now (we're not using it)
                var nbtLength = reader.ReadVarInt();
                reader.ReadBytes(nbtLength);
            }
            parsedEntries.Add(entryId);
        }
        
        Assert.Equal("minecraft:overworld", parsedEntries[0]);
        Assert.Equal("minecraft:the_nether", parsedEntries[1]);
        Assert.Equal("minecraft:the_end", parsedEntries[2]);
    }

    [Fact]
    public void BuildRegistryDataPacket_WithMultipleEntries_MatchesExpected()
    {
        var packets = LoadPacketLog();
        if (packets == null || packets.Count == 0)
            return;

        // Find a Registry Data packet with multiple entries (cat_variant with 11 entries)
        var registryPacket = packets.FirstOrDefault(p =>
            p.PacketName != null &&
            p.PacketName.StartsWith("Registry Data (minecraft:cat_variant)") &&
            p.Direction == "clientbound" &&
            p.State == "CONFIGURATION");

        if (registryPacket == null)
            return;

        // Extract entries from parsed_data
        var entries = new List<(string EntryId, byte[]? NbtData)>
        {
            ("minecraft:persian", null),
            ("minecraft:british_shorthair", null),
            ("minecraft:siamese", null),
            ("minecraft:ragdoll", null),
            ("minecraft:jellie", null),
            ("minecraft:black", null),
            ("minecraft:red", null),
            ("minecraft:tabby", null),
            ("minecraft:all_black", null),
            ("minecraft:calico", null),
            ("minecraft:white", null)
        };

        // Build packet
        var actualPacket = PacketBuilder.BuildRegistryDataPacket("minecraft:cat_variant", entries);

        // Convert expected hex to bytes
        var expectedBytes = HexToBytes(registryPacket.PacketDataHex);

        // Compare
        Assert.Equal(expectedBytes.Length, actualPacket.Length);
        Assert.Equal(expectedBytes, actualPacket);
    }

    [Fact]
    public void BuildFinishConfigurationPacket_MatchesExpectedFromPacketLog()
    {
        var packets = LoadPacketLog();
        if (packets == null || packets.Count == 0)
            return;

        // Find Finish Configuration packet
        var finishConfigPacket = packets.FirstOrDefault(p =>
            p.PacketName == "Finish Configuration" &&
            p.Direction == "clientbound" &&
            p.State == "CONFIGURATION");

        if (finishConfigPacket == null)
            return;

        // Build packet
        var actualPacket = PacketBuilder.BuildFinishConfigurationPacket();

        // Convert expected hex to bytes
        var expectedBytes = HexToBytes(finishConfigPacket.PacketDataHex);

        // Compare
        Assert.Equal(expectedBytes.Length, actualPacket.Length);
        Assert.Equal(expectedBytes, actualPacket);
    }

    [Fact]
    public void BuildFinishConfigurationPacket_RoundTrip_CanBeParsed()
    {
        // Build packet
        var packet = PacketBuilder.BuildFinishConfigurationPacket();

        // Parse it back (skip length prefix, start from packet ID)
        var reader = new ProtocolReader(packet);
        var packetLength = reader.ReadVarInt();
        var packetId = reader.ReadVarInt();
        
        Assert.Equal(0x03, packetId); // Finish Configuration packet ID
        
        // Finish Configuration has no additional fields
        Assert.Equal(1, packetLength); // Just the packet ID
    }
}

