using Hase.Scpi;

namespace Hase.Scpi.Kel103.Runtime.Tests;

public sealed class Kel103InputControlSessionTests
{
    [Theory]
    [InlineData("CC")]
    [InlineData("CV")]
    [InlineData("CR")]
    [InlineData("CW")]
    public async Task Activate_QueriesStateAndModeThenTransmitsOnceAndConfirmsOn(
        string mode)
    {
        var time = new FixedTimeProvider(
            new DateTimeOffset(2026, 8, 6, 12, 0, 0, TimeSpan.Zero));
        var session = new FakeSession("OFF", mode, "ON");
        await using var adapter = new Kel103ReadOnlySessionAdapter(session, time);

        Kel103InputControlMutationResult result = await adapter.ActivateInputAsync();

        Assert.True(result.InputEnabled);
        Assert.Equal(time.GetUtcNow(), result.TimestampUtc);
        Assert.Equal(new[] { ":INPut?", ":FUNCtion?", ":INPut?" }, session.Queries);
        Assert.Equal(new[] { ":INPut ON" }, session.Commands);
        Assert.False(adapter.IsFaulted);
    }

    [Fact]
    public async Task Activate_QueriesAuthoritativeStateEvenWhenAlreadyOn()
    {
        var session = new FakeSession("ON", "CC", "ON");
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);

        Kel103InputControlMutationResult result = await adapter.ActivateInputAsync();

