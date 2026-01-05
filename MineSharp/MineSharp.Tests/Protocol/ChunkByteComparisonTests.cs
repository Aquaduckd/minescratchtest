using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using MineSharp.Core.Protocol;
using MineSharp.World;
using MineSharp.Data;

namespace MineSharp.Tests.Protocol;

/// <summary>
/// Tests to compare C# chunk packet bytes with Python reference.
/// </summary>
public class ChunkByteComparisonTests
{
    [Fact]
    public void GenerateChunkPacketForComparison()
    {
        // Load registries (needed for BlockManager)
        var registryManager = new RegistryManager();
        var dataPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "extracted_data");
        registryManager.LoadRegistries(dataPath);
        
        // Create block manager directly
        var blockManager = new BlockManager(useTerrainGeneration: false);
        
        // Test chunk that's crashing: (-2, -1)
        int chunkX = -2;
        int chunkZ = -1;
        
        // Generate chunk
        blockManager.GetOrCreateChunk(chunkX, chunkZ);
        
        // Build chunk packet
        var chunkPacket = PacketBuilder.BuildChunkDataPacket(chunkX, chunkZ, blockManager);
        
        // Write to file for comparison
        var outputFile = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", $"chunk_csharp_{chunkX}_{chunkZ}.bin");
        File.WriteAllBytes(outputFile, chunkPacket);
        
        Console.WriteLine($"C# chunk packet generated:");
        Console.WriteLine($"  Length: {chunkPacket.Length} bytes");
        Console.WriteLine($"  Saved to: {outputFile}");
        
        // Analyze structure
        AnalyzePacketStructure(chunkPacket, "C# Chunk Packet");
        
