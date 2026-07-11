using Hase.Core.Domain.Identity;
using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace Hase.Protocol;

/// <summary>
/// Writes values using the HASE protocol version 1 binary encoding rules.
/// </summary>
internal sealed class BinaryProtocolWriter
{
    private readonly ArrayBufferWriter<byte> _buffer = new();

    /// <summary>
    /// Writes one unsigned byte.
    /// </summary>
    public void WriteByte(byte value)
    {
        Span<byte> destination = _buffer.GetSpan(1);
        destination[0] = value;
        _buffer.Advance(1);
    }

    /// <summary>
    /// Writes an unsigned 16-bit integer in little-endian byte order.
    /// </summary>
    public void WriteUInt16(ushort value)
    {
        Span<byte> destination = _buffer.GetSpan(sizeof(ushort));

        BinaryPrimitives.WriteUInt16LittleEndian(
            destination,
            value);

        _buffer.Advance(sizeof(ushort));
    }

    /// <summary>
    /// Writes a collection count as an unsigned 16-bit integer.
    /// </summary>
    public void WriteCount(int count)
    {
        if (count is < 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                count,
                $"A protocol collection count must be between " +
                $"0 and {ushort.MaxValue}.");
        }

        WriteUInt16((ushort)count);
    }

    /// <summary>
    /// Writes a UTF-8 string prefixed by its unsigned 16-bit byte length.
    /// </summary>
    public void WriteString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        int byteCount = Encoding.UTF8.GetByteCount(value);

        if (byteCount > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                $"The UTF-8 representation of a protocol string " +
                $"must not exceed {ushort.MaxValue} bytes.");
        }

        WriteUInt16((ushort)byteCount);

        if (byteCount == 0)
        {
            return;
        }

        Span<byte> destination = _buffer.GetSpan(byteCount);

        int bytesWritten = Encoding.UTF8.GetBytes(
            value,
            destination);

        _buffer.Advance(bytesWritten);
    }

    public void WriteId(HaseId id)
    {
        ArgumentNullException.ThrowIfNull(id);

        WriteString(id.Value);
    }

    public void WriteId(EndpointId id)
    => WriteId((HaseId)id);

    public void WriteId(InstrumentId id)
        => WriteId((HaseId)id);

    public void WriteId(PropertyId id)
        => WriteId((HaseId)id);

    /// <summary>
    /// Returns a copy of all bytes written so far.
    /// </summary>
    public byte[] ToArray()
    {
        return _buffer.WrittenSpan.ToArray();
    }
}