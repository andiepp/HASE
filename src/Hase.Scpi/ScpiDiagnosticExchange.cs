namespace Hase.Scpi;

internal sealed class ScpiDiagnosticExchange
{
    private readonly IScpiDiagnosticObserver observer;
    private readonly TimeProvider timeProvider;
    private readonly long startedTimestamp;

    private ScpiDiagnosticExchange(
        IScpiDiagnosticObserver observer,
        TimeProvider timeProvider,
        ScpiDiagnosticExchangeKind exchangeKind)
    {
        this.observer = observer;
        this.timeProvider = timeProvider;
        ExchangeId = Guid.NewGuid();
        ExchangeKind = exchangeKind;
        startedTimestamp = timeProvider.GetTimestamp();

        Publish(new ScpiDiagnosticExchangeStarted(
            ExchangeId,
            timeProvider.GetUtcNow(),
            ExchangeKind));
    }

    public Guid ExchangeId { get; }

    public ScpiDiagnosticExchangeKind ExchangeKind { get; }

    public static ScpiDiagnosticExchange? Start(
        IScpiDiagnosticObserver? observer,
        TimeProvider timeProvider,
        ScpiDiagnosticExchangeKind exchangeKind) =>
        observer is null
            ? null
            : new ScpiDiagnosticExchange(observer, timeProvider, exchangeKind);

    public void ObserveBytes(
        ScpiDiagnosticDirection direction,
        ReadOnlySpan<byte> bytes) =>
        Publish(new ScpiDiagnosticBytesObserved(
            ExchangeId,
            timeProvider.GetUtcNow(),
            ExchangeKind,
            direction,
            bytes));

    public void Complete() =>
        Publish(new ScpiDiagnosticExchangeCompleted(
            ExchangeId,
            timeProvider.GetUtcNow(),
            ExchangeKind,
            GetDuration()));

    public void Fail(
        ScpiDiagnosticOutcome outcome,
        Exception exception,
        bool executionMayHaveOccurred = false)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Publish(new ScpiDiagnosticExchangeFailed(
            ExchangeId,
            timeProvider.GetUtcNow(),
            ExchangeKind,
            GetDuration(),
            outcome,
            Classify(exception),
            executionMayHaveOccurred));
    }

    private TimeSpan GetDuration() =>
        timeProvider.GetElapsedTime(startedTimestamp, timeProvider.GetTimestamp());

    private void Publish(ScpiDiagnosticEvent diagnosticEvent)
    {
        try
        {
            observer.Observe(diagnosticEvent);
        }
        catch
        {
            // Diagnostic observers must never affect SCPI behavior.
        }
    }

    private static ScpiDiagnosticFailureKind Classify(Exception exception) =>
        exception switch
        {
            OperationCanceledException => ScpiDiagnosticFailureKind.Cancellation,
            TimeoutException => ScpiDiagnosticFailureKind.Timeout,
            ObjectDisposedException => ScpiDiagnosticFailureKind.Disposal,
            EndOfStreamException => ScpiDiagnosticFailureKind.EndOfStream,
            InvalidDataException => ScpiDiagnosticFailureKind.InvalidData,
            ScpiCommandTransmissionException => ScpiDiagnosticFailureKind.Transport,
            IOException => ScpiDiagnosticFailureKind.InputOutput,
            _ => ScpiDiagnosticFailureKind.Unknown
        };
}
