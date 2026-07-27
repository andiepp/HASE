namespace Hase.Client.Wpf.ViewModels;

public sealed record EventOccurrenceItemViewModel(
    ulong Sequence,
    string EndpointId,
    string AttachmentGeneration,
    string InstrumentId,
    string EventPath,
    string DisplayName,
    string OccurredAtUtc,
    string Value);
