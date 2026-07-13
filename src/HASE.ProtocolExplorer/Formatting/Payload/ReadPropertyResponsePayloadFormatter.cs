using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using Hase.Protocol;
using Hase.ProtocolExplorer.Tracing.Model;

namespace Hase.ProtocolExplorer.Formatting.Payload;

internal sealed class ReadPropertyResponsePayloadFormatter
{
    public IReadOnlyList<PayloadField> Format(
        ReadPropertyResponse response,
        ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(
            response);

        List<PayloadField> fields = [];

        int offset = 0;

        offset = AddProtocolResult(
            fields,
            payload,
            offset,
            response.Result);

        ReadOnlyMemory<byte> propertyValueMarkerBytes =
            ReadBytes(
                payload,
                offset,
                1,
                "PropertyValue Marker");

        byte propertyValueMarker =
            propertyValueMarkerBytes.Span[0];

        fields.Add(
            new PayloadField(
                offset,
                propertyValueMarkerBytes,
                $"PropertyValue Marker = " +
                $"{FormatMarker(propertyValueMarker)}"));

        offset++;

        switch (propertyValueMarker)
        {
            case 0:
                if (response.PropertyValue is not null)
                {
                    throw new InvalidDataException(
                        "The payload reports no PropertyValue, " +
                        "but the response contains one.");
                }

                break;

            case 1:
                if (response.PropertyValue is null)
                {
                    throw new InvalidDataException(
                        "The payload reports a PropertyValue, " +
                        "but the response contains none.");
                }

                offset = AddPropertyValue(
                    fields,
                    payload,
                    offset,
                    response.PropertyValue);

                break;

            default:
                throw new InvalidDataException(
                    $"Invalid PropertyValue marker " +
                    $"'{propertyValueMarker}'.");
        }

        if (offset != payload.Length)
        {
            throw new InvalidDataException(
                $"The ReadPropertyResponse payload contains " +
                $"{payload.Length} bytes, but {offset} bytes " +
                "were interpreted.");
        }

        return fields;
    }

    private static int AddProtocolResult(
        ICollection<PayloadField> fields,
        ReadOnlyMemory<byte> payload,
        int offset,
        ProtocolResult result)
    {
        ReadOnlyMemory<byte> resultCodeBytes =
            ReadBytes(
                payload,
                offset,
                1,
                "Result Code");

        byte encodedResultCode =
            resultCodeBytes.Span[0];

        fields.Add(
            new PayloadField(
                offset,
                resultCodeBytes,
                $"Result Code = {result.Code} " +
                $"(encoded value = {encodedResultCode})"));

        offset++;

        ReadOnlyMemory<byte> messageMarkerBytes =
            ReadBytes(
                payload,
                offset,
                1,
                "Result Message Marker");

        byte messageMarker =
            messageMarkerBytes.Span[0];

        fields.Add(
            new PayloadField(
                offset,
                messageMarkerBytes,
                $"Result Message Marker = " +
                $"{FormatMarker(messageMarker)}"));

        offset++;

        switch (messageMarker)
        {
            case 0:
                if (result.Message is not null)
                {
                    throw new InvalidDataException(
                        "The payload reports no result message, " +
                        "but the response contains one.");
                }

                return offset;

            case 1:
                if (result.Message is null)
                {
                    throw new InvalidDataException(
                        "The payload reports a result message, " +
                        "but the response contains none.");
                }

                return AddString(
                    fields,
                    payload,
                    offset,
                    "Result Message",
                    result.Message);

            default:
                throw new InvalidDataException(
                    $"Invalid optional result-message marker " +
                    $"'{messageMarker}'.");
        }
    }

    private static int AddPropertyValue(
        ICollection<PayloadField> fields,
        ReadOnlyMemory<byte> payload,
        int offset,
        Hase.Core.Domain.Properties.PropertyValue propertyValue)
    {
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
                $"Variant Type = " +
                $"{GetVariantTypeName(propertyValue.Value)} " +
                $"(encoded value = {variantTypeBytes.Span[0]})"));

        offset++;

        offset = AddVariantValue(
            fields,
            payload,
            offset,
            propertyValue.Value);

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
                $"(Unix milliseconds = " +
                $"{unixTimeMilliseconds})"));

        offset += sizeof(long);

        ReadOnlyMemory<byte> qualityBytes =
            ReadBytes(
                payload,
                offset,
                1,
                "Property Quality");

        fields.Add(
            new PayloadField(
                offset,
                qualityBytes,
                $"Property Quality = {propertyValue.Quality} " +
                $"(encoded value = {qualityBytes.Span[0]})"));

        return offset + 1;
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

    private static int AddString(
        ICollection<PayloadField> fields,
        ReadOnlyMemory<byte> payload,
        int offset,
        string name,
        string expectedValue)
    {
        ReadOnlyMemory<byte> lengthBytes =
            ReadBytes(
                payload,
                offset,
                sizeof(ushort),
                $"{name} Length");

        ushort encodedLength =
            BinaryPrimitives.ReadUInt16LittleEndian(
                lengthBytes.Span);

        fields.Add(
            new PayloadField(
                offset,
                lengthBytes,
                $"{name} byte length = {encodedLength}"));

        offset += sizeof(ushort);

        ReadOnlyMemory<byte> valueBytes =
            ReadBytes(
                payload,
                offset,
                encodedLength,
                name);

        string decodedValue =
            Encoding.UTF8.GetString(
                valueBytes.Span);

        if (!string.Equals(
                expectedValue,
                decodedValue,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"The payload contains {name} " +
                $"'{decodedValue}', but '{expectedValue}' " +
                "was expected.");
        }

        fields.Add(
            new PayloadField(
                offset,
                valueBytes,
                $"{name} = \"{decodedValue}\""));

        return offset + encodedLength;
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

    private static string FormatMarker(
        byte marker)
    {
        return marker switch
        {
            0 => "Null / Absent (0)",
            1 => "Value / Present (1)",
            _ => $"Invalid ({marker})"
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
                $"The ReadPropertyResponse payload ended while " +
                $"reading '{fieldName}' at offset {offset}.");
        }

        return payload.Slice(
            offset,
            length);
    }
}
