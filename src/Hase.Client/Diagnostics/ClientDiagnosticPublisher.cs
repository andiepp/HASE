namespace Hase.Client.Diagnostics;

/// <summary>
/// Assigns process-local sequence numbers and UTC timestamps before publication.
/// </summary>
public sealed class ClientDiagnosticPublisher
{
    private readonly IClientDiagnosticSink sink;
    private readonly Func<DateTimeOffset> utcNow;
    private long sequence;

    public ClientDiagnosticPublisher(IClientDiagnosticSink? sink = null)
        : this(sink ?? NullClientDiagnosticSink.Instance, () => DateTimeOffset.UtcNow)
    {
    }

    internal ClientDiagnosticPublisher(
        IClientDiagnosticSink sink,
        Func<DateTimeOffset> utcNow)
    {
        this.sink = sink ?? throw new ArgumentNullException(nameof(sink));
        this.utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
    }

    public bool IsEnabled(ClientDiagnosticLevel level)
    {
        try
        {
            return sink.IsEnabled(level);
        }
        catch
        {
            return false;
        }
    }

    public void Publish(ClientDiagnosticEvent diagnosticEvent)
    {
        ArgumentNullException.ThrowIfNull(diagnosticEvent);

        if (!IsEnabled(diagnosticEvent.Level))
        {
            return;
        }

        ClientDiagnosticRecord record =
            new(
                Interlocked.Increment(ref sequence),
                utcNow(),
                diagnosticEvent);

        try
        {
            sink.Publish(record);
        }
        catch
        {
            // Diagnostics must never affect client behavior.
        }
    }

    public void Publish(
        ClientDiagnosticLevel level,
        Func<ClientDiagnosticEvent> diagnosticEventFactory)
    {
        ArgumentNullException.ThrowIfNull(diagnosticEventFactory);

        if (!IsEnabled(level))
        {
            return;
        }

        ClientDiagnosticEvent diagnosticEvent = diagnosticEventFactory();
        ArgumentNullException.ThrowIfNull(diagnosticEvent);

        if (diagnosticEvent.Level != level)
        {
            throw new ArgumentException(
                "The diagnostic event level must match the requested level.",
                nameof(diagnosticEventFactory));
        }

        Publish(diagnosticEvent);
    }
}
