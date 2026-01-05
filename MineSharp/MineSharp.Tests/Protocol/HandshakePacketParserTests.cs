using MineSharp.Core;
using MineSharp.Core.Protocol;
using MineSharp.Core.Protocol.PacketTypes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace MineSharp.Tests.Protocol;

public class HandshakePacketParserTests
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
    public void ParseHandshakePacket_FromPacketLog_MatchesExpected()
    {
        // Load packet log
        var logPath = Path.Combine("..", "..", "..", "..", "packet_logs", "packet_example.json");
        if (!File.Exists(logPath))
        {
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
        var handshakePacket = packets?.FirstOrDefault(p =>
            p.PacketName == "Handshake" &&
            p.Direction == "serverbound" &&
            p.State == "HANDSHAKING");

        if (handshakePacket == null)
            return;

        // Convert hex to bytes
        var data = ConvertHexStringToBytes(handshakePacket.PacketDataHex);

        // Parse packet
        var (packetId, packet) = PacketParser.ParsePacket(data, ConnectionState.Handshaking);

        // Assert
        Assert.Equal(handshakePacket.PacketId, packetId);
        Assert.NotNull(packet);
        var handshake = Assert.IsType<HandshakePacket>(packet);

        // Verify against parsed_data
        if (handshakePacket.ParsedData.HasValue)
        {
            var parsed = handshakePacket.ParsedData.Value;
            if (parsed.TryGetProperty("protocol_version", out var protocolVersionProp))
            {
                Assert.Equal(protocolVersionProp.GetInt32(), handshake.ProtocolVersion);
            }
            if (parsed.TryGetProperty("server_address", out var serverAddressProp))
            {
                Assert.Equal(serverAddressProp.GetString(), handshake.ServerAddress);
            }
            if (parsed.TryGetProperty("server_port", out var serverPortProp))
            {
                Assert.Equal(serverPortProp.GetInt32(), handshake.ServerPort);
            }
            if (parsed.TryGetProperty("intent", out var intentProp))
            {
                Assert.Equal(intentProp.GetInt32(), handshake.Intent);
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

