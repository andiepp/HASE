using System.Buffers.Binary;
using System.Text;

namespace Hase.Protocol;

/// <summary>
/// Reads values using the HASE protocol version 1 binary encoding rules.
/// </summary>
internal sealed class BinaryProtocolReader
{
    private static readonly UTF8Encoding StrictUtf8 =
        new(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

    private readonly ReadOnlyMemory<byte> _buffer;
    private int _position;

    public BinaryProtocolReader(
        ReadOnlyMemory<byte> buffer)
    {
        _buffer = buffer;
    }

    /// <summary>
    /// Gets the number of unread bytes.
    /// </summary>
    public int Remaining =>
        _buffer.Length - _position;

    /// <summary>
    /// Reads one unsigned byte.
    /// </summary>
    public byte ReadByte()
    {
        EnsureAvailable(1);

        byte value =
            _buffer.Span[_position];

        _position++;

        return value;
    }

    /// <summary>
    /// Reads an unsigned 16-bit integer in little-endian byte order.
    /// </summary>
    public ushort ReadUInt16()
    {
        EnsureAvailable(sizeof(ushort));

        ushort value =
            BinaryPrimitives.ReadUInt16LittleEndian(
                _buffer.Span.Slice(
                    _position,
                    sizeof(ushort)));

        _position += sizeof(ushort);

        return value;
    }

    /// <summary>
    /// Reads an IEEE 754 binary64 value in little-endian byte order.
    /// </summary>
    public double ReadDouble()
    {
        EnsureAvailable(sizeof(double));

        double value =
            BinaryPrimitives.ReadDoubleLittleEndian(
                _buffer.Span.Slice(
                    _position,
                    sizeof(double)));

        _position += sizeof(double);

        return value;
    }

    /// <summary>
    /// Reads a collection count encoded as an unsigned 16-bit integer.
    /// </summary>
    public int ReadCount()
    {
        return ReadUInt16();
    }

    /// <summary>
    /// Reads a UTF-8 string prefixed by its unsigned 16-bit byte length.
    /// </summary>
    public string ReadString()
    {
        int byteCount =
            ReadUInt16();

        EnsureAvailable(byteCount);

        ReadOnlySpan<byte> bytes =
            _buffer.Span.Slice(
                _position,
                byteCount);

        _position += byteCount;

        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException(
                "The protocol payload contains an invalid UTF-8 string.",
                exception);
        }
    }

    /// <summary>
    /// Verifies that the complete payload has been consumed.
    /// </summary>
    public void EnsureFullyConsumed()
    {
        if (Remaining != 0)
        {
            throw new InvalidDataException(
                $"The protocol payload contains {Remaining} " +
                "unexpected trailing byte(s).");
        }
    }

    private void EnsureAvailable(
        int byteCount)
    {
        if (byteCount < 0 || Remaining < byteCount)
        {
            throw new InvalidDataException(
                $"The protocol payload ended unexpectedly. " +
                $"Requested {byteCount} byte(s), but only " +
                $"{Remaining} byte(s) remain.");
        }
    }
}