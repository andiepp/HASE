using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using Hase.Protocol;
using Hase.ProtocolExplorer.Tracing.Model;

namespace Hase.ProtocolExplorer.Formatting.Payload;

internal sealed class WritePropertyRequestPayloadFormatter
{
    public IReadOnlyList<PayloadField> Format(
        WritePropertyRequest request,
        ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        List<PayloadField> fields = [];

        int offset = 0;

        offset = AddString(
            fields,
            payload,
            offset,
            "InstrumentId",
            request.InstrumentId.Value);

        offset = AddString(
            fields,
            payload,
            offset,
            "PropertyId",
            request.PropertyId.Value);

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
                $"Variant Type = {GetVariantTypeName(request.Value)} " +
                $"(encoded value = {variantTypeBytes.Span[0]})"));

        offset++;

        offset = AddVariantValue(
            fields,
            payload,
            offset,
            request.Value);

        if (offset != payload.Length)
        {
            throw new InvalidDataException(
                $"The WritePropertyRequest payload contains " +
                $"{payload.Length} bytes, but {offset} bytes " +
                "were interpreted.");
        }

        return fields;
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

                    ushort stringByteCount =
                        BinaryPrimitives.ReadUInt16LittleEndian(
                            lengthBytes.Span);

                    fields.Add(
                        new PayloadField(
                            offset,
                            lengthBytes,
                            $"String byte length = {stringByteCount}"));

                    offset += sizeof(ushort);

                    ReadOnlyMemory<byte> valueBytes =
                        ReadBytes(
                            payload,
                            offset,
                            stringByteCount,
                            "String Value");

                    string stringValue =
                        Encoding.UTF8.GetString(
                            valueBytes.Span);

                    fields.Add(
                        new PayloadField(
                            offset,
                            valueBytes,
                            $"String Value = \"{stringValue}\""));

                    return offset + stringByteCount;
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

    private static int AddString(
        ICollection<PayloadField> fields,
        ReadOnlyMemory<byte> payload,
        int offset,
        string name,
        string value)
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

        fields.Add(
            new PayloadField(
                offset,
                valueBytes,
                $"{name} = \"{decodedValue}\""));

        return offset + encodedLength;
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
                $"The WritePropertyRequest payload ended while " +
                $"reading '{fieldName}' at offset {offset}.");
        }

        return payload.Slice(
            offset,
            length);
    }
}