using System.Text;

namespace MineSharp.Core.Protocol;

/// <summary>
/// Reads Minecraft protocol data types from bytes.
/// </summary>
public class ProtocolReader
{
    private readonly byte[] _data;
    private int _offset;

    public ProtocolReader(byte[] data, int offset = 0)
    {
        _data = data;
        _offset = offset;
    }

    public int Offset => _offset;
    public int Remaining => _data.Length - _offset;

    public int ReadVarInt()
    {
        uint result = 0;
        int shift = 0;

        while (true)
        {
            if (_offset >= _data.Length)
                throw new InvalidOperationException("Not enough data for VarInt");

            byte b = _data[_offset];
            _offset++;

            result |= (uint)((b & 0x7F) << shift);

            if ((b & 0x80) == 0)
                break;

            shift += 7;
            if (shift >= 32)
                throw new InvalidOperationException("VarInt too long");
        }

        // Convert to signed integer (two's complement)
        // Casting uint to int handles two's complement automatically
        return unchecked((int)result);
    }

    public long ReadVarLong()
    {
        long result = 0;
        int shift = 0;

        while (true)
        {
            if (_offset >= _data.Length)
                throw new InvalidOperationException("Not enough data for VarLong");

            byte b = _data[_offset];
            _offset++;

            result |= (long)(b & 0x7F) << shift;

            if ((b & 0x80) == 0)
                break;

            shift += 7;
            if (shift >= 64)
                throw new InvalidOperationException("VarLong too long");
        }

        // Convert to signed long (two's complement)
        // Casting ulong to long handles two's complement automatically
        return unchecked((long)result);
    }

    public string ReadString(int maxLength = 32767)
    {
        int length = ReadVarInt();
        if (length < 0 || length > maxLength * 3) // UTF-8 can be up to 3 bytes per char
            throw new InvalidOperationException($"Invalid string length: {length}");

        if (_offset + length > _data.Length)
            throw new InvalidOperationException("Not enough data for string");

        byte[] stringBytes = new byte[length];
        Array.Copy(_data, _offset, stringBytes, 0, length);
        _offset += length;

        return Encoding.UTF8.GetString(stringBytes);
    }

    public ushort ReadUnsignedShort()
    {
        if (_offset + 2 > _data.Length)
            throw new InvalidOperationException("Not enough data for unsigned short");

        ushort value = (ushort)((_data[_offset] << 8) | _data[_offset + 1]);
        _offset += 2;
        return value;
    }

    public short ReadShort()
    {
        if (_offset + 2 > _data.Length)
            throw new InvalidOperationException("Not enough data for short");

        short value = (short)((_data[_offset] << 8) | _data[_offset + 1]);
        _offset += 2;
        return value;
    }

    public int ReadInt()
    {
        if (_offset + 4 > _data.Length)
            throw new InvalidOperationException("Not enough data for int");

        int value = (_data[_offset] << 24) | (_data[_offset + 1] << 16) | 
                    (_data[_offset + 2] << 8) | _data[_offset + 3];
        _offset += 4;
        return value;
    }

    public long ReadLong()
    {
        if (_offset + 8 > _data.Length)
            throw new InvalidOperationException("Not enough data for long");

        long value = ((long)_data[_offset] << 56) | ((long)_data[_offset + 1] << 48) |
                     ((long)_data[_offset + 2] << 40) | ((long)_data[_offset + 3] << 32) |
                     ((long)_data[_offset + 4] << 24) | ((long)_data[_offset + 5] << 16) |
                     ((long)_data[_offset + 6] << 8) | _data[_offset + 7];
        _offset += 8;
        return value;
    }

    public float ReadFloat()
    {
        if (_offset + 4 > _data.Length)
            throw new InvalidOperationException("Not enough data for float");

        int bits = ReadInt();
        return BitConverter.Int32BitsToSingle(bits);
    }

    public double ReadDouble()
    {
        if (_offset + 8 > _data.Length)
            throw new InvalidOperationException("Not enough data for double");

        long bits = ReadLong();
        return BitConverter.Int64BitsToDouble(bits);
    }

    public bool ReadBool()
    {
        return ReadByte() != 0;
    }

    public byte ReadByte()
    {
        if (_offset >= _data.Length)
            throw new InvalidOperationException("Not enough data for byte");

        return _data[_offset++];
    }

    public byte[] ReadBytes(int count)
    {
        if (_offset + count > _data.Length)
            throw new InvalidOperationException($"Not enough data: need {count} bytes");

        byte[] result = new byte[count];
        Array.Copy(_data, _offset, result, 0, count);
        _offset += count;
        return result;
    }

    public byte[] ReadRemainingBytes()
    {
        int remaining = Remaining;
        if (remaining == 0)
            return Array.Empty<byte>();

        return ReadBytes(remaining);
    }

    public Guid ReadUuid()
    {
        if (_offset + 16 > _data.Length)
            throw new InvalidOperationException("Not enough data for UUID");

        byte[] uuidBytes = new byte[16];
        Array.Copy(_data, _offset, uuidBytes, 0, 16);
        _offset += 16;

        // UUID is stored as big-endian, but Guid constructor expects little-endian
        // So we need to swap bytes for .NET Guid
        if (BitConverter.IsLittleEndian)
        {
            // Swap bytes for .NET Guid format
            byte[] swapped = new byte[16];
            swapped[0] = uuidBytes[3];
            swapped[1] = uuidBytes[2];
            swapped[2] = uuidBytes[1];
            swapped[3] = uuidBytes[0];
            swapped[4] = uuidBytes[5];
            swapped[5] = uuidBytes[4];
            swapped[6] = uuidBytes[7];
            swapped[7] = uuidBytes[6];
            Array.Copy(uuidBytes, 8, swapped, 8, 8);
            return new Guid(swapped);
        }

        return new Guid(uuidBytes);
    }
}

