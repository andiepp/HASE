using Hase.Core.Domain.Events;
using Hase.Runtime.Northbound;

namespace Hase.DesktopHost;

/// <summary>
/// Resolves the authoritative Event descriptor for one exact attachment
/// occurrence identity.
/// </summary>
public static class DesktopRuntimeEventDescriptorResolver
{
    public static EventDescriptor? Resolve(
        PublishedRuntimeHostSnapshot snapshot,
        RuntimeHostObservation observation)
    {
        ArgumentNullException.ThrowIfNull(
            snapshot);
        ArgumentNullException.ThrowIfNull(
            observation);

        if (observation.Payload
            is not RuntimeHostEventOccurredObservationPayload payload)
        {
            throw new ArgumentException(
                "An Event occurrence observation is required.",
                nameof(observation));
        }

        PublishedRuntimeEndpointSnapshot? endpoint =
            snapshot.Endpoints.SingleOrDefault(
                candidate =>
                    candidate.EndpointId
                        == observation.EndpointId
                    && candidate.Generation
                        == observation.AttachmentGeneration);
        Hase.Core.Domain.Instruments.InstrumentDescriptor? instrument =
            endpoint?.Descriptor.Instruments.SingleOrDefault(
                candidate =>
                    candidate.Id
                        == payload.InstrumentId);

        return instrument?.Interface.Events.SingleOrDefault(
            candidate =>
                candidate.Path
                    == payload.EventPath);
    }
}
