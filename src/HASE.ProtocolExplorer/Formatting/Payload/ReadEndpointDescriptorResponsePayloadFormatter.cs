using System.Buffers.Binary;
using System.Text;
using Hase.Protocol;
using Hase.ProtocolExplorer.Tracing.Model;

namespace Hase.ProtocolExplorer.Formatting.Payload;

internal sealed class
    ReadEndpointDescriptorResponsePayloadFormatter
{
    public IReadOnlyList<PayloadField> Format(
        ReadEndpointDescriptorResponse response,
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

        ReadOnlyMemory<byte> descriptorMarkerBytes =
            ReadBytes(
                payload,
                offset,
                1,
                "Descriptor Marker");

        byte descriptorMarker =
            descriptorMarkerBytes.Span[0];

        fields.Add(
            new PayloadField(
                offset,
                descriptorMarkerBytes,
                $"Descriptor Marker = " +
                $"{FormatMarker(descriptorMarker)}"));

        offset++;

        switch (descriptorMarker)
        {
            case 0:
                if (response.Descriptor is not null)
                {
                    throw new InvalidDataException(
                        "The payload reports no endpoint descriptor, " +
                        "but the response contains one.");
                }

                break;

            case 1:
                if (response.Descriptor is null)
                {
                    throw new InvalidDataException(
                        "The payload reports an endpoint descriptor, " +
                        "but the response contains none.");
                }

                offset = AddEndpointDescriptor(
                    fields,
                    payload,
                    offset,
                    response.Descriptor);

                break;

            default:
                throw new InvalidDataException(
                    $"Invalid endpoint-descriptor marker " +
                    $"'{descriptorMarker}'.");
        }

        if (offset != payload.Length)
        {
            throw new InvalidDataException(
                $"The ReadEndpointDescriptorResponse payload " +
                $"contains {payload.Length} bytes, but {offset} " +
                "bytes were interpreted.");
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

    private static int AddEndpointDescriptor(
        ICollection<PayloadField> fields,
        ReadOnlyMemory<byte> payload,
        int offset,
        Hase.Core.Domain.Endpoints.EndpointDescriptor descriptor)
    {
        offset = AddString(
            fields,
            payload,
            offset,
            "EndpointId",
            descriptor.Id.Value);

        offset = AddOptionalString(
            fields,
            payload,
            offset,
            "DisplayName",
            descriptor.Metadata.DisplayName);

        offset = AddOptionalString(
            fields,
            payload,
            offset,
            "Description",
            descriptor.Metadata.Description);

        ReadOnlyMemory<byte> instrumentCountBytes =
            ReadBytes(
                payload,
                offset,
                sizeof(ushort),
                "Instrument Count");

        ushort instrumentCount =
            BinaryPrimitives.ReadUInt16LittleEndian(
                instrumentCountBytes.Span);

        fields.Add(
            new PayloadField(
                offset,
                instrumentCountBytes,
                $"Instrument Count = {instrumentCount}"));

        offset += sizeof(ushort);

        if (instrumentCount != descriptor.Instruments.Count)
        {
            throw new InvalidDataException(
                $"The payload reports {instrumentCount} instruments, " +
                $"but the descriptor contains " +
                $"{descriptor.Instruments.Count} instruments.");
        }

        int remainingLength =
            payload.Length - offset;

        if (remainingLength > 0)
        {
            ReadOnlyMemory<byte> instrumentBytes =
                ReadBytes(
                    payload,
                    offset,
                    remainingLength,
                    "Serialized Instrument Descriptors");

            fields.Add(
                new PayloadField(
                    offset,
                    instrumentBytes,
                    $"Serialized Instrument Descriptors = " +
                    $"{instrumentCount} instrument(s)"));

            offset += remainingLength;
        }

        return offset;
    }

    private static int AddOptionalString(
        ICollection<PayloadField> fields,
        ReadOnlyMemory<byte> payload,
        int offset,
        string name,
        string? expectedValue)
    {
        ReadOnlyMemory<byte> markerBytes =
            ReadBytes(
                payload,
                offset,
                1,
                $"{name} Marker");

        byte marker =
            markerBytes.Span[0];

        fields.Add(
            new PayloadField(
                offset,
                markerBytes,
                $"{name} Marker = {FormatMarker(marker)}"));

        offset++;

        switch (marker)
        {
            case 0:
                if (expectedValue is not null)
                {
                    throw new InvalidDataException(
                        $"The payload reports no {name}, but the " +
                        "descriptor contains one.");
                }

                return offset;

            case 1:
                if (expectedValue is null)
                {
                    throw new InvalidDataException(
                        $"The payload reports a {name}, but the " +
                        "descriptor contains none.");
                }

                return AddString(
                    fields,
                    payload,
                    offset,
                    name,
                    expectedValue);

            default:
                throw new InvalidDataException(
                    $"Invalid optional-string marker '{marker}' " +
                    $"for {name}.");
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

        string decodedValue =
            Encoding.UTF8.GetString(
                valueBytes.Span);

        if (!string.Equals(
                decodedValue,
                expectedValue,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"The payload contains {name} '{decodedValue}', " +
                $"but '{expectedValue}' was expected.");
        }

        fields.Add(
            new PayloadField(
                offset,
                valueBytes,
                $"{name} = \"{decodedValue}\""));

        return offset + byteCount;
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
                $"The ReadEndpointDescriptorResponse payload ended " +
                $"while reading '{fieldName}' at offset {offset}.");
        }

        return payload.Slice(
            offset,
            length);
    }
}