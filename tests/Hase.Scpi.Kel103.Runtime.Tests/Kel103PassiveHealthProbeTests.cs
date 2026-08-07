using Hase.Scpi;

namespace Hase.Scpi.Kel103.Runtime.Tests;

public sealed class Kel103PassiveHealthProbeTests
{
    [Fact]
    public async Task ProbeHealthAsync_SendsExactlyOneCharacterizedIdentityQuery()
    {
        var session = new RecordingSession("RND 320-KEL103 V3.30 SN:REDACTED");
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);

        await adapter.ProbeHealthAsync();

        Assert.Equal(["*IDN?"], session.Queries);
        Assert.Empty(session.Commands);
        Assert.False(adapter.IsFaulted);
    }

    [Fact]
    public async Task ProbeHealthAsync_InvalidIdentityFaultsSessionAndPreventsLaterTraffic()
    {
        var session = new RecordingSession("unexpected instrument");
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);

        await Assert.ThrowsAsync<InvalidDataException>(() => adapter.ProbeHealthAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.ProbeHealthAsync());

        Assert.Single(session.Queries);
        Assert.True(adapter.IsFaulted);
    }

    [Fact]
    public async Task ProbeHealthAsync_DoesNotOverlapActiveMutation()
    {
        var session = new BlockingMutationSession();
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);

        Task<Kel103SetpointMutationResult> mutation = adapter.WriteSetpointAsync(
            Kel103SetpointMapping.Current,
            0.25m);
        await session.FirstQueryStarted.Task;

        Task probe = adapter.ProbeHealthAsync();
        await Task.Yield();

        Assert.Equal([":INPut?"], session.Queries);
        Assert.Empty(session.Commands);

        session.ReleaseFirstQuery.SetResult();
        await mutation;
        await probe;

        Assert.Equal(
            [":INPut?", ":CURRent?", ":FUNCtion?", "*IDN?"],
            session.Queries);
        Assert.Equal([":CURRent 0.25A"], session.Commands);
    }

    [Fact]
    public async Task ProbeHealthAsync_AfterDisposalDoesNotUseSession()
    {
        var session = new RecordingSession("RND 320-KEL103 V3.30 SN:REDACTED");
        var adapter = new Kel103ReadOnlySessionAdapter(session);
        await adapter.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => adapter.ProbeHealthAsync());

        Assert.Empty(session.Queries);
        Assert.Equal(1, session.DisposeCount);
    }

    private sealed class RecordingSession(params string[] responses) : IScpiTextSession
    {
        private readonly Queue<string> remaining = new(responses);

        public List<string> Queries { get; } = [];
        public List<string> Commands { get; } = [];
        public int DisposeCount { get; private set; }
        public ScpiTextSessionState State => ScpiTextSessionState.Open;

        public Task<string> QueryAsync(
            string query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Queries.Add(query);
            return Task.FromResult(remaining.Dequeue());
        }

        public Task SendCommandAsync(
            string command,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Commands.Add(command);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class BlockingMutationSession : IScpiTextSession
    {
        private readonly Queue<string> responses = new(
        [
            "OFF",
            "0.25A",
            "CC",
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
