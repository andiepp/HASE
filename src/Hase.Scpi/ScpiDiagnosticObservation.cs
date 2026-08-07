using System.Collections.ObjectModel;

namespace Hase.Scpi;

public enum ScpiDiagnosticExchangeKind
{
    Query = 0,
    Command = 1
}

public enum ScpiDiagnosticDirection
{
    Transmit = 0,
    Receive = 1
}

public enum ScpiDiagnosticOutcome
{
    Succeeded = 0,
    Failed = 1,
    Canceled = 2,
    TimedOut = 3,
    Disposed = 4,
    Uncertain = 5
}

public enum ScpiDiagnosticFailureKind
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

public interface IScpiDiagnosticObserver
{
    void Observe(ScpiDiagnosticEvent diagnosticEvent);
}

public abstract class ScpiDiagnosticEvent
{
    protected ScpiDiagnosticEvent(
        Guid exchangeId,
        DateTimeOffset timestampUtc,
        ScpiDiagnosticExchangeKind exchangeKind)
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

    public ScpiDiagnosticExchangeKind ExchangeKind { get; }
}

public sealed class ScpiDiagnosticExchangeStarted : ScpiDiagnosticEvent
{
    public ScpiDiagnosticExchangeStarted(
        Guid exchangeId,
        DateTimeOffset timestampUtc,
        ScpiDiagnosticExchangeKind exchangeKind)
        : base(exchangeId, timestampUtc, exchangeKind)
    {
    }
}

public sealed class ScpiDiagnosticBytesObserved : ScpiDiagnosticEvent
{
    private readonly ReadOnlyCollection<byte> bytes;

    public ScpiDiagnosticBytesObserved(
        Guid exchangeId,
        DateTimeOffset timestampUtc,
        ScpiDiagnosticExchangeKind exchangeKind,
        ScpiDiagnosticDirection direction,
        ReadOnlySpan<byte> bytes)
        : base(exchangeId, timestampUtc, exchangeKind)
    {
        Direction = direction;
        this.bytes = Array.AsReadOnly(bytes.ToArray());
    }

    public ScpiDiagnosticDirection Direction { get; }

    public int ByteCount => bytes.Count;

    public IReadOnlyList<byte> Bytes => bytes;

    public byte[] ToArray() => bytes.ToArray();
}

public sealed class ScpiDiagnosticExchangeCompleted : ScpiDiagnosticEvent
{
    public ScpiDiagnosticExchangeCompleted(
        Guid exchangeId,
        DateTimeOffset timestampUtc,
        ScpiDiagnosticExchangeKind exchangeKind,
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

    public ScpiDiagnosticOutcome Outcome => ScpiDiagnosticOutcome.Succeeded;
}

public sealed class ScpiDiagnosticExchangeFailed : ScpiDiagnosticEvent
{
    public ScpiDiagnosticExchangeFailed(
        Guid exchangeId,
        DateTimeOffset timestampUtc,
        ScpiDiagnosticExchangeKind exchangeKind,
        TimeSpan duration,
        ScpiDiagnosticOutcome outcome,
        ScpiDiagnosticFailureKind failureKind,
        bool executionMayHaveOccurred)
        : base(exchangeId, timestampUtc, exchangeKind)
    {
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        if (outcome == ScpiDiagnosticOutcome.Succeeded)
        {
            throw new ArgumentException(
                "A failed exchange cannot have a successful outcome.",
                nameof(outcome));
        }

        if (failureKind == ScpiDiagnosticFailureKind.None)
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

    public ScpiDiagnosticOutcome Outcome { get; }

    public ScpiDiagnosticFailureKind FailureKind { get; }

    public bool ExecutionMayHaveOccurred { get; }
}
