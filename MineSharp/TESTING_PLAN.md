# Testing Plan for MineSharp

This document outlines a comprehensive testing strategy using packet logs from the Python server implementation.

## Overview

We'll use packet logs to:
1. **Replay packets** - Test that the C# server can parse packets correctly
2. **Generate expected responses** - Test that the C# server generates correct responses
3. **Round-trip testing** - Test encoding/decoding consistency
4. **State transition testing** - Verify correct state machine behavior
5. **Integration testing** - Test complete login flows

## Test Structure

```
MineSharp.Tests/
├── UnitTests/
│   ├── Protocol/
│   │   ├── VarIntTests.cs
│   │   ├── ProtocolReaderTests.cs
│   │   ├── ProtocolWriterTests.cs
│   │   └── PacketParserTests.cs
│   └── DataTypes/
│       ├── PositionTests.cs
│       └── Vector3Tests.cs
├── IntegrationTests/
│   ├── PacketLogReplayTests.cs
│   ├── LoginFlowTests.cs
│   └── StateTransitionTests.cs
├── TestData/
│   ├── PacketLogs/
│   │   └── (packet log JSON files)
│   └── ExpectedPackets/
│       └── (expected packet hex dumps)
└── TestUtilities/
    ├── PacketLogLoader.cs
    ├── PacketComparer.cs
    └── TestServer.cs
```

## Phase 1: Unit Tests

### 1.1 VarInt/VarLong Tests

**File**: `MineSharp.Tests/UnitTests/Protocol/VarIntTests.cs`

**Test Cases**:
```csharp
[Test]
public void ReadVarInt_Zero_ReturnsZero()
{
    // Arrange
    var data = new byte[] { 0x00 };
    var reader = new ProtocolReader(data);
    
    // Act
    var result = reader.ReadVarInt();
    
    // Assert
    Assert.AreEqual(0, result);
}

[Test]
public void ReadVarInt_SingleByte_ReturnsCorrectValue()
{
    // Test values: 0-127 (single byte)
    // Test cases: 0, 1, 127
}

[Test]
public void ReadVarInt_MultiByte_ReturnsCorrectValue()
{
    // Test values: 128-2097151 (multi-byte)
    // Test cases: 128, 255, 300, 2147483647
}

[Test]
public void WriteVarInt_RoundTrip_MatchesOriginal()
{
    // Test encoding then decoding returns original value
    // Test cases: 0, 1, 127, 128, 255, 300, 2147483647
}

[Test]
public void ReadVarInt_InvalidData_ThrowsException()
{
    // Test malformed VarInt (too long, incomplete)
}
```

**Test Data Source**: Use known VarInt values from packet logs.

### 1.2 ProtocolReader/Writer Tests

**File**: `MineSharp.Tests/UnitTests/Protocol/ProtocolReaderTests.cs`

**Test Cases**:
```csharp
[Test]
public void ReadString_ValidString_ReturnsCorrectValue()
{
    // Test reading UTF-8 strings with VarInt length prefix
    // Use strings from packet logs: "localhost", "ClemenPine", etc.
}

[Test]
public void ReadUuid_ValidUuid_ReturnsCorrectValue()
{
    // Test reading 16-byte UUIDs
    // Use UUIDs from packet logs
}

[Test]
public void ReadBytes_ValidData_ReturnsCorrectBytes()
{
    // Test reading byte arrays
}

[Test]
public void WriteString_RoundTrip_MatchesOriginal()
{
    // Test encoding then decoding returns original string
}

[Test]
public void WriteUuid_RoundTrip_MatchesOriginal()
{
    // Test encoding then decoding returns original UUID
}
```

**Test Data Source**: Extract values from `parsed_data` in packet logs.

### 1.3 Packet Parser Tests

**File**: `MineSharp.Tests/UnitTests/Protocol/PacketParserTests.cs`

**Test Cases**:
```csharp
[Test]
public void ParseHandshakePacket_ValidData_ReturnsCorrectPacket()
{
    // Arrange: Use packet_data_hex from packet log
    var hex = "10008506096c6f63616c686f737463dd02";
    var data = Convert.FromHexString(hex);
    
    // Act
    var (packetId, packet) = PacketParser.ParsePacket(data, ConnectionState.Handshaking);
    
    // Assert
    Assert.AreEqual(0, packetId);
    var handshake = packet as HandshakePacket;
    Assert.NotNull(handshake);
    Assert.AreEqual(773, handshake.ProtocolVersion);
    Assert.AreEqual("localhost", handshake.ServerAddress);
    Assert.AreEqual(25565, handshake.ServerPort);
    Assert.AreEqual(2, handshake.Intent);
}

[Test]
public void ParseLoginStartPacket_ValidData_ReturnsCorrectPacket()
{
    // Use packet log data
}

[Test]
public void ParseClientInformationPacket_ValidData_ReturnsCorrectPacket()
{
    // Use packet log data
}
```

