using System.Buffers.Binary;
using System.Text;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;

namespace Hase.CompactProtocol;

/// <summary>
/// Encodes and decodes Compact Serial Protocol Version 1 bootstrap messages.
/// </summary>
internal static class CompactBootstrapCodec
{
    public const int MaximumIdentifierByteLength =
        63;

    private static readonly UTF8Encoding StrictUtf8 =
        new(
            encoderShouldEmitUTF8Identifier:
                false,
            throwOnInvalidBytes:
                true);

    public static CompactSerialFrame EncodeRequest(
        CompactBootstrapRequest request)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        return new CompactSerialFrame(
            (byte)CompactSerialMessageType.BootstrapRequest,
            request.CorrelationId,
            payload: []);
    }

    public static CompactBootstrapRequest DecodeRequest(
        CompactSerialFrame frame)
    {
        ArgumentNullException.ThrowIfNull(
            frame);

        ValidateMessageType(
            frame,
            CompactSerialMessageType.BootstrapRequest);

        if (!frame.Payload.IsEmpty)
        {
            throw new InvalidDataException(
                "A compact bootstrap request must have an empty payload.");
        }

        try
        {
            return new CompactBootstrapRequest(
                frame.CorrelationId);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new InvalidDataException(
                "The compact bootstrap request correlation identifier "
                + "is invalid.",
                exception);
        }
    }

    public static CompactSerialFrame EncodeResponse(
        CompactBootstrapResponse response)
    {
        ArgumentNullException.ThrowIfNull(
            response);

        byte[] endpointIdBytes =
            EncodeIdentifier(
                response.EndpointId.Value,
                nameof(response.EndpointId));

        byte[] descriptorIdBytes =
            EncodeIdentifier(
                response.DescriptorReference.Id.Value,
                nameof(response.DescriptorReference));

        var payload =
            new byte[
                1
                + endpointIdBytes.Length
                + 1
                + descriptorIdBytes.Length
                + sizeof(ushort)];

        int offset =
            0;

        payload[offset++] =
            checked((byte)endpointIdBytes.Length);

        endpointIdBytes.CopyTo(
            payload,
            offset);

        offset +=
            endpointIdBytes.Length;

        payload[offset++] =
            checked((byte)descriptorIdBytes.Length);

        descriptorIdBytes.CopyTo(
            payload,
            offset);

        offset +=
            descriptorIdBytes.Length;

        BinaryPrimitives.WriteUInt16BigEndian(
            payload.AsSpan(
                offset,
                sizeof(ushort)),
            response.DescriptorReference.Version);

        return new CompactSerialFrame(
            (byte)CompactSerialMessageType.BootstrapResponse,
            response.CorrelationId,
            payload);
    }

    public static CompactBootstrapResponse DecodeResponse(
        CompactSerialFrame frame)
    {
        ArgumentNullException.ThrowIfNull(
            frame);

        ValidateMessageType(
            frame,
            CompactSerialMessageType.BootstrapResponse);

        if (frame.CorrelationId == 0)
        {
            throw new InvalidDataException(
                "The compact bootstrap response correlation identifier "
                + "must be nonzero.");
        }

        ReadOnlySpan<byte> payload =
            frame.Payload.Span;

        int offset =
            0;

        string endpointIdValue =
            DecodeIdentifier(
                payload,
                ref offset,
                "endpoint identity");

        string descriptorIdValue =
            DecodeIdentifier(
                payload,
                ref offset,
                "descriptor identity");

        if (payload.Length - offset
            != sizeof(ushort))
        {
            throw new InvalidDataException(
                "The compact bootstrap response must end with exactly "
                + "one two-byte descriptor version.");
        }

        ushort descriptorVersion =
            BinaryPrimitives.ReadUInt16BigEndian(
                payload[offset..]);

        if (descriptorVersion == 0)
        {
            throw new InvalidDataException(
                "The compact bootstrap descriptor version must be "
                + "greater than zero.");
        }

        try
        {
            return new CompactBootstrapResponse(
                frame.CorrelationId,
                new EndpointId(
                    endpointIdValue),
                new DescriptorReference(
                    new DescriptorId(
                        descriptorIdValue),
                    descriptorVersion));
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException(
                "The compact bootstrap response contains invalid "
                + "identity data.",
                exception);
        }
    }

    private static byte[] EncodeIdentifier(
        string value,
        string parameterName)
    {
        byte[] bytes;

        try
        {
            bytes =
                StrictUtf8.GetBytes(
                    value);
        }
        catch (EncoderFallbackException exception)
        {
            throw new ArgumentException(
                "The compact identifier must contain valid UTF-8 text.",
                parameterName,
                exception);
        }

        if (bytes.Length is < 1 or > MaximumIdentifierByteLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                bytes.Length,
                $"A compact identifier must contain between 1 and "
                + $"{MaximumIdentifierByteLength} UTF-8 bytes.");
        }

        return bytes;
    }

    private static string DecodeIdentifier(
        ReadOnlySpan<byte> payload,
        ref int offset,
        string fieldName)
    {
        if (offset >= payload.Length)
        {
            throw new InvalidDataException(
                $"The compact bootstrap {fieldName} length is missing.");
        }

        int byteLength =
            payload[offset++];

        if (byteLength is < 1 or > MaximumIdentifierByteLength)
        {
            throw new InvalidDataException(
                $"The compact bootstrap {fieldName} length must be "
                + $"between 1 and {MaximumIdentifierByteLength} bytes.");
        }

        if (payload.Length - offset
            < byteLength)
        {
            throw new InvalidDataException(
                $"The compact bootstrap {fieldName} is truncated.");
        }

        try
        {
            string value =
                StrictUtf8.GetString(
                    payload.Slice(
                        offset,
                        byteLength));

            offset +=
                byteLength;

            return value;
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException(
                $"The compact bootstrap {fieldName} is not valid UTF-8.",
                exception);
        }
    }

    private static void ValidateMessageType(
        CompactSerialFrame frame,
        CompactSerialMessageType expected)
    {
        if (frame.MessageType
            != (byte)expected)
        {
            throw new InvalidDataException(
                $"Compact serial message type 0x{frame.MessageType:X2} "
                + $"is not {expected}.");
        }
    }
}