        // Compare with Python if available
        var pythonFile = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", $"chunk_python_{chunkX}_{chunkZ}.bin");
        if (File.Exists(pythonFile))
        {
            var pythonPacket = File.ReadAllBytes(pythonFile);
            ComparePackets(chunkPacket, pythonPacket);
        }
        else
        {
            Console.WriteLine($"Python reference file not found: {pythonFile}");
            Console.WriteLine("Run compare_chunk_bytes.py first to generate Python reference");
        }
    }
    
    private void AnalyzePacketStructure(byte[] packet, string label)
    {
        Console.WriteLine($"\n{'='*60}");
        Console.WriteLine(label);
        Console.WriteLine($"{'='*60}");
        Console.WriteLine($"Total packet length: {packet.Length} bytes");
        
        // Parse packet
        var reader = new ProtocolReader(packet);
        
        // Read packet length
        int packetLength = reader.ReadVarInt();
        Console.WriteLine($"  Packet length (VarInt): {packetLength}");
        
        // Read packet ID
        int packetId = reader.ReadVarInt();
        Console.WriteLine($"  Packet ID (VarInt): 0x{packetId:X2}");
        
        if (packetId == 0x2C) // Chunk Data
        {
            // Read chunk coordinates
            int chunkX = reader.ReadInt();
            int chunkZ = reader.ReadInt();
            Console.WriteLine($"  Chunk X: {chunkX}");
            Console.WriteLine($"  Chunk Z: {chunkZ}");
            
            // Read heightmaps
            int numHeightmaps = reader.ReadVarInt();
            Console.WriteLine($"  Number of heightmaps: {numHeightmaps}");
            
            if (numHeightmaps > 0)
            {
                int heightmapType = reader.ReadVarInt();
                Console.WriteLine($"    Heightmap type: {heightmapType}");
                int heightmapLength = reader.ReadVarInt();
                Console.WriteLine($"    Heightmap data length (longs): {heightmapLength}");
                byte[] heightmapData = reader.ReadBytes(heightmapLength * 8);
                Console.WriteLine($"    Heightmap data: {heightmapData.Length} bytes");
            }
            
            // Read chunk data length
            int chunkDataLength = reader.ReadVarInt();
            Console.WriteLine($"  Chunk data length: {chunkDataLength} bytes");
            
            // Analyze first section
            if (chunkDataLength > 0)
            {
                int chunkDataStart = reader.Offset;
                Console.WriteLine($"  Chunk data starts at offset: {chunkDataStart}");
                
                short blockCount = reader.ReadShort();
                Console.WriteLine($"    Section 0 - Block count: {blockCount}");
                
                byte bitsPerEntry = reader.ReadByte();
                Console.WriteLine($"    Section 0 - Bits per entry: {bitsPerEntry}");
                
                if (bitsPerEntry == 0)
                {
                    int singleValue = reader.ReadVarInt();
                    Console.WriteLine($"    Section 0 - Single value palette: {singleValue}");
                }
                else
                {
                    int paletteLength = reader.ReadVarInt();
                    Console.WriteLine($"    Section 0 - Palette length: {paletteLength}");
                    
                    var palette = new List<int>();
                    for (int i = 0; i < Math.Min(paletteLength, 10); i++)
                    {
                        palette.Add(reader.ReadVarInt());
                    }
                    Console.WriteLine($"    Section 0 - Palette (first {palette.Count}): [{string.Join(", ", palette)}]");
                    
                    if (paletteLength > 10)
                    {
                        // Skip remaining palette entries
                        for (int i = 10; i < paletteLength; i++)
                        {
                            reader.ReadVarInt();
                        }
                        Console.WriteLine($"    Section 0 - ... ({paletteLength - 10} more palette entries)");
                    }
                    
                    // Read data array length
                    int dataArrayLength = reader.ReadVarInt();
                    Console.WriteLine($"    Section 0 - Data array length (longs): {dataArrayLength}");
                    
                    // Read first few longs
                    int numLongsToRead = Math.Min(3, dataArrayLength);
                    for (int i = 0; i < numLongsToRead; i++)
                    {
                        if (reader.Remaining >= 8)
                        {
                            long longVal = reader.ReadLong();
                            Console.WriteLine($"      Long {i}: 0x{longVal:X16}");
                        }
                    }
                    
                    if (dataArrayLength > numLongsToRead)
                    {
                        Console.WriteLine($"      ... ({dataArrayLength - numLongsToRead} more longs)");
                    }
                }
                
                // Biome palette
                byte biomeBits = reader.ReadByte();
                Console.WriteLine($"    Section 0 - Biome bits per entry: {biomeBits}");
                if (biomeBits == 0)
                {
                    int biomeValue = reader.ReadVarInt();
                    Console.WriteLine($"    Section 0 - Biome single value: {biomeValue}");
                }
            }
            
            // Check remaining bytes
            int remaining = packet.Length - reader.Offset;
            Console.WriteLine($"\n  Remaining bytes after chunk data: {remaining}");
        }
    }
    
    private void ComparePackets(byte[] csharpPacket, byte[] pythonPacket)
    {
        Console.WriteLine($"\n{'='*60}");
        Console.WriteLine("Packet Comparison");
        Console.WriteLine($"{'='*60}");
        Console.WriteLine($"C# packet length: {csharpPacket.Length} bytes");
        Console.WriteLine($"Python packet length: {pythonPacket.Length} bytes");
        
        int minLength = Math.Min(csharpPacket.Length, pythonPacket.Length);
        int maxLength = Math.Max(csharpPacket.Length, pythonPacket.Length);
        
        if (csharpPacket.Length != pythonPacket.Length)
        {
            Console.WriteLine($"⚠️  Length mismatch: {csharpPacket.Length} vs {pythonPacket.Length} (diff: {Math.Abs(csharpPacket.Length - pythonPacket.Length)})");
        }
        
        // Compare byte by byte
        int firstDiff = -1;
        int diffCount = 0;
        for (int i = 0; i < minLength; i++)
        {
            if (csharpPacket[i] != pythonPacket[i])
            {
                if (firstDiff == -1)
                {
                    firstDiff = i;
                }
                diffCount++;
            }
        }
        
        if (firstDiff == -1 && csharpPacket.Length == pythonPacket.Length)
        {
            Console.WriteLine("✅ Packets are identical!");
        }
        else
        {
            Console.WriteLine($"❌ Packets differ:");
            Console.WriteLine($"   First difference at offset: {firstDiff} (0x{firstDiff:X})");
            Console.WriteLine($"   Total differences: {diffCount} bytes");
            Console.WriteLine($"   Length difference: {maxLength - minLength} bytes");
            
            // Show first difference
            if (firstDiff >= 0)
            {
                int contextStart = Math.Max(0, firstDiff - 16);
                int contextEnd = Math.Min(minLength, firstDiff + 16);
                
                Console.WriteLine($"\n   Context around first difference (offset {firstDiff}):");
                Console.WriteLine($"   C#:   {BitConverter.ToString(csharpPacket, contextStart, contextEnd - contextStart)}");
                Console.WriteLine($"   Py:   {BitConverter.ToString(pythonPacket, contextStart, contextEnd - contextStart)}");
                
                // Show hex dump
                Console.WriteLine($"\n   Hex dump (offset {contextStart}):");
                for (int i = contextStart; i < contextEnd; i += 16)
                {
                    int end = Math.Min(i + 16, contextEnd);
                    string csharpHex = string.Join(" ", csharpPacket.Skip(i).Take(end - i).Select(b => $"{b:X2}"));
                    string pythonHex = string.Join(" ", pythonPacket.Skip(i).Take(end - i).Select(b => $"{b:X2}"));
                    string marker = (i <= firstDiff && firstDiff < end) ? " <-- DIFF" : "";
                    Console.WriteLine($"   {i:X4}: C#={csharpHex,-48} Py={pythonHex,-48}{marker}");
                }
            }
        }
    }
}

