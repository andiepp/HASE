using System.Buffers.Binary;
using System.Text;
using Hase.Protocol;
using Hase.ProtocolExplorer.Tracing.Model;

namespace Hase.ProtocolExplorer.Formatting.Payload;

internal sealed class ReadPropertyPayloadFormatter
{
    public IReadOnlyList<PayloadField> Format(
        ReadPropertyRequest request,
        ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        int instrumentIdByteCount =
            Encoding.UTF8.GetByteCount(
                request.InstrumentId.Value);

        int propertyIdByteCount =
            Encoding.UTF8.GetByteCount(
                request.PropertyId.Value);

        int expectedLength =
            sizeof(ushort) +
            instrumentIdByteCount +
            sizeof(ushort) +
            propertyIdByteCount;

        if (payload.Length != expectedLength)
        {
            throw new InvalidDataException(
                $"The ReadPropertyRequest payload contains " +
                $"{payload.Length} bytes, but {expectedLength} " +
                "bytes were expected.");
        }

        List<PayloadField> fields = [];

        int offset = 0;

        ReadOnlyMemory<byte> instrumentLengthBytes =
            payload.Slice(
                offset,
                sizeof(ushort));

        ushort encodedInstrumentLength =
            BinaryPrimitives.ReadUInt16LittleEndian(
                instrumentLengthBytes.Span);

        fields.Add(
            new PayloadField(
                offset,
                instrumentLengthBytes,
                $"InstrumentId byte length = " +
                $"{encodedInstrumentLength}"));

        offset += sizeof(ushort);

        ReadOnlyMemory<byte> instrumentIdBytes =
            payload.Slice(
                offset,
                instrumentIdByteCount);

        fields.Add(
            new PayloadField(
                offset,
                instrumentIdBytes,
                $"InstrumentId = " +
                $"\"{request.InstrumentId.Value}\""));

        offset += instrumentIdByteCount;

        ReadOnlyMemory<byte> propertyLengthBytes =
            payload.Slice(
                offset,
                sizeof(ushort));

        ushort encodedPropertyLength =
            BinaryPrimitives.ReadUInt16LittleEndian(
                propertyLengthBytes.Span);

        fields.Add(
            new PayloadField(
                offset,
                propertyLengthBytes,
                $"PropertyId byte length = " +
                $"{encodedPropertyLength}"));

        offset += sizeof(ushort);

        ReadOnlyMemory<byte> propertyIdBytes =
            payload.Slice(
                offset,
                propertyIdByteCount);

        fields.Add(
            new PayloadField(
                offset,
                propertyIdBytes,
                $"PropertyId = " +
                $"\"{request.PropertyId.Value}\""));

        return fields;
    }
}