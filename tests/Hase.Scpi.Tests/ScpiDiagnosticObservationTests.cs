using System.Text;

namespace Hase.Scpi.Tests;

public sealed class ScpiDiagnosticObservationTests
{
    [Fact]
    public async Task QueryAsync_ObservesExactRequestResponseChunksAndCompletion()
    {
        var observer = new RecordingObserver();
        var stream = ScriptedScpiByteStream.FromResponseChunks(
            "12"u8.ToArray(),
            "3\n"u8.ToArray());
        await using var session = CreateSession(stream, observer);

        Assert.Equal("123", await session.QueryAsync("MEAS?"));

        Assert.Collection(
            observer.Events,
            started =>
            {
                var value = Assert.IsType<ScpiDiagnosticExchangeStarted>(started);
                Assert.Equal(ScpiDiagnosticExchangeKind.Query, value.ExchangeKind);
            },
            transmitted =>
            {
                var value = Assert.IsType<ScpiDiagnosticBytesObserved>(transmitted);
                Assert.Equal(ScpiDiagnosticDirection.Transmit, value.Direction);
                Assert.Equal("MEAS?\r", Encoding.ASCII.GetString(value.ToArray()));
            },
            received =>
            {
                var value = Assert.IsType<ScpiDiagnosticBytesObserved>(received);
                Assert.Equal(ScpiDiagnosticDirection.Receive, value.Direction);
                Assert.Equal("12", Encoding.ASCII.GetString(value.ToArray()));
            },
            received =>
            {
                var value = Assert.IsType<ScpiDiagnosticBytesObserved>(received);
                Assert.Equal(ScpiDiagnosticDirection.Receive, value.Direction);
                Assert.Equal("3\n", Encoding.ASCII.GetString(value.ToArray()));
            },
            completed =>
            {
                var value = Assert.IsType<ScpiDiagnosticExchangeCompleted>(completed);
                Assert.Equal(ScpiDiagnosticOutcome.Succeeded, value.Outcome);
                Assert.True(value.Duration >= TimeSpan.Zero);
            });
        Assert.Single(observer.Events.Select(value => value.ExchangeId).Distinct());
        Assert.All(observer.Events, value => Assert.Equal(TimeSpan.Zero, value.TimestampUtc.Offset));
    }

    [Fact]
    public async Task SendCommandAsync_ObservesExactCommandFrameWithoutReceiveBytes()
    {
        var observer = new RecordingObserver();
        var stream = new ScriptedScpiByteStream();
        await using var session = CreateSession(stream, observer);

        await session.SendCommandAsync("OUTPUT ON");

        Assert.Collection(
            observer.Events,
            started => Assert.IsType<ScpiDiagnosticExchangeStarted>(started),
            transmitted =>
            {
                var value = Assert.IsType<ScpiDiagnosticBytesObserved>(transmitted);
                Assert.Equal(ScpiDiagnosticExchangeKind.Command, value.ExchangeKind);
                Assert.Equal(ScpiDiagnosticDirection.Transmit, value.Direction);
                Assert.Equal("OUTPUT ON\r", Encoding.ASCII.GetString(value.ToArray()));
            },
            completed => Assert.IsType<ScpiDiagnosticExchangeCompleted>(completed));
    }

