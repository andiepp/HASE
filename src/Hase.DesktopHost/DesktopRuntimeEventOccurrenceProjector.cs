using Hase.Core.Domain.Events;
using Hase.Operator.Presentation;
using Hase.Runtime.Northbound;

namespace Hase.DesktopHost;

public static class DesktopRuntimeEventOccurrenceProjector
{
    public static DesktopRuntimeEventOccurrence Project(
        RuntimeHostObservation observation,
        EventDescriptor? descriptor)
    {
        ArgumentNullException.ThrowIfNull(
            observation);

        if (observation.Payload
            is not RuntimeHostEventOccurredObservationPayload payload)
        {
            throw new ArgumentException(
                "An Event occurrence observation is required.",
                nameof(observation));
        }

        EventPayloadFormatResult payloadPresentation =
            EventPayloadFormatter.Format(
                descriptor?.Payload,
                payload.Value);

        return new DesktopRuntimeEventOccurrence(
            payload.OccurredAtUtc,
            observation.EndpointId.Value,
            observation.AttachmentGeneration.ToString(),
            payload.InstrumentId.Value,
            payload.EventPath.ToString(),
            descriptor?.DisplayName
                ?? payload.EventPath.ToString(),
            descriptor?.Description
                ?? string.Empty,
            descriptor?.Payload?.DisplayName
                ?? "Payload",
            descriptor?.Payload?.Description
                ?? string.Empty,
            payloadPresentation.Text,
            payloadPresentation.Status);
    }
}
