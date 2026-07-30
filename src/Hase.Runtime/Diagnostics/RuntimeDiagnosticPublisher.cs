namespace Hase.Runtime.Diagnostics;

/// <summary>
/// Assigns process-local sequence numbers and UTC timestamps before publication.
/// </summary>
public sealed class RuntimeDiagnosticPublisher
{
    private readonly IRuntimeDiagnosticSink sink;
    private readonly Func<DateTimeOffset> utcNow;
    private long sequence;

    public RuntimeDiagnosticPublisher(
        IRuntimeDiagnosticSink? sink = null)
        : this(
            sink ?? NullRuntimeDiagnosticSink.Instance,
            () => DateTimeOffset.UtcNow)
    {
    }

    internal RuntimeDiagnosticPublisher(
        IRuntimeDiagnosticSink sink,
        Func<DateTimeOffset> utcNow)
    {
        this.sink =
            sink ??
            throw new ArgumentNullException(
                nameof(sink));

        this.utcNow =
            utcNow ??
            throw new ArgumentNullException(
                nameof(utcNow));
    }

    public bool IsEnabled(
        RuntimeDiagnosticLevel level)
    {
        try
        {
            return sink.IsEnabled(
                level);
        }
        catch
        {
            return false;
        }
    }

    public void Publish(
        RuntimeDiagnosticEvent diagnosticEvent)
    {
        ArgumentNullException.ThrowIfNull(
            diagnosticEvent);

        if (!IsEnabled(
                diagnosticEvent.Level))
        {
            return;
        }

        RuntimeDiagnosticRecord record =
            new(
                Interlocked.Increment(
                    ref sequence),
                utcNow(),
                diagnosticEvent);

        try
        {
            sink.Publish(
                record);
        }
        catch
        {
            // Diagnostic observers must never affect runtime behavior.
        }
    }

    public void Publish(
        RuntimeDiagnosticLevel level,
        Func<RuntimeDiagnosticEvent> diagnosticEventFactory)
    {
        ArgumentNullException.ThrowIfNull(
            diagnosticEventFactory);

        if (!IsEnabled(
                level))
        {
            return;
        }

        RuntimeDiagnosticEvent diagnosticEvent =
            diagnosticEventFactory();

        ArgumentNullException.ThrowIfNull(
            diagnosticEvent);

        if (diagnosticEvent.Level != level)
        {
            throw new ArgumentException(
                "The diagnostic event level must match the requested level.",
                nameof(diagnosticEventFactory));
        }

        Publish(
            diagnosticEvent);
    }
}
