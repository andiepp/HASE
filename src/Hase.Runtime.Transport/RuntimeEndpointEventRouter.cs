using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Runtime;

namespace Hase.Runtime.Transport;

/// <summary>
/// Routes one already-resolved event identity into an existing runtime endpoint
/// graph.
/// </summary>
internal sealed class RuntimeEndpointEventRouter
{
    private readonly RuntimeEndpoint _runtimeEndpoint;

    public RuntimeEndpointEventRouter(
        RuntimeEndpoint runtimeEndpoint)
    {
        _runtimeEndpoint =
            runtimeEndpoint
            ?? throw new ArgumentNullException(
                nameof(runtimeEndpoint));
    }

    public void Publish(
        InstrumentId instrumentId,
        DescriptorPath eventPath,
        DateTimeOffset timestampUtc,
        object? value)
    {
        ArgumentNullException.ThrowIfNull(
            instrumentId);

        ArgumentNullException.ThrowIfNull(
            eventPath);

        RuntimeInstrument? runtimeInstrument =
            _runtimeEndpoint.FindInstrument(
                instrumentId);

        if (runtimeInstrument is null)
        {
            return;
        }

        RuntimeEvent? runtimeEvent =
            runtimeInstrument.FindEvent(
                eventPath);

        if (runtimeEvent is null)
        {
            return;
        }

        runtimeEvent.PublishOccurrence(
            timestampUtc,
            value);
    }
}