        Assert.True(result.InputEnabled);
        Assert.Equal(new[] { ":INPut?", ":FUNCtion?", ":INPut?" }, session.Queries);
        Assert.Equal(new[] { ":INPut ON" }, session.Commands);
    }

    [Fact]
    public async Task Activate_ShortRejectsBeforeTransmissionAndSessionRemainsUsable()
    {
        var session = new FakeSession("OFF", "SHORt", "OFF");
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => adapter.ActivateInputAsync());

        Assert.Equal("Generic KEL-103 input activation rejects SHORT mode.", exception.Message);
        Assert.Equal(new[] { ":INPut?", ":FUNCtion?" }, session.Queries);
        Assert.Empty(session.Commands);
        Assert.False(adapter.IsFaulted);
        Assert.False((await adapter.ReadInputStateAsync()).InputEnabled);
    }

    [Fact]
    public async Task Activate_PreCanceledUsesNoScpiAndDoesNotFault()
    {
        var session = new FakeSession();
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            adapter.ActivateInputAsync(cancellation.Token));

        Assert.Empty(session.Queries);
        Assert.Empty(session.Commands);
        Assert.False(adapter.IsFaulted);
    }

    [Fact]
    public async Task Activate_TransmissionUncertaintyIsPreservedWithoutRetry()
    {
        var transmission = new ScpiCommandTransmissionException(
            "raw transmission detail",
            true,
            new IOException("raw transport detail"));
        var session = new FakeSession("OFF", "CC") { SendException = transmission };
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);

        ScpiCommandTransmissionException actual =
            await Assert.ThrowsAsync<ScpiCommandTransmissionException>(() =>
                adapter.ActivateInputAsync());

        Assert.Same(transmission, actual);
        Assert.True(adapter.IsFaulted);
        Assert.Single(session.Commands);
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.ActivateInputAsync());
        Assert.Single(session.Commands);
    }

    [Theory]
    [InlineData("OFF")]
    [InlineData("MALFORMED")]
    public async Task Activate_UnconfirmedReadbackIsUncertainAndSanitized(string readback)
    {
        var session = new FakeSession("OFF", "CC", readback);
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);

        Kel103MutationOutcomeUncertainException exception =
            await Assert.ThrowsAsync<Kel103MutationOutcomeUncertainException>(() =>
                adapter.ActivateInputAsync());

        Assert.True(exception.ExecutionMayHaveOccurred);
        Assert.DoesNotContain(readback, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(":INPut ON", exception.Message, StringComparison.Ordinal);
        Assert.True(adapter.IsFaulted);
        Assert.Single(session.Commands);
    }

    [Fact]
    public async Task Activate_ReadbackCancellationAfterTransmissionIsUncertain()
    {
        using var cancellation = new CancellationTokenSource();
        var session = new FakeSession("OFF", "CC") { CommandSent = cancellation.Cancel };
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);

        Kel103MutationOutcomeUncertainException exception =
            await Assert.ThrowsAsync<Kel103MutationOutcomeUncertainException>(() =>
                adapter.ActivateInputAsync(cancellation.Token));

        Assert.IsAssignableFrom<OperationCanceledException>(exception.InnerException);
        Assert.True(adapter.IsFaulted);
        Assert.Single(session.Commands);
    }

    [Theory]
    [InlineData("OFF")]
    [InlineData("ON")]
    public async Task Deactivate_HasNoCachedStateOrModePreconditionAndConfirmsOff(
        string unusedPhysicalStartingState)
    {
        _ = unusedPhysicalStartingState;
        var time = new FixedTimeProvider(
            new DateTimeOffset(2026, 8, 6, 12, 30, 0, TimeSpan.Zero));
        var session = new FakeSession("OFF");
        await using var adapter = new Kel103ReadOnlySessionAdapter(session, time);

        Kel103InputControlMutationResult result = await adapter.DeactivateInputAsync();

        Assert.False(result.InputEnabled);
        Assert.Equal(time.GetUtcNow(), result.TimestampUtc);
        Assert.Equal(new[] { ":INPut?" }, session.Queries);
        Assert.Equal(new[] { ":INPut OFF" }, session.Commands);
        Assert.False(adapter.IsFaulted);
    }

    [Fact]
    public async Task Deactivate_TransmissionUncertaintyIsPreservedWithoutRetry()
    {
        var transmission = new ScpiCommandTransmissionException(
            "uncertain",
            true,
            new IOException("write failed"));
        var session = new FakeSession { SendException = transmission };
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);

        ScpiCommandTransmissionException actual =
            await Assert.ThrowsAsync<ScpiCommandTransmissionException>(() =>
                adapter.DeactivateInputAsync());

        Assert.Same(transmission, actual);
        Assert.True(adapter.IsFaulted);
        Assert.Single(session.Commands);
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.DeactivateInputAsync());
        Assert.Single(session.Commands);
    }

    [Theory]
    [InlineData("ON")]
    [InlineData("MALFORMED")]
    public async Task Deactivate_UnconfirmedReadbackIsUncertainAndSanitized(string readback)
    {
        var session = new FakeSession(readback);
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);

        Kel103MutationOutcomeUncertainException exception =
            await Assert.ThrowsAsync<Kel103MutationOutcomeUncertainException>(() =>
                adapter.DeactivateInputAsync());

        Assert.True(exception.ExecutionMayHaveOccurred);
        Assert.DoesNotContain(readback, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(":INPut OFF", exception.Message, StringComparison.Ordinal);
        Assert.True(adapter.IsFaulted);
        Assert.Single(session.Commands);
    }

    [Fact]
    public async Task InputControl_DoesNotInterleaveWithConcurrentRead()
    {
        var session = new BlockingInputControlSession();
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);

        Task<Kel103InputControlMutationResult> mutation = adapter.ActivateInputAsync();
        await session.FirstQueryStarted.Task;
        Task<Kel103Identity> concurrentRead = adapter.ReadIdentityAsync();

        Assert.Equal(new[] { ":INPut?" }, session.Queries);
        session.ReleaseFirstQuery.SetResult();
        await mutation;
        await concurrentRead;

        Assert.Equal(
            new[] { ":INPut?", ":FUNCtion?", ":INPut?", "*IDN?" },
            session.Queries);
        Assert.Equal(new[] { ":INPut ON" }, session.Commands);
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class FakeSession(params string[] responses) : IScpiTextSession
    {
        private readonly Queue<string> pending = new(responses);

        public List<string> Queries { get; } = [];
        public List<string> Commands { get; } = [];
        public Exception? SendException { get; init; }
        public Action? CommandSent { get; init; }
        public ScpiTextSessionState State => ScpiTextSessionState.Open;

        public Task<string> QueryAsync(
            string query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Queries.Add(query);
            return Task.FromResult(pending.Dequeue());
        }

        public Task SendCommandAsync(
            string command,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Commands.Add(command);
            CommandSent?.Invoke();
            return SendException is null
                ? Task.CompletedTask
                : Task.FromException(SendException);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class BlockingInputControlSession : IScpiTextSession
    {
        private readonly Queue<string> responses = new(
        [
            "OFF",
            "CC",
            "ON",
            "RND 320-KEL103 V3.30 SN:REDACTED"
        ]);

        public TaskCompletionSource FirstQueryStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFirstQuery { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<string> Queries { get; } = [];
        public List<string> Commands { get; } = [];
        public ScpiTextSessionState State => ScpiTextSessionState.Open;

        public async Task<string> QueryAsync(
            string query,
            CancellationToken cancellationToken = default)
        {
            Queries.Add(query);
            if (Queries.Count == 1)
            {
                FirstQueryStarted.SetResult();
                await ReleaseFirstQuery.Task.WaitAsync(cancellationToken);
            }

            return responses.Dequeue();
        }

        public Task SendCommandAsync(
            string command,
            CancellationToken cancellationToken = default)
        {
            Commands.Add(command);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
