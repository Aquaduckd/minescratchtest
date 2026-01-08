using MineSharp.Core.Protocol;
using Xunit;
using ProtocolReader = MineSharp.Core.Protocol.ProtocolReader;
using System.Text;

namespace MineSharp.Tests.Protocol;

public class SystemChatMessagePacketBuilderTests
{
    [Fact]
    public void BuildSystemChatMessagePacket_WithSimpleText_ShouldCreateValidPacket()
    {
        // Arrange
        var messageJson = "{\"text\":\"Hello, world!\"}";
        
        // Act
        var packet = PacketBuilder.BuildSystemChatMessagePacket(messageJson, overlay: false);
        
        // Assert
        Assert.NotNull(packet);
        Assert.True(packet.Length > 0);
        
        // Verify packet structure
        var reader = new ProtocolReader(packet);
        
        // Skip length prefix
        var packetLength = reader.ReadVarInt();
        Assert.True(packetLength > 0);
        
        // Verify packet ID
        var packetId = reader.ReadVarInt();
        Assert.Equal(0x77, packetId); // System Chat Message packet ID
        
        // Read NBT data - it's written as raw bytes
        // Calculate remaining bytes (packet length - what we've read so far)
        int nbtStartOffset = reader.Offset;
        int remainingBytes = packet.Length - nbtStartOffset;
        
        // Read NBT compound
        byte compoundType = reader.ReadByte();
        Assert.Equal(10, compoundType); // TAG_Compound
        
        // Read string tag
        byte stringType = reader.ReadByte();
        Assert.Equal(8, stringType); // TAG_String
        
        // Read name length and name
        ushort nameLength = reader.ReadUnsignedShort();
        byte[] nameBytes = reader.ReadBytes(nameLength);
        string name = Encoding.UTF8.GetString(nameBytes);
        Assert.Equal("text", name);
        
        // Read value length and value
        ushort valueLength = reader.ReadUnsignedShort();
        byte[] valueBytes = reader.ReadBytes(valueLength);
        string value = Encoding.UTF8.GetString(valueBytes);
        Assert.Equal("Hello, world!", value);
        
        // Read TAG_End
        byte endType = reader.ReadByte();
        Assert.Equal(0, endType); // TAG_End
        
        // Verify overlay boolean (false)
        var overlay = reader.ReadBool();
        Assert.False(overlay);
    }

    [Fact]
    public void BuildSystemChatMessagePacket_WithOverlay_ShouldSetOverlayFlag()
    {
        // Arrange
        var messageJson = "{\"text\":\"Action bar message\"}";
        
        // Act
        var packet = PacketBuilder.BuildSystemChatMessagePacket(messageJson, overlay: true);
        
        // Assert
        Assert.NotNull(packet);
        Assert.True(packet.Length > 0);
        
        // Verify overlay flag is set to true
        var reader = new ProtocolReader(packet);
        reader.ReadVarInt(); // Skip length
        reader.ReadVarInt(); // Skip packet ID
        
        // Skip NBT data: compound (1) + string tag (1) + name length (2) + "text" (4) + value length (2) + value (19) + end (1)
        reader.ReadByte(); // TAG_Compound
        reader.ReadByte(); // TAG_String
        reader.ReadUnsignedShort(); // name length
        reader.ReadBytes(4); // "text"
        ushort valueLength = reader.ReadUnsignedShort();
        reader.ReadBytes(valueLength); // value
        reader.ReadByte(); // TAG_End
        
        var overlay = reader.ReadBool();
        Assert.True(overlay);
    }

