namespace Hase.Mcnf;

internal sealed class McnfDiagnosticExchange
{
    private readonly IMcnfDiagnosticObserver observer;
    private readonly TimeProvider timeProvider;
    private readonly long startedTimestamp;

    private McnfDiagnosticExchange(
        IMcnfDiagnosticObserver observer,
        TimeProvider timeProvider,
        McnfDiagnosticExchangeKind exchangeKind)
    {
        this.observer = observer;
        this.timeProvider = timeProvider;
        ExchangeId = Guid.NewGuid();
        ExchangeKind = exchangeKind;
        startedTimestamp = timeProvider.GetTimestamp();

        Publish(new McnfDiagnosticExchangeStarted(
            ExchangeId,
            timeProvider.GetUtcNow(),
            ExchangeKind));
    }

    public Guid ExchangeId { get; }

    public McnfDiagnosticExchangeKind ExchangeKind { get; }

    public static McnfDiagnosticExchange? Start(
        IMcnfDiagnosticObserver? observer,
        TimeProvider timeProvider,
        McnfDiagnosticExchangeKind exchangeKind) =>
        observer is null
            ? null
            : new McnfDiagnosticExchange(observer, timeProvider, exchangeKind);

    public void ObserveBytes(
        McnfDiagnosticDirection direction,
        ReadOnlySpan<byte> bytes) =>
        Publish(new McnfDiagnosticBytesObserved(
            ExchangeId,
            timeProvider.GetUtcNow(),
            ExchangeKind,
            direction,
            bytes));

    public void Complete() =>
        Publish(new McnfDiagnosticExchangeCompleted(
            ExchangeId,
            timeProvider.GetUtcNow(),
            ExchangeKind,
            GetDuration()));

    public void Fail(
        McnfDiagnosticOutcome outcome,
        Exception exception,
        bool executionMayHaveOccurred = false)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Publish(new McnfDiagnosticExchangeFailed(
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

    private void Publish(McnfDiagnosticEvent diagnosticEvent)
    {
        try
        {
            observer.Observe(diagnosticEvent);
        }
        catch
        {
            // Diagnostic observers must never affect MCNF behavior.
        }
    }

    private static McnfDiagnosticFailureKind Classify(Exception exception) =>
        exception switch
        {
            OperationCanceledException => McnfDiagnosticFailureKind.Cancellation,
            TimeoutException => McnfDiagnosticFailureKind.Timeout,
            ObjectDisposedException => McnfDiagnosticFailureKind.Disposal,
            EndOfStreamException => McnfDiagnosticFailureKind.EndOfStream,
            InvalidDataException => McnfDiagnosticFailureKind.InvalidData,
            McnfExchangeException => McnfDiagnosticFailureKind.Transport,
            IOException => McnfDiagnosticFailureKind.InputOutput,
            _ => McnfDiagnosticFailureKind.Unknown
        };
}
