using MineSharp.Core.Protocol;
using MineSharp.World;
using MineSharp.World.Generation.Generators;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace MineSharp.Tests.Protocol;

/// <summary>
/// Integration tests for chunk lighting in actual chunk data packets.
/// Verifies that light data is correctly calculated and encoded.
/// </summary>
public class ChunkLightingIntegrationTests
{
    [Fact]
    public void BuildChunkDataPacket_FlatWorld_CalculatesLightCorrectly()
    {
        // Arrange
        var generator = new FlatWorldGenerator();
        var blockManager = new BlockManager(generator);
        
        // Act
        var packet = PacketBuilder.BuildChunkDataPacket(0, 0, blockManager);
        
        // Assert
        Assert.NotNull(packet);
        Assert.True(packet.Length > 0);
        
        // Parse the packet to verify light data
        var reader = new ProtocolReader(packet);
        
        // Skip packet length and ID
        int packetLength = reader.ReadVarInt();
        int packetId = reader.ReadVarInt();
        Assert.Equal(0x2C, packetId);
        
        // Skip chunk coordinates
        reader.ReadInt(); // chunkX
        reader.ReadInt(); // chunkZ
        
        // Skip heightmap
        int numHeightmaps = reader.ReadVarInt();
        Assert.Equal(1, numHeightmaps);
        reader.ReadVarInt(); // heightmap type
        int heightmapLongs = reader.ReadVarInt();
        for (int i = 0; i < heightmapLongs; i++)
        {
            reader.ReadLong();
        }
        
        // Skip chunk data
        int chunkDataLength = reader.ReadVarInt();
        reader.ReadBytes(chunkDataLength);
        
        // Skip block entities
        reader.ReadVarInt();
        
        // Read light data
        // Sky Light Mask
        int numLightBits = 26; // 24 sections + 2
        var skyLightMask = ReadBitset(reader, numLightBits);
        
        // Block Light Mask
        var blockLightMask = ReadBitset(reader, numLightBits);
        
        // Empty Sky Light Mask
        var emptySkyLightMask = ReadBitset(reader, numLightBits);
        
        // Empty Block Light Mask
        var emptyBlockLightMask = ReadBitset(reader, numLightBits);
        
        // Sky Light Arrays
        int skyLightArrayCount = reader.ReadVarInt();
        Assert.True(skyLightArrayCount > 0);
        
        // Verify that sections from ground (section 8) upward have sky light
        // Section 8 = y=64 to 79 (ground section)
        for (int sectionIdx = 8; sectionIdx < 24; sectionIdx++)
        {
            int bitIdx = sectionIdx + 1;
            Assert.True(skyLightMask[bitIdx], $"Section {sectionIdx} should have sky light");
        }
        
        // Verify sections below ground are marked as empty
        for (int sectionIdx = 0; sectionIdx < 8; sectionIdx++)
        {
            int bitIdx = sectionIdx + 1;
            Assert.True(emptySkyLightMask[bitIdx], $"Section {sectionIdx} should be marked as empty sky light");
        }
    }

    [Fact]
    public void BuildChunkDataPacket_NoiseTerrain_CalculatesLightBasedOnHeightmap()
    {
        // Arrange
        var generator = new NoiseTerrainGenerator();
        var blockManager = new BlockManager(generator);
        var heightmap = blockManager.GenerateHeightmap(0, 0);
        
        // Act
        var packet = PacketBuilder.BuildChunkDataPacket(0, 0, blockManager);
        
        // Assert
        Assert.NotNull(packet);
        Assert.NotNull(heightmap);
        
        // For noise terrain, light should be calculated based on heightmap
        // Sections from min height to max height should have sky light
        int minHeight = heightmap.Min();
        int maxHeight = heightmap.Max();
        int minSection = (minHeight + 64) / 16;
        
        // Parse packet to verify
        var reader = new ProtocolReader(packet);
        reader.ReadVarInt(); // packet length
        reader.ReadVarInt(); // packet ID
        reader.ReadInt(); // chunkX
        reader.ReadInt(); // chunkZ
        
        // Skip heightmap
        reader.ReadVarInt(); // num heightmaps
        reader.ReadVarInt(); // heightmap type
        int heightmapLongs = reader.ReadVarInt();
        for (int i = 0; i < heightmapLongs; i++)
        {
            reader.ReadLong();
        }
        
        // Skip chunk data
        int chunkDataLength = reader.ReadVarInt();
        reader.ReadBytes(chunkDataLength);
        reader.ReadVarInt(); // block entities
        
        // Read light masks
        int numLightBits = 26;
        var skyLightMask = ReadBitset(reader, numLightBits);
        
        // Verify that sections from minSection upward have sky light
        for (int sectionIdx = minSection; sectionIdx < 24; sectionIdx++)
        {
            int bitIdx = sectionIdx + 1;
            Assert.True(skyLightMask[bitIdx], $"Section {sectionIdx} (minSection={minSection}) should have sky light");
        }
    }

    private static List<bool> ReadBitset(ProtocolReader reader, int numBits)
    {
        // Bitset is written as a VarInt length followed by longs
        int numLongs = reader.ReadVarInt();
        var bits = new List<bool>(numBits);
        
        for (int i = 0; i < numLongs; i++)
        {
            long longValue = reader.ReadLong();
            for (int bit = 0; bit < 64 && bits.Count < numBits; bit++)
            {
                bits.Add((longValue & (1L << bit)) != 0);
            }
        }
        
        // Pad to numBits if needed
        while (bits.Count < numBits)
        {
            bits.Add(false);
        }
        
        return bits;
    }
}

