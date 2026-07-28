namespace Hase.DesktopHost;

public sealed record DesktopRuntimeEventOccurrence(
    DateTimeOffset OccurredAtUtc,
    string EndpointId,
    string AttachmentGeneration,
    string InstrumentId,
    string EventPath,
    string Value)
{
    public string OccurredAtUtcText =>
        OccurredAtUtc.ToUniversalTime()
            .ToString("O");
}