**Test Data Source**: Directly from packet logs - use `packet_data_hex` and `parsed_data`.

## Phase 2: Packet Log Replay Tests

### 2.1 Packet Log Loader

**File**: `MineSharp.Tests/TestUtilities/PacketLogLoader.cs`

```csharp
public class PacketLogLoader
{
    public static List<PacketLogEntry> LoadPacketLog(string filePath)
    {
        // Load JSON packet log file
        // Return list of packet entries
    }
    
    public static List<PacketLogEntry> GetPacketsByState(
        List<PacketLogEntry> packets, 
        ConnectionState state)
    {
        // Filter packets by connection state
    }
    
    public static List<PacketLogEntry> GetServerboundPackets(
        List<PacketLogEntry> packets)
    {
        // Filter serverbound packets (from client)
    }
    
    public static List<PacketLogEntry> GetClientboundPackets(
        List<PacketLogEntry> packets)
    {
        // Filter clientbound packets (to client)
    }
}

public class PacketLogEntry
{
    public DateTime Timestamp { get; set; }
    public string Direction { get; set; }  // "serverbound" or "clientbound"
    public ConnectionState State { get; set; }
    public int PacketId { get; set; }
    public string PacketName { get; set; }
    public int PacketLength { get; set; }
    public string PacketDataHex { get; set; }
    public Dictionary<string, object>? ParsedData { get; set; }
}
```

### 2.2 Packet Comparer

**File**: `MineSharp.Tests/TestUtilities/PacketComparer.cs`

```csharp
public class PacketComparer
{
    public static bool ComparePackets(byte[] actual, byte[] expected)
    {
        // Compare two packet byte arrays
        // Return true if identical
    }
    
    public static bool ComparePacketsHex(string actualHex, string expectedHex)
    {
        // Compare hex strings
    }
    
    public static PacketComparisonResult ComparePacketStructures(
        object actual, 
        Dictionary<string, object> expected)
    {
        // Compare parsed packet structures
        // Return detailed comparison result
    }
}

public class PacketComparisonResult
{
    public bool IsMatch { get; set; }
    public List<string> Differences { get; set; } = new();
}
```

### 2.3 Replay Tests

**File**: `MineSharp.Tests/IntegrationTests/PacketLogReplayTests.cs`

**Test Cases**:
```csharp
[Test]
public void ReplayHandshakePacket_MatchesExpected()
{
    // Arrange: Load packet log
    var packets = PacketLogLoader.LoadPacketLog("TestData/PacketLogs/pre_play_packets_20260105_141135.json");
    var handshakePacket = packets.First(p => p.PacketName == "Handshake" && p.Direction == "serverbound");
    
    // Act: Parse packet
    var data = Convert.FromHexString(handshakePacket.PacketDataHex);
    var (packetId, packet) = PacketParser.ParsePacket(data, ConnectionState.Handshaking);
    
    // Assert: Compare with expected parsed_data
    Assert.AreEqual(handshakePacket.PacketId, packetId);
    // Compare parsed packet fields with handshakePacket.ParsedData
}

[Test]
public void ReplayAllHandshakingPackets_AllParseCorrectly()
{
    // Load all handshaking packets from log
    // Parse each one
    // Verify all parse correctly
}

[Test]
public void ReplayAllLoginPackets_AllParseCorrectly()
{
    // Similar to above for LOGIN state
}

[Test]
public void ReplayAllConfigurationPackets_AllParseCorrectly()
{
    // Similar to above for CONFIGURATION state
}

[Test]
public void ReplayAllPlayPackets_AllParseCorrectly()
{
    // Similar to above for PLAY state
}
```

## Phase 3: Packet Generation Tests

### 3.1 Response Packet Tests

**File**: `MineSharp.Tests/IntegrationTests/PacketGenerationTests.cs`

