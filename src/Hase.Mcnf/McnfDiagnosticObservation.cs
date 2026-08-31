using System.Collections.ObjectModel;

namespace Hase.Mcnf;

public enum McnfDiagnosticExchangeKind
{
    ConnectivityTest = 0,
    Exchange = 1
}

public enum McnfDiagnosticDirection
{
    Transmit = 0,
    Receive = 1
}

public enum McnfDiagnosticOutcome
{
    Succeeded = 0,
    Failed = 1,
    Canceled = 2,
    TimedOut = 3,
    Disposed = 4,
    Uncertain = 5
}

public enum McnfDiagnosticFailureKind
{
    None = 0,
    Cancellation = 1,
    Timeout = 2,
    Disposal = 3,
    EndOfStream = 4,
    InvalidData = 5,
    InputOutput = 6,
    Transport = 7,
    Unknown = 8
}

public interface IMcnfDiagnosticObserver
{
    void Observe(McnfDiagnosticEvent diagnosticEvent);
}

public abstract class McnfDiagnosticEvent
{
    protected McnfDiagnosticEvent(
        Guid exchangeId,
        DateTimeOffset timestampUtc,
        McnfDiagnosticExchangeKind exchangeKind)
    {
        if (exchangeId == Guid.Empty)
        {
            throw new ArgumentException(
                "The diagnostic exchange identifier must not be empty.",
                nameof(exchangeId));
        }

        ExchangeId = exchangeId;
        TimestampUtc = timestampUtc.ToUniversalTime();
        ExchangeKind = exchangeKind;
    }

    public Guid ExchangeId { get; }

    public DateTimeOffset TimestampUtc { get; }

    public McnfDiagnosticExchangeKind ExchangeKind { get; }
}

public sealed class McnfDiagnosticExchangeStarted : McnfDiagnosticEvent
{
    public McnfDiagnosticExchangeStarted(
        Guid exchangeId,
        DateTimeOffset timestampUtc,
        McnfDiagnosticExchangeKind exchangeKind)
        : base(exchangeId, timestampUtc, exchangeKind)
    {
    }
}

public sealed class McnfDiagnosticBytesObserved : McnfDiagnosticEvent
{
    private readonly ReadOnlyCollection<byte> bytes;

    public McnfDiagnosticBytesObserved(
        Guid exchangeId,
        DateTimeOffset timestampUtc,
        McnfDiagnosticExchangeKind exchangeKind,
        McnfDiagnosticDirection direction,
        ReadOnlySpan<byte> bytes)
        : base(exchangeId, timestampUtc, exchangeKind)
    {
        Direction = direction;
        this.bytes = Array.AsReadOnly(bytes.ToArray());
    }

    public McnfDiagnosticDirection Direction { get; }

    public int ByteCount => bytes.Count;

    public IReadOnlyList<byte> Bytes => bytes;

    public byte[] ToArray() => bytes.ToArray();
}

public sealed class McnfDiagnosticExchangeCompleted : McnfDiagnosticEvent
{
    public McnfDiagnosticExchangeCompleted(
        Guid exchangeId,
        DateTimeOffset timestampUtc,
        McnfDiagnosticExchangeKind exchangeKind,
        TimeSpan duration)
        : base(exchangeId, timestampUtc, exchangeKind)
    {
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        Duration = duration;
    }

    public TimeSpan Duration { get; }

    public McnfDiagnosticOutcome Outcome => McnfDiagnosticOutcome.Succeeded;
}

public sealed class McnfDiagnosticExchangeFailed : McnfDiagnosticEvent
{
    public McnfDiagnosticExchangeFailed(
        Guid exchangeId,
        DateTimeOffset timestampUtc,
        McnfDiagnosticExchangeKind exchangeKind,
        TimeSpan duration,
        McnfDiagnosticOutcome outcome,
        McnfDiagnosticFailureKind failureKind,
        bool executionMayHaveOccurred)
        : base(exchangeId, timestampUtc, exchangeKind)
    {
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        if (outcome == McnfDiagnosticOutcome.Succeeded)
        {
            throw new ArgumentException(
                "A failed exchange cannot have a successful outcome.",
                nameof(outcome));
        }

        if (failureKind == McnfDiagnosticFailureKind.None)
        {
            throw new ArgumentException(
                "A failed exchange requires a failure classification.",
                nameof(failureKind));
        }

        Duration = duration;
        Outcome = outcome;
        FailureKind = failureKind;
        ExecutionMayHaveOccurred = executionMayHaveOccurred;
    }

    public TimeSpan Duration { get; }

    public McnfDiagnosticOutcome Outcome { get; }

    public McnfDiagnosticFailureKind FailureKind { get; }

    public bool ExecutionMayHaveOccurred { get; }
}
