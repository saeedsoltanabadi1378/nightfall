using System.Text;

namespace Nightfall.Infrastructure.Agora;

/// <summary>
/// Little-endian byte packing matching Agora's AccessToken2 wire format (see AgoraAccessToken2.cs).
/// Explicit shifts instead of BitConverter so output doesn't depend on host endianness.
/// </summary>
internal sealed class AgoraByteWriter
{
    private readonly List<byte> _buffer = new();

    public AgoraByteWriter WriteUInt16(ushort value)
    {
        _buffer.Add((byte)(value & 0xFF));
        _buffer.Add((byte)((value >> 8) & 0xFF));
        return this;
    }

    public AgoraByteWriter WriteUInt32(uint value)
    {
        _buffer.Add((byte)(value & 0xFF));
        _buffer.Add((byte)((value >> 8) & 0xFF));
        _buffer.Add((byte)((value >> 16) & 0xFF));
        _buffer.Add((byte)((value >> 24) & 0xFF));
        return this;
    }

    /// <summary>Length-prefixed (uint16 byte count) raw bytes.</summary>
    public AgoraByteWriter WriteBytes(byte[] value)
    {
        WriteUInt16((ushort)value.Length);
        _buffer.AddRange(value);
        return this;
    }

    public AgoraByteWriter WriteString(string value) => WriteBytes(Encoding.UTF8.GetBytes(value));

    /// <summary>Appends bytes with no length prefix (used to splice an already-packed sub-section in).</summary>
    public AgoraByteWriter WriteRaw(byte[] value)
    {
        _buffer.AddRange(value);
        return this;
    }

    /// <summary>uint16 count, then (uint16 key, uint32 value) pairs in insertion order.</summary>
    public AgoraByteWriter WritePrivilegeMap(IReadOnlyDictionary<ushort, uint> privileges)
    {
        WriteUInt16((ushort)privileges.Count);
        foreach (var (key, value) in privileges)
        {
            WriteUInt16(key);
            WriteUInt32(value);
        }
        return this;
    }

    public byte[] ToArray() => _buffer.ToArray();
}
