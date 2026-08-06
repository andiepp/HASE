using System.Text;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Hase.Scpi;
using Hase.Scpi.Kel103.Runtime;
using Hase.Transport.Serial;

namespace Hase.Scpi.Kel103.Hosting.Tests;

public sealed class Kel103PublishedConnectionSlotCommandTests
{
    [Fact]
    public async Task ExecuteAsync_ForwardsOnceAndReturnsAuthoritativeSuccess()
    {
        var session = new FakeSession("OFF", "OFF", "CV");
        await using Kel103PublishedConnectionSlot slot = CreateSlot(session);

        EndpointAttachmentCommandOperationResult result = await slot.ExecuteAsync(
            InstrumentId(),
            Kel103ModeSelectionMapping.ConstantVoltage.CommandPath,
            argument: null);

        Assert.True(result.IsSuccess);
        Assert.Equal([":INPut?", ":INPut?", ":FUNCtion?"], session.Queries);
        Assert.Equal([":FUNCtion CV"], session.Commands);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task ExecuteAsync_ForwardsOrdinaryInputControlOnce(int mappingIndex)
    {
        var session = mappingIndex == 0
            ? new FakeSession("OFF", "CC", "ON")
            : new FakeSession("OFF");
        await using Kel103PublishedConnectionSlot slot = CreateVersionFiveSlot(session);
        Kel103InputControlMapping mapping = Kel103InputControlMapping.All[mappingIndex];

        EndpointAttachmentCommandOperationResult result = await slot.ExecuteAsync(
            InstrumentId(),
            mapping.CommandPath,
            argument: null);

        Assert.True(result.IsSuccess);
        Assert.Equal([mapping.Command], session.Commands);
    }

    [Fact]
    public async Task ExecuteAsync_ForwardsTrueShortConfirmationUnchangedOnce()
    {
        var session = new FakeSession("OFF", "SHORt", "ON");
        await using Kel103PublishedConnectionSlot slot = CreateVersionFiveSlot(session);

        EndpointAttachmentCommandOperationResult result = await slot.ExecuteAsync(
            InstrumentId(),
            Kel103InputControlMapping.ShortCircuitActivate.CommandPath,
            argument: true);

        Assert.True(result.IsSuccess);
        Assert.Equal([":INPut?", ":FUNCtion?", ":INPut?"], session.Queries);
        Assert.Equal([":INPut ON"], session.Commands);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    [InlineData("true")]
    [InlineData(1)]
    public async Task ExecuteAsync_NormalizesInvalidShortConfirmationWithoutScpi(
        object? argument)
    {
        var session = new FakeSession();
        await using Kel103PublishedConnectionSlot slot = CreateVersionFiveSlot(session);

        EndpointAttachmentCommandOperationResult result = await slot.ExecuteAsync(
            InstrumentId(),
            Kel103InputControlMapping.ShortCircuitActivate.CommandPath,
            argument);

        Assert.Equal(
            EndpointAttachmentCommandOperationStatus.ArgumentNotSupported,
            result.Status);
        Assert.Empty(session.Queries);
        Assert.Empty(session.Commands);
    }

    [Fact]
    public async Task ExecuteAsync_SerializesAConcurrentPropertyRead()
    {
        var session = new FakeSession("OFF", "OFF", "CV", "CV")
        {
            BlockFirstQuery = true
        };
        await using Kel103PublishedConnectionSlot slot = CreateSlot(session);

        Task<EndpointAttachmentCommandOperationResult> command = slot.ExecuteAsync(
            InstrumentId(),
            Kel103ModeSelectionMapping.ConstantVoltage.CommandPath,
            argument: null);
        await session.FirstQueryEntered.Task;
        Task<EndpointAttachmentPropertyOperationResult> read = slot.ReadAsync(
            InstrumentId(),
            Kel103OperatingModeMapping.PropertyId);

        await Task.Yield();
        Assert.False(read.IsCompleted);
        Assert.Equal([":INPut?"], session.Queries);
        session.ReleaseFirstQuery.SetResult(true);
        await Task.WhenAll(command, read);

        Assert.Equal(
            [":INPut?", ":INPut?", ":FUNCtion?", ":FUNCtion?"],
            session.Queries);
        Assert.Equal([":FUNCtion CV"], session.Commands);
    }

    [Fact]
    public async Task ExecuteAsync_CancellationWhileWaitingDoesNotEnterConnection()
    {
        var session = new FakeSession("OFF", "OFF", "CV")
        {
            BlockFirstQuery = true
        };
        await using Kel103PublishedConnectionSlot slot = CreateSlot(session);
        Task<EndpointAttachmentCommandOperationResult> first = slot.ExecuteAsync(
            InstrumentId(),
            Kel103ModeSelectionMapping.ConstantVoltage.CommandPath,
            argument: null);
        await session.FirstQueryEntered.Task;
        using var cancellation = new CancellationTokenSource();
        Task<EndpointAttachmentCommandOperationResult> waiting = slot.ExecuteAsync(
            InstrumentId(),
            Kel103ModeSelectionMapping.ConstantResistance.CommandPath,
            argument: null,
            cancellationToken: cancellation.Token);

        cancellation.Cancel();
        Exception? cancellationFailure = await Record.ExceptionAsync(() => waiting);
        session.ReleaseFirstQuery.SetResult(true);
        await first;

        Assert.IsAssignableFrom<OperationCanceledException>(cancellationFailure);
        Assert.Equal([":FUNCtion CV"], session.Commands);
    }

    [Fact]
    public async Task ExecuteAsync_AfterDisposalReturnsSanitizedUnavailable()
    {
        var session = new FakeSession();
        Kel103PublishedConnectionSlot slot = CreateSlot(session);
        await slot.DisposeAsync();

        EndpointAttachmentCommandOperationResult result = await slot.ExecuteAsync(
            InstrumentId(),
            Kel103ModeSelectionMapping.ShortCircuit.CommandPath,
            argument: null);

        Assert.Equal(EndpointAttachmentCommandOperationStatus.Unavailable, result.Status);
        Assert.DoesNotContain("SHORT", result.Diagnostic ?? string.Empty, StringComparison.Ordinal);
        Assert.Empty(session.Queries);
        Assert.Empty(session.Commands);
    }

    [Fact]
    public async Task ExecuteAsync_AfterReplacementUsesOnlyReplacementConnection()
    {
        RuntimeEndpoint endpoint = CreateVersionFourEndpoint();
        var initialSession = new FakeSession();
        Kel103OperationalConnection initialConnection = CreateConnection(
            endpoint,
            initialSession);
        var replacementStream = new ScriptedSerialByteStream(
            ReplacementResponses(includeCommand: true));
        var replacementFactory = new SingleSerialFactory(replacementStream);
        var connectionFactory = new Kel103OperationalConnectionFactory(
            endpoint.Context,
            replacementFactory,
            new FixedTimeProvider());
        await using var slot = new Kel103PublishedConnectionSlot(
            initialConnection,
            connectionFactory,
            new FixedTimeProvider());
        endpoint.UpdateConnectionStatus(
            new EndpointConnectionStatus(EndpointConnectionState.Faulted));

        await slot.ReplaceAsync(SupportedOptions());
        EndpointAttachmentCommandOperationResult result = await slot.ExecuteAsync(
            InstrumentId(),
            Kel103ModeSelectionMapping.ConstantVoltage.CommandPath,
            argument: null);

        Assert.True(result.IsSuccess);
        Assert.Equal(ScpiTextSessionState.Disposed, initialSession.State);
        Assert.Equal(1, replacementFactory.OpenCount);
        Assert.Equal(1, replacementStream.Writes.Count(
            value => value == ":FUNCtion CV\r"));
    }

    [Fact]
    public async Task ReplaceAsync_WaitsForInFlightCommandWithoutReplay()
    {
        RuntimeEndpoint endpoint = CreateVersionFourEndpoint();
        endpoint.UpdateConnectionStatus(
            new EndpointConnectionStatus(EndpointConnectionState.Ready));
        var initialSession = new FakeSession("OFF", "OFF", "CV")
        {
            BlockFirstQuery = true
        };
        Kel103OperationalConnection initialConnection = CreateConnection(
            endpoint,
            initialSession);
        var replacementStream = new ScriptedSerialByteStream(
            ReplacementResponses(includeCommand: false));
        var replacementFactory = new SingleSerialFactory(replacementStream);
        var connectionFactory = new Kel103OperationalConnectionFactory(
            endpoint.Context,
            replacementFactory,
            new FixedTimeProvider());
        await using var slot = new Kel103PublishedConnectionSlot(
            initialConnection,
            connectionFactory,
            new FixedTimeProvider());

        Task<EndpointAttachmentCommandOperationResult> command = slot.ExecuteAsync(
            InstrumentId(),
            Kel103ModeSelectionMapping.ConstantVoltage.CommandPath,
            argument: null);
        await initialSession.FirstQueryEntered.Task;
        endpoint.UpdateConnectionStatus(
            new EndpointConnectionStatus(EndpointConnectionState.Faulted));
        Task replacement = slot.ReplaceAsync(SupportedOptions());

        await Task.Yield();
        Assert.False(replacement.IsCompleted);
        Assert.Equal(0, replacementFactory.OpenCount);
        initialSession.ReleaseFirstQuery.SetResult(true);
        Assert.True((await command).IsSuccess);
        await replacement;

        Assert.Equal([":FUNCtion CV"], initialSession.Commands);
        Assert.Equal(ScpiTextSessionState.Disposed, initialSession.State);
        Assert.Equal(1, replacementFactory.OpenCount);
        Assert.DoesNotContain(":FUNCtion CV\r", replacementStream.Writes);
        Assert.Equal(EndpointConnectionState.Ready, endpoint.ConnectionStatus.State);
    }

    private static Kel103PublishedConnectionSlot CreateSlot(FakeSession session)
    {
        RuntimeEndpoint endpoint = CreateVersionFourEndpoint();
        return new Kel103PublishedConnectionSlot(
            CreateConnection(endpoint, session),
            new Kel103OperationalConnectionFactory(
                endpoint.Context,
                new RejectingSerialFactory(),
                new FixedTimeProvider()),
            new FixedTimeProvider());
    }

    private static Kel103PublishedConnectionSlot CreateVersionFiveSlot(
        FakeSession session)
    {
        RuntimeEndpoint endpoint = CreateVersionFiveEndpoint();
        return new Kel103PublishedConnectionSlot(
            CreateConnection(endpoint, session),
            new Kel103OperationalConnectionFactory(
                endpoint.Context,
                new RejectingSerialFactory(),
                new FixedTimeProvider()),
            new FixedTimeProvider());
    }

    private static Kel103OperationalConnection CreateConnection(
        RuntimeEndpoint endpoint,
        FakeSession session)
    {
        var runtimeAdapter = new Kel103RuntimeEndpointAdapter(
            new Kel103ReadOnlySessionAdapter(session, new FixedTimeProvider()),
            endpoint,
            new FixedTimeProvider());
        return new Kel103OperationalConnection(
            runtimeAdapter,
            new Kel103EndpointAttachmentPropertyOperations(
                runtimeAdapter,
                new FixedTimeProvider()));
    }

    private static RuntimeEndpoint CreateVersionFourEndpoint() =>
        new RuntimeContext().CreateEndpoint(
            Kel103ControlledSetpointDefinition.EndpointDefinition.Materialize(
                new EndpointId("kel-test-01")));

    private static RuntimeEndpoint CreateVersionFiveEndpoint() =>
        new RuntimeContext().CreateEndpoint(
            Kel103ControlledInputDefinition.EndpointDefinition.Materialize(
                new EndpointId("kel-test-01")));

    private static InstrumentId InstrumentId() => new("electronic-load-01");

    private static SerialTransportOptions SupportedOptions() =>
        new(
            "TEST-PORT",
            115200,
            8,
            SerialParity.None,
            SerialStopBits.One,
            SerialHandshake.None);

    private static string[] ReplacementResponses(bool includeCommand)
    {
        var responses = new List<string>
        {
            "RND 320-KEL103 V3.30 SN:REDACTED\n",
            "0.0000V\n",
            "0.0000A\n",
            "0.0000W\n",
            "CC\n",
            "OFF\n",
            "0.1000V\n",
            "0.1000A\n",
            "0.1000OHM\n",
            "0.1000W\n"
        };
        if (includeCommand)
        {
            responses.AddRange(["OFF\n", "OFF\n", "CV\n"]);
        }

        return responses.ToArray();
    }

    private sealed class FakeSession(params string[] responses) : IScpiTextSession
    {
        private readonly Queue<string> pending = new(responses);

        public bool BlockFirstQuery { get; init; }
        public TaskCompletionSource<bool> FirstQueryEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> ReleaseFirstQuery { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<string> Queries { get; } = [];
        public List<string> Commands { get; } = [];
        public ScpiTextSessionState State { get; private set; } =
            ScpiTextSessionState.Open;

        public async Task<string> QueryAsync(
            string query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Queries.Add(query);
            if (BlockFirstQuery && Queries.Count == 1)
            {
                FirstQueryEntered.SetResult(true);
                await ReleaseFirstQuery.Task.WaitAsync(cancellationToken);
            }

            return pending.Dequeue();
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
            State = ScpiTextSessionState.Disposed;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RejectingSerialFactory : ISerialByteStreamFactory
    {
        public ValueTask<ISerialByteStream> OpenAsync(
            SerialTransportOptions options,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "A replacement connection was not expected.");
    }

    private sealed class SingleSerialFactory(ISerialByteStream stream)
        : ISerialByteStreamFactory
    {
        public int OpenCount { get; private set; }

        public ValueTask<ISerialByteStream> OpenAsync(
            SerialTransportOptions options,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenCount++;
            return ValueTask.FromResult(stream);
        }
    }

    private sealed class ScriptedSerialByteStream(params string[] responses)
        : ISerialByteStream
    {
        private readonly Queue<byte[]> pending = new(
            responses.Select(Encoding.ASCII.GetBytes));

        public List<string> Writes { get; } = [];

        public ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte[] response = pending.Dequeue();
            response.AsSpan().CopyTo(buffer.Span);
            return ValueTask.FromResult(response.Length);
        }

        public ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Writes.Add(Encoding.ASCII.GetString(buffer.Span));
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(2026, 8, 6, 0, 0, 0, TimeSpan.Zero);
    }
}