**Test Cases**:
```csharp
[Test]
public void BuildLoginSuccessPacket_MatchesExpected()
{
    // Arrange: Load expected packet from log
    var packets = PacketLogLoader.LoadPacketLog("TestData/PacketLogs/pre_play_packets_20260105_141135.json");
    var expectedPacket = packets.First(p => p.PacketName == "Login Success" && p.Direction == "clientbound");
    
    // Extract data from parsed_data
    var uuid = Guid.Parse(expectedPacket.ParsedData["profile"]["uuid"]);
    var username = expectedPacket.ParsedData["profile"]["username"];
    
    // Act: Build packet
    var builder = new PacketBuilder();
    var actualPacket = builder.BuildLoginSuccessPacket(uuid, username, new List<object>());
    
    // Assert: Compare hex
    var expectedHex = expectedPacket.PacketDataHex;
    var actualHex = Convert.ToHexString(actualPacket);
    Assert.AreEqual(expectedHex, actualHex, ignoreCase: true);
}

[Test]
public void BuildRegistryDataPacket_MatchesExpected()
{
    // Load expected registry data packet from log
    // Build same packet
    // Compare hex
}

[Test]
public void BuildFinishConfigurationPacket_MatchesExpected()
{
    // Similar pattern
}

[Test]
public void BuildLoginPlayPacket_MatchesExpected()
{
    // Similar pattern
}

[Test]
public void BuildSynchronizePlayerPositionPacket_MatchesExpected()
{
    // Similar pattern
}
```

## Phase 4: State Transition Tests

### 4.1 Login Flow Integration Test

**File**: `MineSharp.Tests/IntegrationTests/LoginFlowTests.cs`

**Test Cases**:
```csharp
[Test]
public void CompleteLoginFlow_MatchesPacketLog()
{
    // Arrange: Load complete packet log
    var packets = PacketLogLoader.LoadPacketLog("TestData/PacketLogs/pre_play_packets_20260105_141135.json");
    var serverboundPackets = PacketLogLoader.GetServerboundPackets(packets);
    var expectedClientboundPackets = PacketLogLoader.GetClientboundPackets(packets);
    
    // Act: Simulate connection
    var connection = new TestClientConnection();
    var handler = new PacketHandler(...);
    
    var actualClientboundPackets = new List<byte[]>();
    
    foreach (var serverboundPacket in serverboundPackets)
    {
        // Parse serverbound packet
        var data = Convert.FromHexString(serverboundPacket.PacketDataHex);
        var (packetId, packet) = PacketParser.ParsePacket(data, connection.State);
        
        // Handle packet (this should generate clientbound responses)
        await handler.HandlePacketAsync(connection, packetId, packet, data);
        
        // Collect generated clientbound packets
        actualClientboundPackets.AddRange(connection.SentPackets);
        connection.SentPackets.Clear();
    }
    
    // Assert: Compare generated packets with expected
    Assert.AreEqual(expectedClientboundPackets.Count, actualClientboundPackets.Count);
    for (int i = 0; i < expectedClientboundPackets.Count; i++)
    {
        var expected = Convert.FromHexString(expectedClientboundPackets[i].PacketDataHex);
        var actual = actualClientboundPackets[i];
        Assert.IsTrue(PacketComparer.ComparePackets(actual, expected));
    }
}

[Test]
public void StateTransitions_MatchExpectedSequence()
{
    // Verify state transitions: HANDSHAKING -> LOGIN -> CONFIGURATION -> PLAY
    // Check that state changes occur at correct packet boundaries
}
```

## Phase 5: Round-Trip Tests

### 5.1 Encode-Decode Tests

**File**: `MineSharp.Tests/IntegrationTests/RoundTripTests.cs`

**Test Cases**:
```csharp
[Test]
public void HandshakePacket_RoundTrip_MatchesOriginal()
{
    // Parse packet from log
    // Rebuild packet
    // Compare hex
}

[Test]
public void LoginStartPacket_RoundTrip_MatchesOriginal()
{
    // Similar pattern
}

[Test]
public void AllPacketTypes_RoundTrip_MatchesOriginal()
{
    // Test all packet types can be parsed and rebuilt correctly
}
```

## Phase 6: Test Data Management

### 6.1 Test Data Structure

```
TestData/
├── PacketLogs/
│   ├── complete_login_flow.json
│   ├── handshake_only.json
│   ├── login_only.json
│   └── configuration_only.json
├── ExpectedPackets/
│   ├── handshake_0x00.json
│   ├── login_success_0x02.json
│   └── (individual packet examples)
└── TestScenarios/
    ├── scenario_001_basic_login.json
    └── scenario_002_multiple_registries.json
```

### 6.2 Test Data Extraction Script

**File**: `MineSharp.Tests/TestUtilities/ExtractTestData.cs`

