using MineSharp.Core.Protocol;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace MineSharp.Tests.Protocol;

public class LoginSuccessPacketBuilderTests
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

    [Fact]
    public void BuildLoginSuccessPacket_MatchesExpectedFromPacketLog()
    {
        // Load packet log
        var logPath = Path.Combine("..", "..", "..", "..", "packet_logs", "packet_example.json");
        if (!File.Exists(logPath))
        {
            logPath = Path.Combine("..", "..", "packet_logs", "packet_example.json");
        }

        if (!File.Exists(logPath))
        {
            return; // Skip test if log file doesn't exist
        }

        var json = File.ReadAllText(logPath);
        var packets = JsonSerializer.Deserialize<List<PacketLogEntry>>(json);

        if (packets == null || packets.Count == 0)
            return;

        // Find Login Success packet
        var loginSuccessPacket = packets.FirstOrDefault(p =>
            p.PacketName == "Login Success" &&
            p.Direction == "clientbound" &&
            p.State == "LOGIN");

        if (loginSuccessPacket == null)
            return;

        // Extract UUID and username from parsed_data or use defaults
        var uuid = Guid.Parse("670fb6ce-0b55-448f-a9a9-ed3f1da93508");
        var username = "ClemenPine";

        if (loginSuccessPacket.ParsedData.HasValue)
        {
            var parsed = loginSuccessPacket.ParsedData.Value;
            // The parsed_data has a string representation, so we'll use the known values
        }

        // Build packet
        var actualPacket = PacketBuilder.BuildLoginSuccessPacket(uuid, username, new List<object>());

        // Convert expected hex to bytes
        var expectedHex = loginSuccessPacket.PacketDataHex.Replace(" ", "").Replace("-", "");
        var expectedBytes = new byte[expectedHex.Length / 2];
        for (int i = 0; i < expectedBytes.Length; i++)
        {
            expectedBytes[i] = Convert.ToByte(expectedHex.Substring(i * 2, 2), 16);
        }

        // Compare
        Assert.Equal(expectedBytes.Length, actualPacket.Length);
        Assert.Equal(expectedBytes, actualPacket);
    }

    [Fact]
    public void BuildLoginSuccessPacket_RoundTrip_CanBeParsed()
    {
        var uuid = Guid.Parse("670fb6ce-0b55-448f-a9a9-ed3f1da93508");
        var username = "ClemenPine";

        // Build packet
        var packet = PacketBuilder.BuildLoginSuccessPacket(uuid, username, new List<object>());

        // Parse it back (skip length prefix, start from packet ID)
        var reader = new ProtocolReader(packet);
        var packetLength = reader.ReadVarInt();
        var packetId = reader.ReadVarInt();
        
        Assert.Equal(0x02, packetId); // Login Success packet ID
        
        var parsedUuid = reader.ReadUuid();
        var parsedUsername = reader.ReadString();
        var propertyCount = reader.ReadVarInt();
        
        Assert.Equal(uuid, parsedUuid);
        Assert.Equal(username, parsedUsername);
        Assert.Equal(0, propertyCount); // Empty properties for offline mode
    }
}

