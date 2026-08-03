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
        var writeNumber = 0;
        var stream = new ScriptedScpiByteStream(
            write: async (_, cancellationToken) =>
            {
                if (Interlocked.Increment(ref writeNumber) == 1)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken);
                }
            },
            read: async (buffer, cancellationToken) =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
                "OK\n"u8.CopyTo(buffer.Span);
                return 3;
            });
        await using var session = CreateSession(stream, TimeSpan.FromMilliseconds(200));

        var command = session.SendCommandAsync("OUTPUT ON");
        var query = session.QueryAsync("READ?");

        await command;
        Assert.Equal("OK", await query);
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
