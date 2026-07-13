using System.Buffers.Binary;
using System.Text;
using Hase.Protocol;
using Hase.ProtocolExplorer.Tracing.Model;

namespace Hase.ProtocolExplorer.Formatting.Payload;

internal sealed class DiscoverResponsePayloadFormatter
{
    public IReadOnlyList<PayloadField> Format(
        DiscoverResponse response,
        ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(
            response);

        List<PayloadField> fields = [];

        int offset = 0;

        int endpointIdByteCount =
            Encoding.UTF8.GetByteCount(
                response.EndpointId.Value);

        ReadOnlyMemory<byte> endpointLengthBytes =
            ReadBytes(
                payload,
                offset,
                sizeof(ushort),
                "EndpointId length");

        ushort encodedEndpointLength =
            BinaryPrimitives.ReadUInt16LittleEndian(
                endpointLengthBytes.Span);

        fields.Add(
            new PayloadField(
                offset,
                endpointLengthBytes,
                $"EndpointId byte length = " +
                $"{encodedEndpointLength}"));

        offset += sizeof(ushort);

        ReadOnlyMemory<byte> endpointIdBytes =
            ReadBytes(
                payload,
                offset,
                endpointIdByteCount,
                "EndpointId");

        fields.Add(
            new PayloadField(
                offset,
                endpointIdBytes,
                $"EndpointId = " +
                $"\"{response.EndpointId.Value}\""));

        offset += endpointIdByteCount;

        ReadOnlyMemory<byte> instrumentCountBytes =
            ReadBytes(
                payload,
                offset,
                sizeof(ushort),
                "Instrument count");

        ushort encodedInstrumentCount =
            BinaryPrimitives.ReadUInt16LittleEndian(
                instrumentCountBytes.Span);

        fields.Add(
            new PayloadField(
                offset,
                instrumentCountBytes,
                $"Instrument count = " +
                $"{encodedInstrumentCount}"));

        offset += sizeof(ushort);

        for (int index = 0;
            index < response.InstrumentIds.Count;
            index++)
        {
            string instrumentId =
                response.InstrumentIds[index].Value;

            int instrumentIdByteCount =
                Encoding.UTF8.GetByteCount(
                    instrumentId);

            ReadOnlyMemory<byte> instrumentLengthBytes =
                ReadBytes(
                    payload,
                    offset,
                    sizeof(ushort),
                    $"InstrumentId[{index}] length");

            ushort encodedInstrumentLength =
                BinaryPrimitives.ReadUInt16LittleEndian(
                    instrumentLengthBytes.Span);

            fields.Add(
                new PayloadField(
                    offset,
                    instrumentLengthBytes,
                    $"InstrumentId[{index}] byte length = " +
                    $"{encodedInstrumentLength}"));

            offset += sizeof(ushort);

            ReadOnlyMemory<byte> instrumentIdBytes =
                ReadBytes(
                    payload,
                    offset,
                    instrumentIdByteCount,
                    $"InstrumentId[{index}]");

            fields.Add(
                new PayloadField(
                    offset,
                    instrumentIdBytes,
                    $"InstrumentId[{index}] = " +
                    $"\"{instrumentId}\""));

            offset += instrumentIdByteCount;
        }

        if (offset != payload.Length)
        {
            throw new InvalidDataException(
                $"The DiscoverResponse payload contains " +
                $"{payload.Length} bytes, but {offset} bytes " +
                "were interpreted.");
        }

        if (encodedInstrumentCount !=
            response.InstrumentIds.Count)
        {
            throw new InvalidDataException(
                $"The payload reports " +
                $"{encodedInstrumentCount} instruments, but the " +
                $"message contains " +
                $"{response.InstrumentIds.Count} instruments.");
        }

        return fields;
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
                $"The DiscoverResponse payload ended while reading " +
                $"'{fieldName}' at offset {offset}.");
        }

        return payload.Slice(
            offset,
            length);
    }
}