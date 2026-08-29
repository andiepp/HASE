using System.Diagnostics;

namespace Hase.Scpi.Tests;

public sealed class ScpiTextSessionLifecycleTests
{
    [Fact]
    public async Task QueuedQueryCancellation_DoesNotFaultSessionOrPerformIo()
    {
        var readStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stream = new ScriptedScpiByteStream(read: async (buffer, cancellationToken) =>
        {
            readStarted.SetResult();
            await releaseRead.Task.WaitAsync(cancellationToken);
            "OK\n"u8.CopyTo(buffer.Span);
            return 3;
        });
        await using var session = CreateSession(stream, TimeSpan.FromSeconds(5));
        var active = session.QueryAsync("ACTIVE?");
        await readStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        using var cancellation = new CancellationTokenSource();
        var queued = session.QueryAsync("QUEUED?", cancellation.Token);

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);

        Assert.Single(stream.Writes);
        Assert.Equal(ScpiTextSessionState.Open, session.State);
        releaseRead.SetResult();
        Assert.Equal("OK", await active);
    }

    [Fact]
    public async Task QueuedCommandCancellation_DoesNotCreateUncertainOutcome()
    {
        var readStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stream = new ScriptedScpiByteStream(read: async (buffer, cancellationToken) =>
        {
            readStarted.SetResult();
            await releaseRead.Task.WaitAsync(cancellationToken);
            "OK\n"u8.CopyTo(buffer.Span);
            return 3;
        });
        await using var session = CreateSession(stream, TimeSpan.FromSeconds(5));
        var active = session.QueryAsync("ACTIVE?");
        await readStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        using var cancellation = new CancellationTokenSource();
        var queued = session.SendCommandAsync("OUTPUT ON", cancellation.Token);

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);

        Assert.Single(stream.Writes);
        Assert.Equal(ScpiTextSessionState.Open, session.State);
        releaseRead.SetResult();
        Assert.Equal("OK", await active);
    }

    [Fact]
    public async Task ExchangeTimeout_StartsAfterSerializationOwnershipIsAcquired()
    {
        // The exchange timeout must start when an exchange acquires
        // serialization ownership, not when it was requested. The query below
        // waits for the command to release the gate and then needs its own
        // read time; together those exceed the timeout, so the query can only
        // succeed if its budget started at ownership.
        //
        // Ownership order is established by gate rather than by elapsed time:
        // the command's write blocks until released, and the query is only
        // requested once that write has started, so the query is provably
        // queued behind it. Only the two durations below are wall-clock, and
        // each leaves 400 ms of slack against the timeout so that scheduling
        // jitter under a parallel suite run cannot decide the outcome.
        TimeSpan exchangeTimeout = TimeSpan.FromMilliseconds(1000);
        TimeSpan ownershipHold = TimeSpan.FromMilliseconds(600);
        TimeSpan readDuration = TimeSpan.FromMilliseconds(600);

        var commandWriteStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCommandWrite = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var writeNumber = 0;
        var stream = new ScriptedScpiByteStream(
            write: async (_, cancellationToken) =>
            {
                if (Interlocked.Increment(ref writeNumber) == 1)
                {
                    commandWriteStarted.SetResult();
                    await releaseCommandWrite.Task.WaitAsync(cancellationToken);
                }
            },
            read: async (buffer, cancellationToken) =>
            {
                await Task.Delay(readDuration, cancellationToken);
                "OK\n"u8.CopyTo(buffer.Span);
                return 3;
            });
        await using var session = CreateSession(stream, exchangeTimeout);

        var command = session.SendCommandAsync("OUTPUT ON");
        await commandWriteStarted.Task;

        var requested = Stopwatch.StartNew();
        var query = session.QueryAsync("READ?");

        await Task.Delay(ownershipHold);
        releaseCommandWrite.SetResult();

        await command;
        Assert.Equal("OK", await query);
        requested.Stop();

        // Without this the test could pass vacuously: if the query had not
        // actually waited longer than the timeout, it would succeed whether or
        // not the budget started at ownership.
        Assert.True(
            requested.Elapsed > exchangeTimeout,
            $"The query completed in {requested.Elapsed}, which does not exceed "
            + $"the {exchangeTimeout} exchange timeout, so it does not "
            + "demonstrate that the timeout started at ownership.");
        Assert.Equal(ScpiTextSessionState.Open, session.State);
    }

    [Fact]
    public async Task ActiveQueryCancellation_FaultsSession()
    {
        var readStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stream = new ScriptedScpiByteStream(read: async (_, cancellationToken) =>
        {
            readStarted.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        });
        await using var session = CreateSession(stream, TimeSpan.FromSeconds(5));
        using var cancellation = new CancellationTokenSource();
        var query = session.QueryAsync("READ?", cancellation.Token);
        await readStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => query);

        Assert.Equal(ScpiTextSessionState.Faulted, session.State);
    }

    [Fact]
    public async Task ConcurrentDisposeAsync_CallersAwaitOneStreamDisposal()
    {
        var disposalStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseDisposal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stream = new ScriptedScpiByteStream(dispose: async () =>
        {
            disposalStarted.SetResult();
            await releaseDisposal.Task;
        });
        var session = CreateSession(stream, TimeSpan.FromSeconds(5));

        var first = session.DisposeAsync().AsTask();
        await disposalStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        var second = session.DisposeAsync().AsTask();

        Assert.False(first.IsCompleted);
        Assert.False(second.IsCompleted);
        Assert.Equal(1, stream.DisposeCount);
        releaseDisposal.SetResult();
        await Task.WhenAll(first, second);
        Assert.Equal(ScpiTextSessionState.Disposed, session.State);
    }

    private static ScpiTextSession CreateSession(IScpiByteStream stream, TimeSpan timeout) =>
        new(stream, new ScpiTextFramingOptions(
            ScpiCommandTerminator.CarriageReturn,
            ScpiResponseTerminator.LineFeed,
            timeout,
            512));
}