    [Fact]
    public void BuildSystemChatMessagePacket_WithFormattedText_ShouldCreateValidPacket()
    {
        // Arrange
        var messageJson = "{\"text\":\"Hello\",\"color\":\"yellow\"}";
        
        // Act
        var packet = PacketBuilder.BuildSystemChatMessagePacket(messageJson, overlay: false);
        
        // Assert
        Assert.NotNull(packet);
        Assert.True(packet.Length > 0);
        
        // Verify NBT structure is valid
        var reader = new ProtocolReader(packet);
        reader.ReadVarInt(); // Skip length
        reader.ReadVarInt(); // Skip packet ID
        
        // Verify it's a compound
        byte compoundType = reader.ReadByte();
        Assert.Equal(10, compoundType); // TAG_Compound
        
        // Read and verify "text" tag
        byte stringType1 = reader.ReadByte();
        Assert.Equal(8, stringType1); // TAG_String
        ushort nameLength1 = reader.ReadUnsignedShort();
        byte[] nameBytes1 = reader.ReadBytes(nameLength1);
        string name1 = Encoding.UTF8.GetString(nameBytes1);
        Assert.Equal("text", name1);
        ushort valueLength1 = reader.ReadUnsignedShort();
        byte[] valueBytes1 = reader.ReadBytes(valueLength1);
        string value1 = Encoding.UTF8.GetString(valueBytes1);
        Assert.Equal("Hello", value1);
        
        // Read and verify "color" tag
        byte stringType2 = reader.ReadByte();
        Assert.Equal(8, stringType2); // TAG_String
        ushort nameLength2 = reader.ReadUnsignedShort();
        byte[] nameBytes2 = reader.ReadBytes(nameLength2);
        string name2 = Encoding.UTF8.GetString(nameBytes2);
        Assert.Equal("color", name2);
        ushort valueLength2 = reader.ReadUnsignedShort();
        byte[] valueBytes2 = reader.ReadBytes(valueLength2);
        string value2 = Encoding.UTF8.GetString(valueBytes2);
        Assert.Equal("yellow", value2);
        
        // Verify TAG_End
        byte endType = reader.ReadByte();
        Assert.Equal(0, endType); // TAG_End
    }

    [Fact]
    public void BuildSystemChatMessagePacket_WithEmptyMessage_ShouldCreateValidPacket()
    {
        // Arrange
        var messageJson = "{\"text\":\"\"}";
        
        // Act
        var packet = PacketBuilder.BuildSystemChatMessagePacket(messageJson, overlay: false);
        
        // Assert
        Assert.NotNull(packet);
        Assert.True(packet.Length > 0);
        
        // Verify empty message is handled correctly
        var reader = new ProtocolReader(packet);
        reader.ReadVarInt(); // Skip length
        reader.ReadVarInt(); // Skip packet ID
        
        // Read NBT and verify empty string
        byte compoundType = reader.ReadByte();
        Assert.Equal(10, compoundType); // TAG_Compound
        byte stringType = reader.ReadByte();
        Assert.Equal(8, stringType); // TAG_String
        ushort nameLength = reader.ReadUnsignedShort();
        byte[] nameBytes = reader.ReadBytes(nameLength);
        string name = Encoding.UTF8.GetString(nameBytes);
        Assert.Equal("text", name);
        ushort valueLength = reader.ReadUnsignedShort();
        Assert.Equal(0, valueLength); // Empty string
        byte endType = reader.ReadByte();
        Assert.Equal(0, endType); // TAG_End
    }

    [Fact]
    public void BuildSystemChatMessagePacket_RoundTrip_CanBeParsed()
    {
        // Arrange
        var messageJson = "{\"text\":\"Test message\"}";
        
        // Act
        var packet = PacketBuilder.BuildSystemChatMessagePacket(messageJson, overlay: false);
        
        // Assert
        var reader = new ProtocolReader(packet);
        var length = reader.ReadVarInt();
        var packetId = reader.ReadVarInt();
        
        // Read NBT and extract text
        reader.ReadByte(); // TAG_Compound
        reader.ReadByte(); // TAG_String
        reader.ReadUnsignedShort(); // name length
        reader.ReadBytes(4); // "text"
        ushort valueLength = reader.ReadUnsignedShort();
        byte[] valueBytes = reader.ReadBytes(valueLength);
        string textValue = Encoding.UTF8.GetString(valueBytes);
        reader.ReadByte(); // TAG_End
        
        var overlay = reader.ReadBool();
        
        Assert.Equal(0x77, packetId);
        Assert.Equal("Test message", textValue);
        Assert.False(overlay);
    }
}

