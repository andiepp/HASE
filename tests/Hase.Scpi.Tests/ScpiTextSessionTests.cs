using System.Text;

namespace Hase.Scpi.Tests;

public sealed class ScpiTextSessionTests
{
    [Fact]
    public async Task QueryAsync_WritesFormattedRequestAndReturnsResponse()
    {
        var stream = ScriptedScpiByteStream.FromResponseChunks("HASE,MODEL\n"u8.ToArray());
        await using var session = CreateSession(stream);

        var response = await session.QueryAsync("*IDN?");

        Assert.Equal("HASE,MODEL", response);
        Assert.Equal("*IDN?\r", Encoding.ASCII.GetString(Assert.Single(stream.Writes)));
        Assert.Equal(ScpiTextSessionState.Open, session.State);
    }

    [Fact]
    public async Task QueryAsync_FramesResponseAcrossReadBoundaries()
    {
        var stream = ScriptedScpiByteStream.FromResponseChunks(
            "12"u8.ToArray(),
            "3\r"u8.ToArray(),
            "\n"u8.ToArray());
        await using var session = CreateSession(
            stream,
            responseTerminator: ScpiResponseTerminator.CarriageReturnLineFeed);

        Assert.Equal("123", await session.QueryAsync("MEAS?"));
    }

    [Fact]
    public async Task QueryAsync_SerializesConcurrentExchanges()
    {
        var firstReadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readNumber = 0;
        var stream = new ScriptedScpiByteStream(read: async (buffer, cancellationToken) =>
        {
            var currentRead = Interlocked.Increment(ref readNumber);
            if (currentRead == 1)
            {
                firstReadStarted.SetResult();
                await releaseFirstRead.Task.WaitAsync(cancellationToken);
            }

            var response = currentRead == 1 ? "FIRST\n"u8.ToArray() : "SECOND\n"u8.ToArray();
            response.AsSpan().CopyTo(buffer.Span);
            return response.Length;
        });
        await using var session = CreateSession(stream, timeout: TimeSpan.FromSeconds(5));

        var first = session.QueryAsync("FIRST?");
        await firstReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        var second = session.QueryAsync("SECOND?");

        Assert.Single(stream.Writes);
        releaseFirstRead.SetResult();

        Assert.Equal("FIRST", await first);
        Assert.Equal("SECOND", await second);
        Assert.Equal(2, stream.Writes.Count);
    }