```csharp
public class TestDataExtractor
{
    public static void ExtractPacketExamples(string packetLogPath, string outputDir)
    {
        // Extract individual packet examples from log
        // Save as separate JSON files for easy reference
    }
    
    public static void ExtractStateSequences(string packetLogPath, string outputDir)
    {
        // Extract packets grouped by state
        // Create separate files for each state
    }
}
```

## Phase 7: Automated Test Generation

### 7.1 Test Case Generator

**File**: `MineSharp.Tests/TestUtilities/TestCaseGenerator.cs`

```csharp
public class TestCaseGenerator
{
    public static void GenerateParserTests(string packetLogPath, string outputFile)
    {
        // Generate C# test methods from packet log
        // One test per packet type
    }
    
    public static void GenerateBuilderTests(string packetLogPath, string outputFile)
    {
        // Generate C# test methods for packet building
        // One test per clientbound packet type
    }
}
```

## Phase 8: Performance Tests

### 8.1 Packet Parsing Performance

**File**: `MineSharp.Tests/PerformanceTests/PacketParsingPerformanceTests.cs`

**Test Cases**:
```csharp
[Test]
public void ParseHandshakePacket_Performance_MeetsTarget()
{
    // Parse handshake packet 10,000 times
    // Measure time
    // Assert < target time (e.g., 1ms per packet)
}

[Test]
public void BuildLoginSuccessPacket_Performance_MeetsTarget()
{
    // Similar for packet building
}
```

## Implementation Order

1. **Phase 1**: Unit tests for core protocol (VarInt, data types)
2. **Phase 2**: Packet log loader and comparer utilities
3. **Phase 3**: Packet replay tests (parsing)
4. **Phase 4**: Packet generation tests (building)
5. **Phase 5**: Round-trip tests
6. **Phase 6**: Integration tests (complete flows)
7. **Phase 7**: Automated test generation
8. **Phase 8**: Performance tests

## Test Execution Strategy

### During Development
- Run unit tests after each protocol implementation
- Run replay tests to verify parsing matches Python server
- Run generation tests to verify building matches expected packets

### Before Release
- Run full test suite
- Run integration tests with multiple packet logs
- Run performance tests
- Manual testing with actual Minecraft client

## Continuous Integration

### Test Pipeline
1. Load packet logs from `packet_logs/` directory
2. Run all unit tests
3. Run all integration tests
4. Run performance benchmarks
5. Generate test coverage report

### Success Criteria
- ✅ All unit tests pass
- ✅ All packet replay tests pass (100% match with packet logs)
- ✅ All packet generation tests pass (100% match with expected packets)
- ✅ All integration tests pass (complete login flow works)
- ✅ Performance targets met

## Test Data Sources

1. **Packet Logs**: `packet_logs/*.json` - Real packet captures from Python server
2. **Expected Packets**: Extract from packet logs for comparison
3. **Test Scenarios**: Manually created scenarios for edge cases

## Notes

- Packet logs contain both `packet_data_hex` (raw bytes) and `parsed_data` (structured data)
- Use `packet_data_hex` for exact byte comparison
- Use `parsed_data` for structure validation
- Chunk data packets are large - exclude from hex comparison, compare structure only
- Some packets may have variable data (timestamps, IDs) - compare structure, not exact values

## Example Test Case

```csharp
[Test]
[TestCaseSource(nameof(GetHandshakePacketsFromLogs))]
public void ParseHandshakePacket_FromLog_MatchesExpected(PacketLogEntry logEntry)
{
    // Arrange
    var data = Convert.FromHexString(logEntry.PacketDataHex);
    
    // Act
    var (packetId, packet) = PacketParser.ParsePacket(data, ConnectionState.Handshaking);
    
    // Assert
    Assert.AreEqual(logEntry.PacketId, packetId);
    var handshake = packet as HandshakePacket;
    Assert.NotNull(handshake);
    
    // Compare with parsed_data from log
    var expected = logEntry.ParsedData;
    Assert.AreEqual((int)expected["protocol_version"], handshake.ProtocolVersion);
    Assert.AreEqual((string)expected["server_address"], handshake.ServerAddress);
    Assert.AreEqual((int)expected["server_port"], handshake.ServerPort);
    Assert.AreEqual((int)expected["intent"], handshake.Intent);
}

public static IEnumerable<PacketLogEntry> GetHandshakePacketsFromLogs()
{
    var logs = Directory.GetFiles("TestData/PacketLogs", "*.json");
    foreach (var log in logs)
    {
        var packets = PacketLogLoader.LoadPacketLog(log);
        foreach (var packet in packets.Where(p => p.PacketName == "Handshake"))
        {
            yield return packet;
        }
    }
}
```

