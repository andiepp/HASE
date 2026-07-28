namespace Hase.DesktopHost;

public sealed record DesktopRuntimeOperatorActivityEntry(
    DateTimeOffset TimestampUtc,
    DesktopRuntimeOperatorActivityKind Kind,
    string EndpointId,
    string AttachmentGeneration,
    string InstrumentId,
    string OperationPath,
    string InputSummary,
    DesktopRuntimeOperatorActivityOutcome Outcome,
    string Diagnostic,
    string Reconciliation)
{
    public string TimestampUtcText =>
        TimestampUtc.ToUniversalTime()
            .ToString("O");
}
