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

    /// <summary>
    /// Writes an Angle (1 byte).
    /// Angle is encoded as steps of 1/256 of a full turn.
    /// Formula: byteValue = (angleInDegrees * 256 / 360) mod 256
    /// </summary>
    public ProtocolWriter WriteAngle(float angleInDegrees)
    {
        // Convert degrees to byte: (angle * 256 / 360) mod 256
        // Normalize angle to 0-360 range first
        float normalizedAngle = angleInDegrees % 360f;
        if (normalizedAngle < 0)
        {
            normalizedAngle += 360f;
        }
        
        byte angleByte = (byte)Math.Round(normalizedAngle * 256f / 360f);
        _buffer.Add(angleByte);
        return this;
    }

    /// <summary>
    /// Writes an LpVec3 (Low Precision Vec3).
    /// Encodes 3 doubles in a compact format (usually 6 bytes).
    /// Used for velocity in Spawn Entity and Set Entity Velocity packets.
    /// 
    /// Implementation based on protocol spec pseudocode.
    /// </summary>
    public ProtocolWriter WriteLpVec3(double x, double y, double z)
    {
        // Clamp values to valid range
        const double MAX_VALUE = 1.7179869183e10;
        x = Math.Max(-MAX_VALUE, Math.Min(MAX_VALUE, x));
        y = Math.Max(-MAX_VALUE, Math.Min(MAX_VALUE, y));
        z = Math.Max(-MAX_VALUE, Math.Min(MAX_VALUE, z));
        
        // Check for NaN
        if (double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(z))
        {
            x = y = z = 0.0;
        }
        
        // Find the absolute maximum of all three values
        double maxCoordinate = Math.Max(Math.Max(Math.Abs(x), Math.Abs(y)), Math.Abs(z));
        
        // If all values are very small (< 3.051944088384301e-5), encode as single byte 0x00
        if (maxCoordinate < 3.051944088384301e-5)
        {
            _buffer.Add(0x00);
            return this;
        }
        
        // Calculate scale factor (rounded up)
        long maxCoordinateI = (long)maxCoordinate;
        long scaleFactor = maxCoordinate > (double)maxCoordinateI ? maxCoordinateI + 1L : maxCoordinateI;
        
        // Check if continuation is needed (if scaleFactor >= 3)
        bool needContinuation = (scaleFactor & 3L) != scaleFactor;
        
        // Pack function: (value * 0.5 + 0.5) * 32766
        const double MAX_QUANTIZED_VALUE = 32766.0;
        long pack(double value)
        {
            double normalized = (value / (double)scaleFactor) * 0.5 + 0.5; // Scale to 0-1
            return (long)Math.Round(normalized * MAX_QUANTIZED_VALUE);
        }
        
        long packedX = pack(x) << 3;
        long packedY = pack(y) << 18;
        long packedZ = pack(z) << 33;
        
        // Pack scale factor (2 bits) with continuation flag if needed
        long packedScale = needContinuation ? (scaleFactor & 3L) | 4L : scaleFactor;
        
        // Combine all packed values
        long packed = packedZ | packedY | packedX | packedScale;
        
        // Write first 2 bytes (little-endian)
        _buffer.Add((byte)(packed & 0xFF));
        _buffer.Add((byte)((packed >> 8) & 0xFF));
        
        // Write next 4 bytes as int (big-endian)
        int upperBits = (int)(packed >> 16);
        _buffer.Add((byte)((upperBits >> 24) & 0xFF));
        _buffer.Add((byte)((upperBits >> 16) & 0xFF));
        _buffer.Add((byte)((upperBits >> 8) & 0xFF));
        _buffer.Add((byte)(upperBits & 0xFF));
        
        // If continuation needed, write remaining scale factor as VarInt
        if (needContinuation)
        {
            WriteVarInt((int)(scaleFactor >> 2));
        }
        
        return this;
    }

    /// <summary>
    /// Writes a Fixed BitSet (n bits).
    /// A bitset where each bit corresponds to an enum variant.
    /// Size is ceil(n / 8) bytes.
    /// </summary>
    /// <param name="bits">The bits to set (bit 0 = first enum, bit 1 = second enum, etc.)</param>
    /// <param name="numBits">The number of bits in the bitset (number of enum variants)</param>
    public ProtocolWriter WriteFixedBitSet(int bits, int numBits)
    {
        int numBytes = (int)Math.Ceiling(numBits / 8.0);
        
        for (int i = 0; i < numBytes; i++)
        {
            byte b = 0;
            for (int bitIndex = 0; bitIndex < 8; bitIndex++)
            {
                int globalBitIndex = i * 8 + bitIndex;
                if (globalBitIndex < numBits)
                {
                    if ((bits & (1 << globalBitIndex)) != 0)
                    {
                        b |= (byte)(1 << bitIndex);
                    }
                }
            }
            _buffer.Add(b);
        }
        
        return this;
    }
}

