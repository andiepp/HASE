using System.Globalization;
using Hase.Runtime.Northbound;

namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Formats normalized northbound observations for physical Protocol Explorer
/// validation without interpreting transport-specific details.
/// </summary>
internal static class RuntimeHostObservationFormatter
{
    public static string Format(
        RuntimeHostObservation observation)
    {
        ArgumentNullException.ThrowIfNull(
            observation);

        var lines =
            new List<string>
            {
                $"Sequence              : {observation.Sequence}",
                $"Kind                  : {observation.Kind}",
                $"Endpoint              : {observation.EndpointId.Value}",
                $"Attachment generation : {observation.AttachmentGeneration}"
            };

        AppendPayload(
            lines,
            observation.Payload);

        return string.Join(
            Environment.NewLine,
            lines);
    }

    private static void AppendPayload(
        ICollection<string> lines,
        RuntimeHostObservationPayload payload)
    {
        switch (payload)
        {
            case RuntimeHostAttachmentPublishedObservationPayload published:
                lines.Add(
                    $"Connection state      : "
                    + $"{published.Endpoint.ConnectionStatus.State}");
                break;

            case RuntimeHostAttachmentEndedObservationPayload ended:
                lines.Add(
                    $"Ended at UTC          : {ended.EndedAtUtc:O}");
                break;

            case RuntimeHostConnectionStatusChangedObservationPayload status:
                lines.Add(
                    $"Connection transition : "
                    + $"{status.PreviousStatus.State} -> "
                    + $"{status.CurrentStatus.State}");

                if (!string.IsNullOrWhiteSpace(
                        status.CurrentStatus.Detail))
                {
                    lines.Add(
                        $"Connection detail     : "
                        + $"{status.CurrentStatus.Detail}");
                }

                break;

            case RuntimeHostPropertyValueChangedObservationPayload property:
                lines.Add(
                    $"Instrument            : {property.InstrumentId.Value}");

                lines.Add(
                    $"Property              : {property.PropertyId.Value}");

                lines.Add(
                    $"Previous value        : "
                    + $"{FormatValue(property.PreviousValue?.Value)}");

                lines.Add(
                    $"Current value         : "
                    + $"{FormatValue(property.CurrentValue.Value)}");
                break;

            case RuntimeHostEventOccurredObservationPayload runtimeEvent:
                lines.Add(
                    $"Instrument            : {runtimeEvent.InstrumentId.Value}");

                lines.Add(
                    $"Event                 : {runtimeEvent.EventPath}");

                lines.Add(
                    $"Occurred at UTC       : {runtimeEvent.OccurredAtUtc:O}");

                lines.Add(
                    $"Value                 : {FormatValue(runtimeEvent.Value)}");
                break;

            default:
                throw new InvalidDataException(
                    $"Unsupported runtime-host observation payload "
                    + $"'{payload.GetType().FullName}'.");
        }
    }

    private static string FormatValue(
        object? value)
    {
        return value switch
        {
            null =>
                "<null>",

            IFormattable formattable =>
                formattable.ToString(
                    null,
                    CultureInfo.InvariantCulture),

            _ =>
                value.ToString()
                ?? "<null>"
        };
    }
}