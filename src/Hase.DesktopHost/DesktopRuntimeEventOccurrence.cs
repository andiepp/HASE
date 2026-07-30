using Hase.Operator.Presentation;

namespace Hase.DesktopHost;

public sealed record DesktopRuntimeEventOccurrence(
    DateTimeOffset OccurredAtUtc,
    string EndpointId,
    string AttachmentGeneration,
    string InstrumentId,
    string EventPath,
    string EventDisplayName,
    string EventDescription,
    string PayloadDisplayName,
    string PayloadDescription,
    string PayloadText,
    EventPayloadFormatStatus PayloadStatus)
{
    public string OccurredAtUtcText =>
        OccurredAtUtc.ToUniversalTime()
            .ToString("O");

    public string PayloadDiagnostic =>
        PayloadStatus is EventPayloadFormatStatus.Formatted
            or EventPayloadFormatStatus.NoPayload
                ? string.Empty
                : PayloadStatus.ToString();
}