    [Fact]
    public async Task QueryAsync_RejectsEndOfStreamAndFaultsSession()
    {
        var stream = new ScriptedScpiByteStream();
        await using var session = CreateSession(stream);

        await Assert.ThrowsAsync<EndOfStreamException>(() => session.QueryAsync("READ?"));

        Assert.Equal(ScpiTextSessionState.Faulted, session.State);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.QueryAsync("READ?"));
    }

    [Fact]
    public async Task QueryAsync_RejectsMalformedResponseAndFaultsSession()
    {
        var stream = ScriptedScpiByteStream.FromResponseChunks([0x41, 0x80, 0x0A]);
        await using var session = CreateSession(stream);

        await Assert.ThrowsAsync<InvalidDataException>(() => session.QueryAsync("READ?"));

        Assert.Equal(ScpiTextSessionState.Faulted, session.State);
    }

    [Fact]
    public async Task QueryAsync_RejectsInvalidReadCountAndFaultsSession()
    {
        var stream = new ScriptedScpiByteStream(read: (buffer, _) =>
            ValueTask.FromResult(buffer.Length + 1));
        await using var session = CreateSession(stream);

        await Assert.ThrowsAsync<InvalidDataException>(() => session.QueryAsync("READ?"));

        Assert.Equal(ScpiTextSessionState.Faulted, session.State);
    }

    [Fact]
    public async Task QueryAsync_PropagatesWriteFailureAndFaultsSession()
    {
        var failure = new IOException("write failed");
        var stream = new ScriptedScpiByteStream(write: (_, _) => ValueTask.FromException(failure));
        await using var session = CreateSession(stream);

        var actual = await Assert.ThrowsAsync<IOException>(() => session.QueryAsync("READ?"));

        Assert.Same(failure, actual);
        Assert.Equal(ScpiTextSessionState.Faulted, session.State);
    }

    [Fact]
    public async Task QueryAsync_PropagatesReadFailureAndFaultsSession()
    {
        var failure = new IOException("read failed");
        var stream = new ScriptedScpiByteStream(read: (_, _) => ValueTask.FromException<int>(failure));
        await using var session = CreateSession(stream);

        var actual = await Assert.ThrowsAsync<IOException>(() => session.QueryAsync("READ?"));

        Assert.Same(failure, actual);
        Assert.Equal(ScpiTextSessionState.Faulted, session.State);
    }

    [Fact]
    public async Task QueryAsync_EnforcesTimeoutWhenStreamIgnoresCancellation()
    {
        var neverCompletes = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stream = new ScriptedScpiByteStream(read: (_, _) => new ValueTask<int>(neverCompletes.Task));
        await using var session = CreateSession(stream, timeout: TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAsync<TimeoutException>(() => session.QueryAsync("READ?"));

        Assert.Equal(ScpiTextSessionState.Faulted, session.State);
    }

    [Fact]
    public async Task QueryAsync_PreCanceledRequestDoesNotUseOrFaultSession()
    {
        var stream = ScriptedScpiByteStream.FromResponseChunks("OK\n"u8.ToArray());
        await using var session = CreateSession(stream);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            session.QueryAsync("READ?", cancellation.Token));

        Assert.Empty(stream.Writes);
        Assert.Equal(ScpiTextSessionState.Open, session.State);
        Assert.Equal("OK", await session.QueryAsync("READ?"));
    }

    [Fact]
    public async Task QueryAsync_RejectsInvalidRequestWithoutUsingOrFaultingSession()
    {
        var stream = ScriptedScpiByteStream.FromResponseChunks("OK\n"u8.ToArray());
        await using var session = CreateSession(stream);

        await Assert.ThrowsAsync<ArgumentException>(() => session.QueryAsync("READ?\nNEXT?"));

        Assert.Empty(stream.Writes);
        Assert.Equal(ScpiTextSessionState.Open, session.State);
    }

    [Fact]
    public async Task SendCommandAsync_WritesFormattedCommandWithoutReading()
    {
        var stream = new ScriptedScpiByteStream(read: (_, _) =>
            ValueTask.FromException<int>(new InvalidOperationException("A command must not read.")));
        await using var session = CreateSession(stream);

        await session.SendCommandAsync("OUTPUT ON");

        Assert.Equal("OUTPUT ON\r", Encoding.ASCII.GetString(Assert.Single(stream.Writes)));
        Assert.Equal(ScpiTextSessionState.Open, session.State);
    }

    [Fact]
    public async Task SendCommandAsync_RejectsInvalidCommandWithoutUsingOrFaultingSession()
    {
        var stream = new ScriptedScpiByteStream();
        await using var session = CreateSession(stream);

        await Assert.ThrowsAsync<ArgumentException>(() => session.SendCommandAsync("OUTPUT ON\nNEXT"));

        Assert.Empty(stream.Writes);
        Assert.Equal(ScpiTextSessionState.Open, session.State);
    }

    [Fact]
    public async Task SendCommandAsync_PreCanceledCommandDoesNotUseOrFaultSession()
    {
        var stream = new ScriptedScpiByteStream();
        await using var session = CreateSession(stream);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            session.SendCommandAsync("OUTPUT ON", cancellation.Token));

        Assert.Empty(stream.Writes);
        Assert.Equal(ScpiTextSessionState.Open, session.State);
    }

    [Fact]
    public async Task SendCommandAsync_WriteFailureReportsUncertainOutcomeAndFaultsSession()
    {
        var failure = new IOException("write failed");
        var stream = new ScriptedScpiByteStream(write: (_, _) => ValueTask.FromException(failure));
        await using var session = CreateSession(stream);

        var actual = await Assert.ThrowsAsync<ScpiCommandTransmissionException>(() =>
            session.SendCommandAsync("OUTPUT ON"));

        Assert.True(actual.ExecutionMayHaveOccurred);
        Assert.Same(failure, actual.InnerException);
        Assert.Equal(ScpiTextSessionState.Faulted, session.State);
    }

    [Fact]
    public async Task SendCommandAsync_TimeoutReportsUncertainOutcomeAndFaultsSession()
    {
        var neverCompletes = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stream = new ScriptedScpiByteStream(write: (_, _) => new ValueTask(neverCompletes.Task));
        await using var session = CreateSession(stream, timeout: TimeSpan.FromMilliseconds(100));

        var actual = await Assert.ThrowsAsync<ScpiCommandTransmissionException>(() =>
            session.SendCommandAsync("OUTPUT ON"));

        Assert.True(actual.ExecutionMayHaveOccurred);
        Assert.IsType<TimeoutException>(actual.InnerException);
        Assert.Equal(ScpiTextSessionState.Faulted, session.State);
    }

    [Fact]
    public async Task SendCommandAsync_CancellationDuringWriteReportsUncertainOutcomeAndFaultsSession()
    {
        var writeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stream = new ScriptedScpiByteStream(write: async (_, cancellationToken) =>
        {
            writeStarted.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        });
        await using var session = CreateSession(stream, timeout: TimeSpan.FromSeconds(5));
        using var cancellation = new CancellationTokenSource();

        var command = session.SendCommandAsync("OUTPUT ON", cancellation.Token);
        await writeStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        cancellation.Cancel();
        var actual = await Assert.ThrowsAsync<ScpiCommandTransmissionException>(() => command);

        Assert.True(actual.ExecutionMayHaveOccurred);
        Assert.IsAssignableFrom<OperationCanceledException>(actual.InnerException);
        Assert.Equal(ScpiTextSessionState.Faulted, session.State);
    }

    [Fact]
    public async Task SendCommandAsync_SerializesBehindActiveQuery()
    {
        var queryReadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseQueryRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stream = new ScriptedScpiByteStream(read: async (buffer, cancellationToken) =>
        {
            queryReadStarted.SetResult();
            await releaseQueryRead.Task.WaitAsync(cancellationToken);
            "OK\n"u8.CopyTo(buffer.Span);
            return 3;
        });
        await using var session = CreateSession(stream, timeout: TimeSpan.FromSeconds(5));

        var query = session.QueryAsync("READ?");
        await queryReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        var command = session.SendCommandAsync("OUTPUT ON");

        Assert.Single(stream.Writes);
        releaseQueryRead.SetResult();
        Assert.Equal("OK", await query);
        await command;

        Assert.Equal(2, stream.Writes.Count);
        Assert.Equal("OUTPUT ON\r", Encoding.ASCII.GetString(stream.Writes[1]));
    }

    [Fact]
    public async Task SendCommandAsync_FaultedSessionRejectsFurtherCommandsWithoutRetry()
    {
        var stream = new ScriptedScpiByteStream(write: (_, _) =>
            ValueTask.FromException(new IOException("write failed")));
        await using var session = CreateSession(stream);
        await Assert.ThrowsAsync<ScpiCommandTransmissionException>(() =>
            session.SendCommandAsync("OUTPUT ON"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.SendCommandAsync("OUTPUT ON"));

        Assert.Single(stream.Writes);
    }

    [Fact]
    public async Task SendCommandAsync_DisposedSessionRejectsCommand()
    {
        var stream = new ScriptedScpiByteStream();
        var session = CreateSession(stream);
        await session.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            session.SendCommandAsync("OUTPUT ON"));

        Assert.Empty(stream.Writes);
    }

    [Fact]
    public async Task DisposeAsync_DisposesOwnedStreamAndIsIdempotent()
    {
        var stream = new ScriptedScpiByteStream();
        var session = CreateSession(stream);

        await session.DisposeAsync();
        await session.DisposeAsync();

        Assert.Equal(ScpiTextSessionState.Disposed, session.State);
        Assert.Equal(1, stream.DisposeCount);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => session.QueryAsync("READ?"));
    }

    [Fact]
    public void Constructor_RejectsNullDependencies()
    {
        var options = CreateOptions();
        var stream = new ScriptedScpiByteStream();

        Assert.Throws<ArgumentNullException>(() => new ScpiTextSession(null!, options));
        Assert.Throws<ArgumentNullException>(() => new ScpiTextSession(stream, null!));
    }

    private static ScpiTextSession CreateSession(
        IScpiByteStream stream,
        ScpiResponseTerminator responseTerminator = ScpiResponseTerminator.LineFeed,
        TimeSpan? timeout = null) =>
        new(stream, CreateOptions(responseTerminator, timeout));

    private static ScpiTextFramingOptions CreateOptions(
        ScpiResponseTerminator responseTerminator = ScpiResponseTerminator.LineFeed,
        TimeSpan? timeout = null) =>
        new(
            ScpiCommandTerminator.CarriageReturn,
            responseTerminator,
            timeout ?? TimeSpan.FromSeconds(3),
            512);
}
