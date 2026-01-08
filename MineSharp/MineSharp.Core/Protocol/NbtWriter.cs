using System.Text;
using System.Text.Json;

namespace MineSharp.Core.Protocol;

/// <summary>
/// Writes NBT (Named Binary Tag) data in binary format.
/// Used for encoding text components and other NBT data in packets.
/// </summary>
public class NbtWriter
{
    private readonly List<byte> _buffer;

    public NbtWriter()
    {
        _buffer = new List<byte>();
    }

    public byte[] ToArray()
    {
        return _buffer.ToArray();
    }

    /// <summary>
    /// Writes a TAG_String (type 8).
    /// Format: 2 bytes (unsigned short, big-endian) for length, then UTF-8 bytes.
    /// </summary>
    public NbtWriter WriteString(string value)
    {
        byte[] stringBytes = Encoding.UTF8.GetBytes(value);
        if (stringBytes.Length > 65535)
        {
            throw new ArgumentException($"String too long for NBT: {stringBytes.Length} bytes");
        }

        // Write length as unsigned short (big-endian)
        ushort length = (ushort)stringBytes.Length;
        _buffer.Add((byte)(length >> 8));
        _buffer.Add((byte)(length & 0xFF));
        _buffer.AddRange(stringBytes);
        return this;
    }

    /// <summary>
    /// Writes a TAG_Compound (type 10).
    /// Format: tag type (1 byte), then tags, then TAG_End (1 byte).
    /// </summary>
    public NbtWriter WriteCompoundStart()
    {
        _buffer.Add(10); // TAG_Compound
        return this;
    }

    /// <summary>
    /// Writes TAG_End (type 0) to close a compound.
    /// </summary>
    public NbtWriter WriteCompoundEnd()
    {
        _buffer.Add(0); // TAG_End
        return this;
    }

    /// <summary>
    /// Writes a named tag within a compound.
    /// Format: tag type (1 byte), name length (2 bytes), name (UTF-8), payload.
    /// </summary>
    public NbtWriter WriteNamedTag(byte tagType, string name, Action<NbtWriter> writePayload)
    {
        _buffer.Add(tagType);
        
        // Write name length and name
        byte[] nameBytes = Encoding.UTF8.GetBytes(name);
        if (nameBytes.Length > 65535)
        {
            throw new ArgumentException($"Tag name too long: {nameBytes.Length} bytes");
        }
        
        ushort nameLength = (ushort)nameBytes.Length;
        _buffer.Add((byte)(nameLength >> 8));
        _buffer.Add((byte)(nameLength & 0xFF));
        _buffer.AddRange(nameBytes);
        
        // Write payload
        writePayload(this);
        return this;
    }

    /// <summary>
    /// Writes a named string tag within a compound.
    /// </summary>
    public NbtWriter WriteNamedString(string name, string value)
    {
        return WriteNamedTag(8, name, writer => writer.WriteString(value));
    }

    /// <summary>
    /// Converts a JSON text component to NBT binary format.
    /// Supports simple text components like {"text":"message"}.
    /// </summary>
    public static byte[] JsonTextComponentToNbt(string jsonTextComponent)
    {
        // Parse JSON to extract text component structure
        using JsonDocument doc = JsonDocument.Parse(jsonTextComponent);
        JsonElement root = doc.RootElement;

        var writer = new NbtWriter();
        writer.WriteCompoundStart();
        
        // Write all properties from the JSON object
        foreach (var property in root.EnumerateObject())
        {
            string propertyName = property.Name;
            JsonElement propertyValue = property.Value;

            if (propertyValue.ValueKind == JsonValueKind.String)
            {
                writer.WriteNamedString(propertyName, propertyValue.GetString() ?? "");
            }
            // TODO: Support other types (numbers, booleans, nested objects, arrays) if needed
        }
        
        writer.WriteCompoundEnd();
        return writer.ToArray();
    }
}

