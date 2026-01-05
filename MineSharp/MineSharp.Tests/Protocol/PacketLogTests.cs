using MineSharp.Core.Protocol;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace MineSharp.Tests.Protocol;

/// <summary>
/// Tests using real packet data from packet logs.
/// </summary>
public class PacketLogTests
{
    private class PacketLogEntry
    {
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;

        [JsonPropertyName("direction")]
        public string Direction { get; set; } = string.Empty;

        [JsonPropertyName("state")]
        public string State { get; set; } = string.Empty;

        [JsonPropertyName("packet_id")]
        public int PacketId { get; set; }

        [JsonPropertyName("packet_name")]
        public string? PacketName { get; set; }

        [JsonPropertyName("packet_length")]
        public int PacketLength { get; set; }

        [JsonPropertyName("packet_data_hex")]
        public string PacketDataHex { get; set; } = string.Empty;

        [JsonPropertyName("parsed_data")]
        public JsonElement? ParsedData { get; set; }
    }

    [Fact]
    public void ParseHandshakePacket_FromPacketLog_MatchesExpected()
    {
        // Load packet log
        var logPath = Path.Combine("..", "..", "..", "..", "packet_logs", "packet_example.json");
        if (!File.Exists(logPath))
        {
            // Try alternative path
            logPath = Path.Combine("..", "..", "packet_logs", "packet_example.json");
        }

        if (!File.Exists(logPath))
        {
            // Skip test if log file doesn't exist
            return;
        }

        var json = File.ReadAllText(logPath);
        var packets = JsonSerializer.Deserialize<List<PacketLogEntry>>(json);

        if (packets == null || packets.Count == 0)
            return;

        // Find handshake packet
        var handshakePacket = packets.FirstOrDefault(p => 
            p.PacketName == "Handshake" && 
            p.Direction == "serverbound" &&
            p.State == "HANDSHAKING");

        if (handshakePacket == null)
            return;

        // Convert hex to bytes
        var data = ConvertHexStringToBytes(handshakePacket.PacketDataHex);

        // Read packet length (VarInt)
        var reader = new ProtocolReader(data);
        var packetLength = reader.ReadVarInt();
        Assert.Equal(handshakePacket.PacketLength, packetLength);

        // Read packet ID (VarInt)
        var packetId = reader.ReadVarInt();
        Assert.Equal(handshakePacket.PacketId, packetId);

        // Verify we can read the protocol version
        var protocolVersion = reader.ReadVarInt();
        
        // Check parsed_data if available
        if (handshakePacket.ParsedData.HasValue)
        {
            var parsed = handshakePacket.ParsedData.Value;
            if (parsed.TryGetProperty("protocol_version", out var protocolVersionProp))
            {
                var expectedProtocolVersion = protocolVersionProp.GetInt32();
                Assert.Equal(expectedProtocolVersion, protocolVersion);
            }
        }
    }

    [Fact]
    public void ParseLoginStartPacket_FromPacketLog_MatchesExpected()
    {
        var logPath = Path.Combine("..", "..", "..", "..", "packet_logs", "packet_example.json");
        if (!File.Exists(logPath))
        {
            logPath = Path.Combine("..", "..", "packet_logs", "packet_example.json");
        }

        if (!File.Exists(logPath))
            return;

        var json = File.ReadAllText(logPath);
        var packets = JsonSerializer.Deserialize<List<PacketLogEntry>>(json);

        if (packets == null || packets.Count == 0)
            return;

        var loginStartPacket = packets.FirstOrDefault(p =>
            p.PacketName == "Login Start" &&
            p.Direction == "serverbound" &&
            p.State == "LOGIN");

        if (loginStartPacket == null)
            return;

        var data = ConvertHexStringToBytes(loginStartPacket.PacketDataHex);

        var reader = new ProtocolReader(data);
        var packetLength = reader.ReadVarInt();
        var packetId = reader.ReadVarInt();

        // Read username
        var username = reader.ReadString();

        // Read UUID
        var uuid = reader.ReadUuid();

        // Verify against parsed_data
        if (loginStartPacket.ParsedData.HasValue)
        {
            var parsed = loginStartPacket.ParsedData.Value;
            if (parsed.TryGetProperty("username", out var usernameProp))
            {
                Assert.Equal(usernameProp.GetString(), username);
            }
            if (parsed.TryGetProperty("player_uuid", out var uuidProp))
            {
                var expectedUuid = Guid.Parse(uuidProp.GetString() ?? "");
                Assert.Equal(expectedUuid, uuid);
            }
        }
    }

    private static byte[] ConvertHexStringToBytes(string hex)
    {
        // Remove spaces if present
        hex = hex.Replace(" ", "").Replace("-", "");
        
        var bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        return bytes;
    }
}

