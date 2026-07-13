using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using Hase.Protocol;
using Hase.ProtocolExplorer.Tracing.Model;

namespace Hase.ProtocolExplorer.Formatting.Payload;

internal sealed class EventNotificationPayloadFormatter
{
    public IReadOnlyList<PayloadField> Format(
        EventNotification notification,
        ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(
            notification);

        List<PayloadField> fields = [];

        int offset = 0;

        offset = AddString(
            fields,
            payload,
            offset,
            "InstrumentId");

        offset = AddString(
            fields,
            payload,
            offset,
            "EventPath");

        ReadOnlyMemory<byte> timestampBytes =
            ReadBytes(
                payload,
                offset,
                sizeof(long),
                "TimestampUtc");

        long unixTimeMilliseconds =
            BinaryPrimitives.ReadInt64LittleEndian(
                timestampBytes.Span);

        DateTimeOffset timestampUtc =
            DateTimeOffset.FromUnixTimeMilliseconds(
                unixTimeMilliseconds);

        fields.Add(
            new PayloadField(
                offset,
                timestampBytes,
                $"TimestampUtc = {timestampUtc:O} " +
                $"(Unix milliseconds = {unixTimeMilliseconds})"));

        offset += sizeof(long);

        ReadOnlyMemory<byte> variantTypeBytes =
            ReadBytes(
                payload,
                offset,
                1,
                "Variant Type");

        fields.Add(
            new PayloadField(
                offset,
                variantTypeBytes,
                $"Variant Type = {GetVariantTypeName(notification.Value)} " +
                $"(encoded value = {variantTypeBytes.Span[0]})"));

        offset++;

        offset = AddVariantValue(
            fields,
            payload,
            offset,
            notification.Value);

        if (offset != payload.Length)
        {
            throw new InvalidDataException(
                $"The EventNotification payload contains " +
                $"{payload.Length} bytes, but {offset} bytes " +
                "were interpreted.");
        }

        return fields;
    }

    private static int AddString(
        ICollection<PayloadField> fields,
        ReadOnlyMemory<byte> payload,
        int offset,
        string name)
    {
        ReadOnlyMemory<byte> lengthBytes =
            ReadBytes(
                payload,
                offset,
                sizeof(ushort),
                $"{name} Length");

        ushort byteCount =
            BinaryPrimitives.ReadUInt16LittleEndian(
                lengthBytes.Span);

        fields.Add(
            new PayloadField(
                offset,
                lengthBytes,
                $"{name} byte length = {byteCount}"));

        offset += sizeof(ushort);

        ReadOnlyMemory<byte> valueBytes =
            ReadBytes(
                payload,
                offset,
                byteCount,
                name);

        string value =
            Encoding.UTF8.GetString(
                valueBytes.Span);

        fields.Add(
            new PayloadField(
                offset,
                valueBytes,
                $"{name} = \"{value}\""));

        return offset + byteCount;
    }

    private static int AddVariantValue(
        ICollection<PayloadField> fields,
        ReadOnlyMemory<byte> payload,
        int offset,
        object? value)
    {
        switch (value)
        {
            case null:
                return offset;

            case bool:
                {
                    ReadOnlyMemory<byte> valueBytes =
                        ReadBytes(
                            payload,
                            offset,
                            1,
                            "Boolean Value");

                    byte encodedValue =
                        valueBytes.Span[0];

                    bool booleanValue =
                        encodedValue switch
                        {
                            0 => false,
                            1 => true,

                            _ => throw new InvalidDataException(
                                $"Invalid encoded Boolean value " +
                                $"'{encodedValue}'.")
                        };

                    fields.Add(
                        new PayloadField(
                            offset,
                            valueBytes,
                            $"Boolean Value = {booleanValue}"));

                    return offset + 1;
                }

            case int:
                {
                    ReadOnlyMemory<byte> valueBytes =
                        ReadBytes(
                            payload,
                            offset,
                            sizeof(int),
                            "Int32 Value");

                    int int32Value =
                        BinaryPrimitives.ReadInt32LittleEndian(
                            valueBytes.Span);

                    fields.Add(
                        new PayloadField(
                            offset,
                            valueBytes,
                            $"Int32 Value = {int32Value}"));

                    return offset + sizeof(int);
                }

            case long:
                {
                    ReadOnlyMemory<byte> valueBytes =
                        ReadBytes(
                            payload,
                            offset,
                            sizeof(long),
                            "Int64 Value");

                    long int64Value =
                        BinaryPrimitives.ReadInt64LittleEndian(
                            valueBytes.Span);

                    fields.Add(
                        new PayloadField(
                            offset,
                            valueBytes,
                            $"Int64 Value = {int64Value}"));

                    return offset + sizeof(long);
                }

            case double:
                {
                    ReadOnlyMemory<byte> valueBytes =
                        ReadBytes(
                            payload,
                            offset,
                            sizeof(double),
                            "Double Value");

                    double doubleValue =
                        BinaryPrimitives.ReadDoubleLittleEndian(
                            valueBytes.Span);

                    fields.Add(
                        new PayloadField(
                            offset,
                            valueBytes,
                            $"Double Value = " +
                            $"{doubleValue.ToString(CultureInfo.InvariantCulture)}"));

                    return offset + sizeof(double);
                }

            case string:
                {
                    ReadOnlyMemory<byte> lengthBytes =
                        ReadBytes(
                            payload,
                            offset,
                            sizeof(ushort),
                            "String Length");

                    ushort byteCount =
                        BinaryPrimitives.ReadUInt16LittleEndian(
                            lengthBytes.Span);

                    fields.Add(
                        new PayloadField(
                            offset,
                            lengthBytes,
                            $"String byte length = {byteCount}"));

                    offset += sizeof(ushort);

                    ReadOnlyMemory<byte> valueBytes =
                        ReadBytes(
                            payload,
                            offset,
                            byteCount,
                            "String Value");

                    string stringValue =
                        Encoding.UTF8.GetString(
                            valueBytes.Span);

                    fields.Add(
                        new PayloadField(
                            offset,
                            valueBytes,
                            $"String Value = \"{stringValue}\""));

                    return offset + byteCount;
                }

            default:
                throw new NotSupportedException(
                    $"CLR type '{value.GetType().FullName}' is not " +
                    "supported by the protocol variant formatter.");
        }
    }

    private static string GetVariantTypeName(
        object? value)
    {
        return value switch
        {
            null => "Null",
            bool => "Boolean",
            int => "Int32",
            long => "Int64",
            double => "Double",
            string => "String",

            _ => throw new NotSupportedException(
                $"CLR type '{value.GetType().FullName}' is not " +
                "supported by the protocol variant formatter.")
        };
    }

    private static ReadOnlyMemory<byte> ReadBytes(
        ReadOnlyMemory<byte> payload,
        int offset,
        int length,
        string fieldName)
    {
        if (offset < 0 ||
            length < 0 ||
            offset > payload.Length - length)
        {
            throw new InvalidDataException(
                $"The EventNotification payload ended while " +
                $"reading '{fieldName}' at offset {offset}.");
        }

        return payload.Slice(
            offset,
            length);
    }
}