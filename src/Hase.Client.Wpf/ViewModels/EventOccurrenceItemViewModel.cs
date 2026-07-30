using Hase.Operator.Presentation;

namespace Hase.Client.Wpf.ViewModels;

public sealed record EventOccurrenceItemViewModel(
    ulong Sequence,
    string EndpointId,
    string AttachmentGeneration,
    string InstrumentId,
    string EventPath,
    string DisplayName,
    string OccurredAtUtc,
    string PayloadDisplayName,
    string PayloadDescription,
    string PayloadText,
    EventPayloadFormatStatus PayloadStatus)
{
    public bool HasPayloadDescription =>
        !string.IsNullOrWhiteSpace(
            PayloadDescription);

    public bool HasPayloadDiagnostic =>
        PayloadStatus is not EventPayloadFormatStatus.Formatted
            and not EventPayloadFormatStatus.NoPayload;
}