    [Fact]
    public async Task QueryAsync_TimeoutObservesSanitizedTimedOutTerminalEvent()
    {
        var observer = new RecordingObserver();
        var neverCompletes = new TaskCompletionSource<int>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var stream = new ScriptedScpiByteStream(
            read: (_, _) => new ValueTask<int>(neverCompletes.Task));
        await using var session = CreateSession(
            stream,
            observer,
            timeout: TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAsync<TimeoutException>(() => session.QueryAsync("READ?"));

        ScpiDiagnosticExchangeFailed failure =
            Assert.IsType<ScpiDiagnosticExchangeFailed>(observer.Events[^1]);
        Assert.Equal(ScpiDiagnosticOutcome.TimedOut, failure.Outcome);
        Assert.Equal(ScpiDiagnosticFailureKind.Timeout, failure.FailureKind);
        Assert.False(failure.ExecutionMayHaveOccurred);
        Assert.DoesNotContain(
            observer.Events,
            value => value.GetType().GetProperties().Any(
                property => property.Name.Contains("Message", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task QueryAsync_CancellationObservesCanceledTerminalEvent()
    {
        var observer = new RecordingObserver();
        var readStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var stream = new ScriptedScpiByteStream(
            read: async (_, cancellationToken) =>
            {
                readStarted.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return 0;
            });
        await using var session = CreateSession(
            stream,
            observer,
            timeout: TimeSpan.FromSeconds(5));
        using var cancellation = new CancellationTokenSource();

        Task<string> query = session.QueryAsync("READ?", cancellation.Token);
        await readStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => query);
        ScpiDiagnosticExchangeFailed failure =
            Assert.IsType<ScpiDiagnosticExchangeFailed>(observer.Events[^1]);
        Assert.Equal(ScpiDiagnosticOutcome.Canceled, failure.Outcome);
        Assert.Equal(ScpiDiagnosticFailureKind.Cancellation, failure.FailureKind);
    }

    [Fact]
    public async Task QueryAsync_DisposalObservesDisposedTerminalEvent()
    {
        var observer = new RecordingObserver();
        var readStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var stream = new ScriptedScpiByteStream(
            read: async (_, cancellationToken) =>
            {
                readStarted.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return 0;
            });
        var session = CreateSession(
            stream,
            observer,
            timeout: TimeSpan.FromSeconds(5));

        Task<string> query = session.QueryAsync("READ?");
        await readStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        ValueTask disposal = session.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => query);
        await disposal;
        ScpiDiagnosticExchangeFailed failure =
            Assert.IsType<ScpiDiagnosticExchangeFailed>(observer.Events[^1]);
        Assert.Equal(ScpiDiagnosticOutcome.Disposed, failure.Outcome);
        Assert.Equal(ScpiDiagnosticFailureKind.Disposal, failure.FailureKind);
    }

    [Fact]
    public async Task QueryAsync_EndOfStreamObservesClassifiedFailure()
    {
        var observer = new RecordingObserver();
        var stream = new ScriptedScpiByteStream();
        await using var session = CreateSession(stream, observer);

        await Assert.ThrowsAsync<EndOfStreamException>(() => session.QueryAsync("READ?"));

        ScpiDiagnosticExchangeFailed failure =
            Assert.IsType<ScpiDiagnosticExchangeFailed>(observer.Events[^1]);
        Assert.Equal(ScpiDiagnosticOutcome.Failed, failure.Outcome);
        Assert.Equal(ScpiDiagnosticFailureKind.EndOfStream, failure.FailureKind);
    }

    [Fact]
    public async Task SendCommandAsync_WriteFailurePreservesUncertainOutcomeWithoutRetry()
    {
        var observer = new RecordingObserver();
        var stream = new ScriptedScpiByteStream(
            write: (_, _) => ValueTask.FromException(new IOException("private failure text")));
        await using var session = CreateSession(stream, observer);

        ScpiCommandTransmissionException exception =
            await Assert.ThrowsAsync<ScpiCommandTransmissionException>(() =>
                session.SendCommandAsync("OUTPUT ON"));

        Assert.True(exception.ExecutionMayHaveOccurred);
        Assert.Single(stream.Writes);
        ScpiDiagnosticExchangeFailed failure =
            Assert.IsType<ScpiDiagnosticExchangeFailed>(observer.Events[^1]);
        Assert.Equal(ScpiDiagnosticOutcome.Uncertain, failure.Outcome);
        Assert.Equal(ScpiDiagnosticFailureKind.Transport, failure.FailureKind);
        Assert.True(failure.ExecutionMayHaveOccurred);
        Assert.DoesNotContain(
            observer.Events.Select(value => value.ToString()),
            value => value?.Contains("private failure text", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task ObserverFailureNeverAffectsQueryOrSessionState()
    {
        var observer = new ThrowingObserver();
        var stream = ScriptedScpiByteStream.FromResponseChunks("OK\n"u8.ToArray());
        await using var session = CreateSession(stream, observer);

        Assert.Equal("OK", await session.QueryAsync("READ?"));
        Assert.Equal(ScpiTextSessionState.Open, session.State);
        Assert.Equal(4, observer.CallCount);
    }

    [Fact]
    public async Task ObservedBytesAreOwnedCopies()
    {
        var observer = new RecordingObserver();
        byte[] response = "OK\n"u8.ToArray();
        var stream = ScriptedScpiByteStream.FromResponseChunks(response);
        await using var session = CreateSession(stream, observer);

        await session.QueryAsync("READ?");
        response.AsSpan().Fill(0x58);

        ScpiDiagnosticBytesObserved received = observer.Events
            .OfType<ScpiDiagnosticBytesObserved>()
            .Single(value => value.Direction == ScpiDiagnosticDirection.Receive);
        byte[] firstCopy = received.ToArray();
        firstCopy.AsSpan().Fill(0x59);

        Assert.Equal("OK\n", Encoding.ASCII.GetString(received.ToArray()));
    }

    [Fact]
    public async Task ConcurrentQueriesProduceNonInterleavedDiagnosticSequences()
    {
        var observer = new RecordingObserver();
        var firstReadStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstRead = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int readNumber = 0;
        var stream = new ScriptedScpiByteStream(
            read: async (buffer, cancellationToken) =>
            {
                int current = Interlocked.Increment(ref readNumber);
                if (current == 1)
                {
                    firstReadStarted.SetResult();
                    await releaseFirstRead.Task.WaitAsync(cancellationToken);
                }

                byte[] response = current == 1
                    ? "FIRST\n"u8.ToArray()
                    : "SECOND\n"u8.ToArray();
                response.AsSpan().CopyTo(buffer.Span);
                return response.Length;
            });
        await using var session = CreateSession(
            stream,
            observer,
            timeout: TimeSpan.FromSeconds(5));

        Task<string> first = session.QueryAsync("FIRST?");
        await firstReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Task<string> second = session.QueryAsync("SECOND?");
        await Task.Yield();

        Assert.Single(observer.Events.Select(value => value.ExchangeId).Distinct());

        releaseFirstRead.SetResult();
        Assert.Equal("FIRST", await first);
        Assert.Equal("SECOND", await second);

        Guid[] exchangeOrder = observer.Events
            .Select(value => value.ExchangeId)
            .Distinct()
            .ToArray();
        Assert.Equal(2, exchangeOrder.Length);
        int firstLast = observer.Events.FindLastIndex(
            value => value.ExchangeId == exchangeOrder[0]);
        int secondFirst = observer.Events.FindIndex(
            value => value.ExchangeId == exchangeOrder[1]);
        Assert.True(firstLast < secondFirst);
    }

    [Fact]
    public void ObserverConstructorRejectsNullObserver()
    {
        var stream = new ScriptedScpiByteStream();

        Assert.Throws<ArgumentNullException>(() =>
            new ScpiTextSession(stream, CreateOptions(), null!));
    }

    private static ScpiTextSession CreateSession(
        IScpiByteStream stream,
        IScpiDiagnosticObserver observer,
        TimeSpan? timeout = null) =>
        new(stream, CreateOptions(timeout), observer);

    private static ScpiTextFramingOptions CreateOptions(TimeSpan? timeout = null) =>
        new(
            ScpiCommandTerminator.CarriageReturn,
            ScpiResponseTerminator.LineFeed,
            timeout ?? TimeSpan.FromSeconds(3),
            512);

    private sealed class RecordingObserver : IScpiDiagnosticObserver
    {
        public List<ScpiDiagnosticEvent> Events { get; } = [];

        public void Observe(ScpiDiagnosticEvent diagnosticEvent) =>
            Events.Add(diagnosticEvent);
    }

    private sealed class ThrowingObserver : IScpiDiagnosticObserver
    {
        public int CallCount { get; private set; }

        public void Observe(ScpiDiagnosticEvent diagnosticEvent)
        {
            CallCount++;
            throw new InvalidOperationException("observer failure");
        }
    }
}
