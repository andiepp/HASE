using System.Globalization;
using Hase.Runtime.Northbound;

namespace Hase.DesktopHost;

public static class DesktopRuntimeEventOccurrenceProjector
{
    public static DesktopRuntimeEventOccurrence Project(
        RuntimeHostObservation observation)
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

        return new DesktopRuntimeEventOccurrence(
            payload.OccurredAtUtc,
            observation.EndpointId.Value,
            observation.AttachmentGeneration.ToString(),
            payload.InstrumentId.Value,
            payload.EventPath.ToString(),
            FormatValue(
                payload.Value));
    }

    private static string FormatValue(
        object? value)
    {
        if (value is null)
        {
            return "null";
        }

        return value is IFormattable formattable
            ? formattable.ToString(
                format: null,
                CultureInfo.InvariantCulture)
                ?? string.Empty
            : value.ToString()
                ?? string.Empty;
    }
}
