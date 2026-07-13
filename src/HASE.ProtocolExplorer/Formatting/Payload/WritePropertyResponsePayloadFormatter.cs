using Hase.Protocol;

namespace Hase.ProtocolExplorer.Formatting.Payload;

/// <summary>
/// The payload layout of WritePropertyResponse is identical to
/// ReadPropertyResponse.
///
/// Reuse the existing formatter to guarantee both response messages
/// are interpreted identically.
/// </summary>
internal sealed class WritePropertyResponsePayloadFormatter
{
    private readonly ReadPropertyResponsePayloadFormatter
        _formatter =
        new();

    public IReadOnlyList<Tracing.Model.PayloadField> Format(
        WritePropertyResponse response,
        ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(
            response);

        ReadPropertyResponse equivalent =
            new(
                response.CorrelationId,
                response.Result,
                response.PropertyValue);

        return _formatter.Format(
            equivalent,
            payload);
    }
}