using Hase.Scpi;

namespace Hase.Scpi.Kel103.Runtime.Tests;

public sealed class Kel103ReadOnlySessionAdapterTests
{
    [Fact]
    public async Task Synchronize_QueriesExactOrderAndReturnsOneTimestamp()
    {
        var session = new FakeSession("RND 320-KEL103 V3.30 SN:REDACTED", "9.8864V", "0.1000A", "0.9893W");
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero));
        await using var adapter = new Kel103ReadOnlySessionAdapter(session, time);

        Kel103SynchronizationSnapshot result = await adapter.VerifyAndSynchronizeAsync();

        Assert.Equal(new[] { "*IDN?", ":MEASure:VOLTage?", ":MEASure:CURRent?", ":MEASure:POWer?" }, session.Queries);
        Assert.Equal("KEL-103", result.Identity.ProductIdentity);
        Assert.Equal(9.8864m, result.Voltage);
        Assert.Equal(0.1000m, result.Current);
        Assert.Equal(0.9893m, result.Power);
        Assert.Equal(time.GetUtcNow(), result.TimestampUtc);
    }

    [Theory]
    [InlineData(0, "1.25V", "1.25")]
    [InlineData(1, "0.10A", "0.10")]
    [InlineData(2, "0.125W", "0.125")]
    public async Task ReadMeasurement_UsesSelectedProductionMapping(int index, string response, string expected)
    {
        var session = new FakeSession(response);
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);

        Kel103MeasurementObservation result = await adapter.ReadMeasurementAsync(
            Kel103MeasurementMapping.All[index]);

        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), result.Value);
        Assert.Equal((Kel103Measurement)index, result.Measurement);
        Assert.Equal(Kel103MeasurementMapping.All[index].Query, Assert.Single(session.Queries));
        Assert.Equal(TimeSpan.Zero, result.TimestampUtc.Offset);
    }

    [Fact]
    public async Task ReadIdentity_ReturnsSanitizedIdentity()
    {
        var session = new FakeSession("RND 320-KEL103 V3.30 SN:REDACTED");
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);
        Kel103Identity identity = await adapter.ReadIdentityAsync();
        Assert.Equal("KEL-103 V3.30", identity.ToString());
        Assert.DoesNotContain("REDACTED", identity.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SynchronizeOperatingState_QueriesExactOrderAndReturnsOneTimestamp()
    {
        var session = new FakeSession(
            "RND 320-KEL103 V3.30 SN:REDACTED",
            "9.8864V",
            "0.1000A",
            "0.9893W",
            "CR",
            "OFF",
            "10.000V",
            "0.1000A",
            "100.00OHM",
            "1.000W");
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero));
        await using var adapter = new Kel103ReadOnlySessionAdapter(session, time);

        Kel103OperatingStateSynchronizationSnapshot result =
            await adapter.VerifyAndSynchronizeOperatingStateAsync();

        Assert.Equal(
            new[]
            {
                "*IDN?",
                ":MEASure:VOLTage?",
                ":MEASure:CURRent?",
                ":MEASure:POWer?",
                ":FUNCtion?",
                ":INPut?",
                ":VOLTage?",
                ":CURRent?",
                ":RESistance?",
                ":POWer?"
            },
            session.Queries);
        Assert.Equal("KEL-103", result.Identity.ProductIdentity);
        Assert.Equal(9.8864m, result.Voltage);
        Assert.Equal(0.1000m, result.Current);
        Assert.Equal(0.9893m, result.Power);
        Assert.Equal(Kel103OperatingMode.ConstantResistance, result.OperatingMode);
        Assert.False(result.InputEnabled);
        Assert.Equal(10.000m, result.TargetVoltage);
        Assert.Equal(0.1000m, result.TargetCurrent);
        Assert.Equal(100.00m, result.TargetResistance);
        Assert.Equal(1.000m, result.TargetPower);
        Assert.Equal(time.GetUtcNow(), result.TimestampUtc);
    }

    [Fact]
    public async Task ReadOperatingMode_UsesExactQueryAndReturnsTimestamp()
    {
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero));
        var session = new FakeSession("SHORt");
        await using var adapter = new Kel103ReadOnlySessionAdapter(session, time);

        Kel103OperatingModeObservation result = await adapter.ReadOperatingModeAsync();

        Assert.Equal(Kel103OperatingMode.ShortCircuit, result.Mode);
        Assert.Equal(time.GetUtcNow(), result.TimestampUtc);
        Assert.Equal(new[] { ":FUNCtion?" }, session.Queries);
    }

    [Fact]
    public async Task ReadInputState_UsesExactQueryAndReturnsTimestamp()
    {
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero));
        var session = new FakeSession("ON");
        await using var adapter = new Kel103ReadOnlySessionAdapter(session, time);

        Kel103InputStateObservation result = await adapter.ReadInputStateAsync();

        Assert.True(result.InputEnabled);
        Assert.Equal(time.GetUtcNow(), result.TimestampUtc);
        Assert.Equal(new[] { ":INPut?" }, session.Queries);
    }

    [Theory]
    [InlineData(0, "1.25V", "1.25")]
    [InlineData(1, "0.10A", "0.10")]
    [InlineData(2, "100.0OHM", "100.0")]
    [InlineData(3, "0.125W", "0.125")]
    public async Task ReadSetpoint_UsesSelectedProductionMapping(
        int index,
        string response,
        string expected)
    {
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero));
        var session = new FakeSession(response);
        await using var adapter = new Kel103ReadOnlySessionAdapter(session, time);

        Kel103SetpointObservation result = await adapter.ReadSetpointAsync(
            Kel103SetpointMapping.All[index]);

        Assert.Equal((Kel103Setpoint)index, result.Setpoint);
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), result.Value);
        Assert.Equal(time.GetUtcNow(), result.TimestampUtc);
        Assert.Equal(Kel103SetpointMapping.All[index].Query, Assert.Single(session.Queries));
    }

    [Fact]
    public async Task InvalidOperatingState_FaultsAndPreventsLaterQuery()
    {
        var session = new FakeSession("WRONG");
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);

        await Assert.ThrowsAsync<InvalidDataException>(() => adapter.ReadOperatingModeAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.ReadInputStateAsync());

        Assert.True(adapter.IsFaulted);
        Assert.Equal(new[] { ":FUNCtion?" }, session.Queries);
    }

    [Fact]
    public async Task PreCanceledOperatingStateSynchronization_SendsNoQuery()
    {
        var session = new FakeSession();
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            adapter.VerifyAndSynchronizeOperatingStateAsync(cancellation.Token));

        Assert.Empty(session.Queries);
        Assert.False(adapter.IsFaulted);
    }

    [Fact]
    public async Task OperatingStateSynchronization_DoesNotInterleaveWithConcurrentRead()
    {
        var session = new OperatingStateBlockingSession();
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);

        Task<Kel103OperatingStateSynchronizationSnapshot> synchronization =
            adapter.VerifyAndSynchronizeOperatingStateAsync();
        await session.FirstQueryStarted.Task;
        Task<Kel103InputStateObservation> concurrentRead = adapter.ReadInputStateAsync();

        Assert.Equal(new[] { "*IDN?" }, session.Queries);
        session.ReleaseFirstQuery.SetResult();
        await synchronization;
        await concurrentRead;

        Assert.Equal(
            new[]
            {
                "*IDN?",
                ":MEASure:VOLTage?",
                ":MEASure:CURRent?",
                ":MEASure:POWer?",
                ":FUNCtion?",
                ":INPut?",
                ":VOLTage?",
                ":CURRent?",
                ":RESistance?",
                ":POWer?",
                ":INPut?"
            },
            session.Queries);
    }

    [Fact]
    public async Task InvalidMeasurement_FaultsAndPreventsLaterQuery()
    {
        var session = new FakeSession("1.0A");
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            adapter.ReadMeasurementAsync(Kel103MeasurementMapping.Voltage));
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.ReadIdentityAsync());
        Assert.True(adapter.IsFaulted);
        Assert.Single(session.Queries);
    }

    [Fact]
    public async Task WrongIdentity_FaultsWithoutMeasurementQueries()
    {
        var session = new FakeSession("RND OTHER V3.30 SN:REDACTED");
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);
        await Assert.ThrowsAsync<InvalidDataException>(() => adapter.VerifyAndSynchronizeAsync());
        Assert.Equal(new[] { "*IDN?" }, session.Queries);
        Assert.True(adapter.IsFaulted);
    }

    [Fact]
    public async Task PreCanceledOperation_SendsNoQuery()
    {
        var session = new FakeSession();
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            adapter.ReadIdentityAsync(cancellation.Token));
        Assert.Empty(session.Queries);
        Assert.False(adapter.IsFaulted);
    }

    [Fact]
    public async Task Dispose_DisposesSessionOnceAndRejectsReuse()
    {
        var session = new FakeSession();
        var adapter = new Kel103ReadOnlySessionAdapter(session);
        await adapter.DisposeAsync();
        await adapter.DisposeAsync();
        Assert.Equal(1, session.DisposeCount);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => adapter.ReadIdentityAsync());
    }

    [Fact]
    public async Task Synchronization_DoesNotInterleaveWithConcurrentRead()
    {
        var session = new BlockingSession();
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);

        Task<Kel103SynchronizationSnapshot> synchronization = adapter.VerifyAndSynchronizeAsync();
        await session.FirstQueryStarted.Task;
        Task<Kel103Identity> concurrentRead = adapter.ReadIdentityAsync();

        Assert.Equal(new[] { "*IDN?" }, session.Queries);
        session.ReleaseFirstQuery.SetResult();
        await synchronization;
        await concurrentRead;

        Assert.Equal(
            new[] { "*IDN?", ":MEASure:VOLTage?", ":MEASure:CURRent?", ":MEASure:POWer?", "*IDN?" },
            session.Queries);
    }

    [Fact]
    public async Task NullDependencies_AreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new Kel103ReadOnlySessionAdapter(null!));
        var session = new FakeSession();
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);
        await Assert.ThrowsAsync<ArgumentNullException>(() => adapter.ReadMeasurementAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => adapter.ReadSetpointAsync(null!));
    }

    [Fact]
    public void Assembly_DoesNotReferenceDeferredLayers()
    {
        string[] references = typeof(Kel103ReadOnlySessionAdapter).Assembly
            .GetReferencedAssemblies().Select(value => value.Name ?? string.Empty).ToArray();
        Assert.DoesNotContain(references, name => name == "Hase.Transport");
        Assert.DoesNotContain(references, name => name == "Hase.Runtime.Transport");
        Assert.DoesNotContain(references, name => name.Contains("Grpc", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.Contains("Wpf", StringComparison.Ordinal));
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class FakeSession(params string[] responses) : IScpiTextSession
    {
        private readonly Queue<string> pending = new(responses);
        public List<string> Queries { get; } = [];
        public int DisposeCount { get; private set; }
        public ScpiTextSessionState State => DisposeCount == 0 ? ScpiTextSessionState.Open : ScpiTextSessionState.Disposed;
        public Task<string> QueryAsync(string query, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Queries.Add(query);
            return Task.FromResult(pending.Dequeue());
        }
        public Task SendCommandAsync(string command, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class BlockingSession : IScpiTextSession
    {
        private readonly Queue<string> responses = new(
            ["RND 320-KEL103 V3.30 SN:REDACTED", "1V", "0.1A", "0.1W", "RND 320-KEL103 V3.30 SN:REDACTED"]);
        public TaskCompletionSource FirstQueryStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFirstQuery { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<string> Queries { get; } = [];
        public ScpiTextSessionState State => ScpiTextSessionState.Open;
        public async Task<string> QueryAsync(string query, CancellationToken cancellationToken = default)
        {
            Queries.Add(query);
            if (Queries.Count == 1)
            {
                FirstQueryStarted.SetResult();
                await ReleaseFirstQuery.Task.WaitAsync(cancellationToken);
            }
            return responses.Dequeue();
        }
        public Task SendCommandAsync(string command, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class OperatingStateBlockingSession : IScpiTextSession
    {
        private readonly Queue<string> responses = new(
        [
            "RND 320-KEL103 V3.30 SN:REDACTED",
            "1V",
            "0.1A",
            "0.1W",
            "CC",
            "OFF",
            "1V",
            "0.1A",
            "1OHM",
            "0.1W",
            "OFF"
        ]);

        public TaskCompletionSource FirstQueryStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseFirstQuery { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<string> Queries { get; } = [];

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
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
