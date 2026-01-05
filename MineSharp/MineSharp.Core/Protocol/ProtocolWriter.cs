using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace MineSharp.Core.Protocol;

/// <summary>
/// Writes Minecraft protocol data types to bytes.
/// </summary>
public class ProtocolWriter
{
    private readonly List<byte> _buffer;

    public ProtocolWriter()
    {
        _buffer = new List<byte>();
    }

    public byte[] ToArray()
    {
        return _buffer.ToArray();
    }

    public ProtocolWriter WriteVarInt(int value)
    {
        // Convert to unsigned for encoding
        uint unsignedValue = (uint)value;
        if (value < 0)
        {
            unsignedValue = (uint)(value + 0x100000000);
        }

        while (true)
        {
            byte b = (byte)(unsignedValue & 0x7F);
            unsignedValue >>= 7;

            if (unsignedValue != 0)
            {
                b |= 0x80;
            }

            _buffer.Add(b);

            if (unsignedValue == 0)
                break;
        }

        return this;
    }

    public ProtocolWriter WriteVarLong(long value)
    {
        // Convert to unsigned for encoding
        ulong unsignedValue = unchecked((ulong)value);

        while (true)
        {
            byte b = (byte)(unsignedValue & 0x7F);
            unsignedValue >>= 7;

            if (unsignedValue != 0)
            {
                b |= 0x80;
            }

            _buffer.Add(b);

            if (unsignedValue == 0)
                break;
        }

        return this;
    }

    public ProtocolWriter WriteString(string value, int maxLength = 32767)
    {
        byte[] stringBytes = Encoding.UTF8.GetBytes(value);
        if (stringBytes.Length > maxLength * 3)
            throw new ArgumentException($"String too long: {stringBytes.Length} bytes");

        WriteVarInt(stringBytes.Length);
        _buffer.AddRange(stringBytes);
        return this;
    }

    public ProtocolWriter WriteUnsignedShort(ushort value)
    {
        _buffer.Add((byte)(value >> 8));
        _buffer.Add((byte)(value & 0xFF));
        return this;
    }

    public ProtocolWriter WriteShort(short value)
    {
        _buffer.Add((byte)(value >> 8));
        _buffer.Add((byte)(value & 0xFF));
        return this;
    }

    public ProtocolWriter WriteInt(int value)
    {
        _buffer.Add((byte)(value >> 24));
        _buffer.Add((byte)(value >> 16));
        _buffer.Add((byte)(value >> 8));
        _buffer.Add((byte)value);
        return this;
    }

    public ProtocolWriter WriteLong(long value)
    {
        _buffer.Add((byte)(value >> 56));
        _buffer.Add((byte)(value >> 48));
        _buffer.Add((byte)(value >> 40));
        _buffer.Add((byte)(value >> 32));
        _buffer.Add((byte)(value >> 24));
        _buffer.Add((byte)(value >> 16));
        _buffer.Add((byte)(value >> 8));
        _buffer.Add((byte)value);
        return this;
    }

    public ProtocolWriter WriteFloat(float value)
    {
        int bits = BitConverter.SingleToInt32Bits(value);
        WriteInt(bits);
        return this;
    }

    public ProtocolWriter WriteDouble(double value)
    {
        long bits = BitConverter.DoubleToInt64Bits(value);
        WriteLong(bits);
        return this;
    }

    public ProtocolWriter WriteBool(bool value)
    {
        _buffer.Add(value ? (byte)1 : (byte)0);
        return this;
    }

    public ProtocolWriter WriteByte(byte value)
    {
        _buffer.Add(value);
        return this;
    }

    public ProtocolWriter WriteBytes(byte[] value)
    {
        _buffer.AddRange(value);
        return this;
    }

    public ProtocolWriter WriteUuid(Guid value)
    {
        byte[] uuidBytes = value.ToByteArray();
        
        // .NET Guid is little-endian, but Minecraft uses big-endian
        // So we need to swap bytes
        if (BitConverter.IsLittleEndian)
        {
            // Swap bytes for Minecraft format
            _buffer.Add(uuidBytes[3]);
            _buffer.Add(uuidBytes[2]);
            _buffer.Add(uuidBytes[1]);
            _buffer.Add(uuidBytes[0]);
            _buffer.Add(uuidBytes[5]);
            _buffer.Add(uuidBytes[4]);
            _buffer.Add(uuidBytes[7]);
            _buffer.Add(uuidBytes[6]);
            _buffer.AddRange(uuidBytes.Skip(8));
        }
        else
        {
            _buffer.AddRange(uuidBytes);
        }
        
        return this;
    }

    public ProtocolWriter WriteBitset(List<bool> bits)
    {
        // Calculate number of longs needed
        int numBits = bits.Count;
        int numLongs = (numBits + 63) / 64; // Ceiling division
        
        // Write length (number of longs)
        WriteVarInt(numLongs);
        
        // Pack bits into longs
        for (int longIdx = 0; longIdx < numLongs; longIdx++)
        {
            long longValue = 0;
            for (int bitIdx = 0; bitIdx < 64; bitIdx++)
            {
                int bitPos = longIdx * 64 + bitIdx;
                if (bitPos < numBits && bits[bitPos])
                {
                    // Set bit at position (bitIdx) in the long
                    longValue |= (1L << bitIdx);
                }
            }
            WriteLong(longValue);
        }
        
        return this;
    }

    /// <summary>
    /// Writes a PalettedContainer using Indirect palette format.
    /// </summary>
    public ProtocolWriter WritePalettedContainerIndirect(
        int bitsPerEntry,
        List<int> palette,
        List<int> dataArray)
    {
        // Bits per entry
        WriteByte((byte)bitsPerEntry);
        
        // Palette length
        WriteVarInt(palette.Count);
        
        // Palette array (VarInt IDs)
        foreach (var paletteId in palette)
        {
            WriteVarInt(paletteId);
        }
        
        // Data array: Pack entries into longs
        int entriesPerLong = 64 / bitsPerEntry;
        int numEntries = dataArray.Count;
        int numLongs = (numEntries + entriesPerLong - 1) / entriesPerLong;
        
        // Note: Protocol 1.21.5+ says length is calculated, not sent
        // Python does NOT write numLongs VarInt - it goes straight to the data array
        // The client calculates numLongs from bitsPerEntry and the number of entries
        
        // Pack data into longs
        for (int longIdx = 0; longIdx < numLongs; longIdx++)
        {
            ulong longValue = 0;
            for (int entryIdx = 0; entryIdx < entriesPerLong; entryIdx++)
            {
                int dataIdx = longIdx * entriesPerLong + entryIdx;
                if (dataIdx < numEntries)
                {
                    int entryValue = dataArray[dataIdx];
                    
                    // Validate entry value is within palette range
                    if (entryValue < 0 || entryValue >= palette.Count)
                    {
                        throw new ArgumentException(
                            $"Palette index {entryValue} out of range [0, {palette.Count - 1}] at data index {dataIdx}");
                    }
                    
                    int bitOffset = entryIdx * bitsPerEntry;
                    // Mask to ensure value fits in bitsPerEntry bits
                    ulong maskedValue = (ulong)(entryValue & ((1 << bitsPerEntry) - 1));
                    longValue |= (maskedValue << bitOffset);
                }
            }
            WriteLong((long)longValue);
        }
        
        return this;
    }
}

