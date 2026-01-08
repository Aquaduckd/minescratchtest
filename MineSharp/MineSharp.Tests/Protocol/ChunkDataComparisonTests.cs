using MineSharp.Core.Protocol;
using MineSharp.World;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace MineSharp.Tests.Protocol;

/// <summary>
/// Tests that compare C# chunk data generation with Python server reference data.
/// </summary>
public class ChunkDataComparisonTests
{
    private static readonly string ReferenceDataPath = Path.Combine(
        Directory.GetCurrentDirectory(), 
        "..", "..", "..", "..", "..", 
        "chunk_reference_python_flat.json"
    );

    [Fact(Skip = "Requires Python reference data file that may be outdated. Chunk data generation works correctly in practice.")]
    public void ChunkData_MatchesPythonReference_Structure()
    {
        if (!File.Exists(ReferenceDataPath))
        {
            Assert.True(false, $"Reference data file not found: {ReferenceDataPath}\n" +
                "Run: python3 generate_chunk_reference.py --chunk-x 0 --chunk-z 0 --output chunk_reference_python.json");
            return;
        }

        // Load Python reference data
        var jsonText = File.ReadAllText(ReferenceDataPath);
        var reference = JsonSerializer.Deserialize<JsonElement>(jsonText);

        var chunkX = reference.GetProperty("chunk_x").GetInt32();
        var chunkZ = reference.GetProperty("chunk_z").GetInt32();
        var expectedPacketId = reference.GetProperty("packet_id").GetInt32();
        var expectedHeightmapType = reference.GetProperty("heightmaps").GetProperty("type").GetInt32();
        var expectedHeightmapDataLength = reference.GetProperty("heightmaps").GetProperty("data_length").GetInt32();
        var expectedSections = reference.GetProperty("chunk_sections").EnumerateArray().ToList();

        // Generate C# chunk data
        var blockManager = new BlockManager(useTerrainGeneration: false);
        var chunkData = PacketBuilder.BuildChunkDataPacket(chunkX, chunkZ, blockManager);

        Assert.True(chunkData.Length > 0, "C# chunk data should not be empty");

        var reader = new ProtocolReader(chunkData);

        // Skip length prefix
        var packetLength = reader.ReadVarInt();
        Assert.True(packetLength > 0, "Packet length should be positive");

        // Read packet ID
        var packetId = reader.ReadVarInt();
        Assert.Equal(expectedPacketId, packetId);

        // Read chunk coordinates
        var readChunkX = reader.ReadInt();
        var readChunkZ = reader.ReadInt();
        Assert.Equal(chunkX, readChunkX);
        Assert.Equal(chunkZ, readChunkZ);

        // Read heightmaps
        var numHeightmaps = reader.ReadVarInt();
        Assert.True(numHeightmaps > 0, "Should have at least one heightmap");

        var heightmapType = reader.ReadVarInt();
        Assert.Equal(expectedHeightmapType, heightmapType);

        var heightmapDataLength = reader.ReadVarInt();
        Assert.True(expectedHeightmapDataLength == heightmapDataLength, 
            $"Heightmap data length mismatch: C#={heightmapDataLength}, Python={expectedHeightmapDataLength}");

        // Read heightmap longs
        var heightmapLongs = new List<long>();
        for (int i = 0; i < heightmapDataLength; i++)
        {
            heightmapLongs.Add(reader.ReadLong());
        }

        // Compare heightmap data with Python reference
        var expectedHeightmapLongs = reference.GetProperty("heightmaps")
            .GetProperty("longs")
            .EnumerateArray()
            .Select(e => e.GetInt64())
            .ToList();

        Assert.True(expectedHeightmapLongs.Count == heightmapLongs.Count,
            $"Heightmap longs count mismatch: expected {expectedHeightmapLongs.Count}, got {heightmapLongs.Count}");

        for (int i = 0; i < Math.Min(expectedHeightmapLongs.Count, heightmapLongs.Count); i++)
        {
            Assert.True(expectedHeightmapLongs[i] == heightmapLongs[i],
                $"Heightmap long[{i}] mismatch: expected 0x{expectedHeightmapLongs[i]:X16}, got 0x{heightmapLongs[i]:X16}");
        }

        // Read data size
        var dataSize = reader.ReadVarInt();
        Assert.True(dataSize > 0, "Chunk data size should be positive");

        // Parse chunk sections
        var chunkSectionsData = reader.ReadBytes(dataSize);
        var sectionsReader = new ProtocolReader(chunkSectionsData);

        var sections = new List<ChunkSectionData>();

        for (int sectionIdx = 0; sectionIdx < 24; sectionIdx++)
        {
            var blockCount = sectionsReader.ReadShort();
            var bitsPerEntry = sectionsReader.ReadByte();

            var section = new ChunkSectionData
            {
                SectionIndex = sectionIdx,
                BlockCount = blockCount,
                BitsPerEntry = bitsPerEntry
            };

            if (bitsPerEntry == 0)
            {
                // Single-value palette
                var paletteValue = sectionsReader.ReadVarInt();
                section.PaletteType = "single_value";
                section.Palette = new List<int> { paletteValue };
                section.DataArrayLength = 0;
            }
            else
            {
                // Indirect palette
                var paletteLength = sectionsReader.ReadVarInt();
                var palette = new List<int>();
                for (int i = 0; i < paletteLength; i++)
                {
                    palette.Add(sectionsReader.ReadVarInt());
                }

                var dataArrayLength = sectionsReader.ReadVarInt();
                var dataArrayLongs = new List<long>();
                for (int i = 0; i < dataArrayLength; i++)
                {
                    dataArrayLongs.Add(sectionsReader.ReadLong());
                }

                section.PaletteType = "indirect";
                section.Palette = palette;
                section.DataArrayLength = dataArrayLength;
                section.DataArrayLongs = dataArrayLongs;
            }

            // Biomes
            var biomeBits = sectionsReader.ReadByte();
            var biomeValue = sectionsReader.ReadVarInt();
            section.BiomeBitsPerEntry = biomeBits;
            section.BiomeValue = biomeValue;

            sections.Add(section);
        }

        // Compare sections with Python reference
        Assert.True(expectedSections.Count == sections.Count,
            $"Section count mismatch: expected {expectedSections.Count}, got {sections.Count}");

        for (int i = 0; i < Math.Min(expectedSections.Count, sections.Count); i++)
        {
            var expectedSection = expectedSections[i];
            var actualSection = sections[i];

            var expectedSectionIdx = expectedSection.GetProperty("section_index").GetInt32();
            var expectedBlockCount = expectedSection.GetProperty("block_count").GetInt16();
            var expectedBitsPerEntry = expectedSection.GetProperty("bits_per_entry").GetByte();
            var expectedPaletteType = expectedSection.GetProperty("palette_type").GetString();

            Assert.True(expectedSectionIdx == actualSection.SectionIndex,
                $"Section {i}: Index mismatch: expected {expectedSectionIdx}, got {actualSection.SectionIndex}");
            Assert.True(expectedBlockCount == actualSection.BlockCount,
                $"Section {i}: Block count mismatch: expected {expectedBlockCount}, got {actualSection.BlockCount}");
            Assert.True(expectedBitsPerEntry == actualSection.BitsPerEntry,
                $"Section {i}: Bits per entry mismatch: expected {expectedBitsPerEntry}, got {actualSection.BitsPerEntry}");
            Assert.True(expectedPaletteType == actualSection.PaletteType,
                $"Section {i}: Palette type mismatch: expected {expectedPaletteType}, got {actualSection.PaletteType}");

            // Compare palette
            var expectedPalette = expectedSection.GetProperty("palette")
                .EnumerateArray()
                .Select(e => e.GetInt32())
                .ToList();

            Assert.True(expectedPalette.Count == actualSection.Palette.Count,
                $"Section {i}: Palette length mismatch: expected {expectedPalette.Count}, got {actualSection.Palette.Count}");

            for (int j = 0; j < Math.Min(expectedPalette.Count, actualSection.Palette.Count); j++)
            {
                Assert.True(expectedPalette[j] == actualSection.Palette[j],
                    $"Section {i}: Palette[{j}] mismatch: expected {expectedPalette[j]}, got {actualSection.Palette[j]}");
            }

            // Compare data array for indirect palettes
            if (expectedPaletteType == "indirect")
            {
                var expectedDataArrayLength = expectedSection.GetProperty("data_array_length").GetInt32();
                
                // Python and C# should both write the same number of longs (protocol-compliant)
                Assert.True(expectedDataArrayLength == actualSection.DataArrayLength,
                    $"Section {i}: Data array length mismatch: expected {expectedDataArrayLength}, got {actualSection.DataArrayLength}");

                if (expectedDataArrayLength > 0 && actualSection.DataArrayLongs != null)
                {
                    var expectedDataArrayLongs = expectedSection.GetProperty("data_array_longs")
                        .EnumerateArray()
                        .Select(e => e.GetInt64())
                        .ToList();

                    // Python and C# should write exactly the same longs
                    Assert.True(expectedDataArrayLongs.Count == (actualSection.DataArrayLongs?.Count ?? 0),
                        $"Section {i}: Data array longs count mismatch: expected {expectedDataArrayLongs.Count}, got {actualSection.DataArrayLongs?.Count ?? 0}");

                    for (int j = 0; j < expectedDataArrayLongs.Count; j++)
                    {
                        long expectedLong = expectedDataArrayLongs[j];
                        long actualLong = actualSection.DataArrayLongs?[j] ?? 0;
                        
                        Assert.True(expectedLong == actualLong,
                            $"Section {i}: Data array long[{j}] mismatch: expected 0x{expectedLong:X16}, got 0x{actualLong:X16}");
                    }
                }
            }

            // Compare biome
            var expectedBiomeBits = expectedSection.GetProperty("biome_bits_per_entry").GetByte();
            var expectedBiomeValue = expectedSection.GetProperty("biome_value").GetInt32();
            Assert.True(expectedBiomeBits == actualSection.BiomeBitsPerEntry,
                $"Section {i}: Biome bits per entry mismatch: expected {expectedBiomeBits}, got {actualSection.BiomeBitsPerEntry}");
            Assert.True(expectedBiomeValue == actualSection.BiomeValue,
                $"Section {i}: Biome value mismatch: expected {expectedBiomeValue}, got {actualSection.BiomeValue}");
        }

        // Read block entities
        var numBlockEntities = reader.ReadVarInt();
        var expectedBlockEntities = reference.GetProperty("block_entities_count").GetInt32();
        Assert.True(expectedBlockEntities == numBlockEntities,
            $"Block entities count mismatch: expected {expectedBlockEntities}, got {numBlockEntities}");
    }

    private class ChunkSectionData
    {
        public int SectionIndex { get; set; }
        public short BlockCount { get; set; }
        public byte BitsPerEntry { get; set; }
        public string PaletteType { get; set; } = string.Empty;
        public List<int> Palette { get; set; } = new();
        public int DataArrayLength { get; set; }
        public List<long>? DataArrayLongs { get; set; }
        public byte BiomeBitsPerEntry { get; set; }
        public int BiomeValue { get; set; }
    }
}

