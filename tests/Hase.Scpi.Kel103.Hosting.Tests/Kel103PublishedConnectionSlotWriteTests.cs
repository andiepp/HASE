using System.Text;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Hase.Scpi;
using Hase.Scpi.Kel103.Runtime;
using Hase.Transport.Serial;

namespace Hase.Scpi.Kel103.Hosting.Tests;

public sealed class Kel103PublishedConnectionSlotWriteTests
{
    [Fact]
    public async Task WriteAsync_ForwardsOnceAndReturnsAuthoritativeConfirmation()
    {
        var session = new FakeSession("OFF", "0.25A", "CC");
        await using Kel103PublishedConnectionSlot slot = CreateSlot(
            CreateVersionFourEndpoint(),
            session);

        EndpointAttachmentPropertyOperationResult result = await slot.WriteAsync(
            InstrumentId(),
            Kel103SetpointMapping.Current.PropertyId,
            0.25m);

        Assert.True(result.IsSuccess);
        Assert.Equal(0.25m, result.ConfirmedValue!.Value);
        Assert.Equal([":INPut?", ":CURRent?", ":FUNCtion?"], session.Queries);
        Assert.Equal([":CURRent 0.25A"], session.Commands);
    }

    [Fact]
    public async Task WriteAsync_VersionTwoRemainsUnsupportedWithoutScpiUse()
    {
        var session = new FakeSession();
        await using Kel103PublishedConnectionSlot slot = CreateSlot(
            CreateVersionTwoEndpoint(),
            session);

        EndpointAttachmentPropertyOperationResult result = await slot.WriteAsync(
            InstrumentId(),
            Kel103SetpointMapping.Current.PropertyId,
            0.25m);

        Assert.Equal(EndpointAttachmentPropertyOperationStatus.NotSupported, result.Status);
        Assert.Empty(session.Queries);
        Assert.Empty(session.Commands);
    }

    [Theory]
    [InlineData(31.0)]
    [InlineData("invalid")]
    public async Task WriteAsync_InvalidRequestPreservesFailureWithoutScpiUse(object requestedValue)
    {
        var session = new FakeSession();
        await using Kel103PublishedConnectionSlot slot = CreateSlot(
            CreateVersionFourEndpoint(),
            session);

        EndpointAttachmentPropertyOperationResult result = await slot.WriteAsync(
            InstrumentId(),
            Kel103SetpointMapping.Current.PropertyId,
            requestedValue);

        Assert.Equal(EndpointAttachmentPropertyOperationStatus.Failure, result.Status);
        Assert.Empty(session.Queries);
        Assert.Empty(session.Commands);
    }

    [Fact]
    public async Task WriteAsync_InputOnPreservesUnavailableAndReadyState()
    {
        RuntimeEndpoint endpoint = CreateVersionFourEndpoint();
        endpoint.UpdateConnectionStatus(new EndpointConnectionStatus(EndpointConnectionState.Ready));
        var session = new FakeSession("ON");
        await using Kel103PublishedConnectionSlot slot = CreateSlot(endpoint, session);

        EndpointAttachmentPropertyOperationResult result = await slot.WriteAsync(
            InstrumentId(),
            Kel103SetpointMapping.Current.PropertyId,
            0.25m);

        Assert.Equal(EndpointAttachmentPropertyOperationStatus.Unavailable, result.Status);
        Assert.Equal(EndpointConnectionState.Ready, endpoint.ConnectionStatus.State);
        Assert.Equal([":INPut?"], session.Queries);
        Assert.Empty(session.Commands);
    }

    [Fact]
    public async Task WriteAsync_UncertainReadbackPreservesUnavailableAndProjectsFault()
    {
        RuntimeEndpoint endpoint = CreateVersionFourEndpoint();
        endpoint.UpdateConnectionStatus(new EndpointConnectionStatus(EndpointConnectionState.Ready));
        var session = new FakeSession("OFF", "0.20A", "CC");
        await using Kel103PublishedConnectionSlot slot = CreateSlot(endpoint, session);

        EndpointAttachmentPropertyOperationResult result = await slot.WriteAsync(
            InstrumentId(),
            Kel103SetpointMapping.Current.PropertyId,
            0.25m);

        Assert.Equal(EndpointAttachmentPropertyOperationStatus.Unavailable, result.Status);
        Assert.Equal(EndpointConnectionState.Faulted, endpoint.ConnectionStatus.State);
        Assert.Equal([":CURRent 0.25A"], session.Commands);
    }

    [Fact]
    public async Task WriteAsync_PreCancellationDoesNotEnterTheConnection()
    {
        var session = new FakeSession();
        await using Kel103PublishedConnectionSlot slot = CreateSlot(
            CreateVersionFourEndpoint(),
            session);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => slot.WriteAsync(
            InstrumentId(),
            Kel103SetpointMapping.Current.PropertyId,
            0.25m,
            cancellation.Token));

        Assert.Empty(session.Queries);
        Assert.Empty(session.Commands);
    }

    [Fact]
    public async Task WriteAsync_SerializesAConcurrentRead()
    {
        var session = new FakeSession("OFF", "0.25A", "CC", "CC")
        {
            BlockFirstQuery = true
        };
        await using Kel103PublishedConnectionSlot slot = CreateSlot(
            CreateVersionFourEndpoint(),
            session);

        Task<EndpointAttachmentPropertyOperationResult> write = slot.WriteAsync(
            InstrumentId(),
            Kel103SetpointMapping.Current.PropertyId,
            0.25m);
        await session.FirstQueryEntered.Task;
        Task<EndpointAttachmentPropertyOperationResult> read = slot.ReadAsync(
            InstrumentId(),
            Kel103OperatingModeMapping.PropertyId);

        await Task.Yield();
        bool readCompletedBeforeRelease = read.IsCompleted;
        string[] queriesBeforeRelease = session.Queries.ToArray();
        session.ReleaseFirstQuery.SetResult(true);
        await Task.WhenAll(write, read);

        Assert.False(readCompletedBeforeRelease);
        Assert.Equal([":INPut?"], queriesBeforeRelease);
        Assert.Equal([":INPut?", ":CURRent?", ":FUNCtion?", ":FUNCtion?"], session.Queries);
        Assert.Equal([":CURRent 0.25A"], session.Commands);
    }

    [Fact]
    public async Task WriteAsync_CancellationWhileWaitingDoesNotEnterTheConnection()
    {
        var session = new FakeSession("OFF", "0.25A", "CC")
        {
            BlockFirstQuery = true
        };
        await using Kel103PublishedConnectionSlot slot = CreateSlot(
            CreateVersionFourEndpoint(),
            session);
        Task<EndpointAttachmentPropertyOperationResult> first = slot.WriteAsync(
            InstrumentId(),
            Kel103SetpointMapping.Current.PropertyId,
            0.25m);
        await session.FirstQueryEntered.Task;
        using var cancellation = new CancellationTokenSource();
        Task<EndpointAttachmentPropertyOperationResult> waiting = slot.WriteAsync(
            InstrumentId(),
            Kel103SetpointMapping.Current.PropertyId,
            0.30m,
            cancellation.Token);

        cancellation.Cancel();
        Exception? cancellationFailure = await Record.ExceptionAsync(() => waiting);
        string[] queriesBeforeRelease = session.Queries.ToArray();
        session.ReleaseFirstQuery.SetResult(true);
        await first;

        Assert.IsAssignableFrom<OperationCanceledException>(cancellationFailure);
        Assert.Equal([":INPut?"], queriesBeforeRelease);
        Assert.Equal([":CURRent 0.25A"], session.Commands);
    }

    [Fact]
    public async Task WriteAsync_AfterDisposalReturnsSanitizedUnavailable()
    {
        var session = new FakeSession();
        Kel103PublishedConnectionSlot slot = CreateSlot(
            CreateVersionFourEndpoint(),
            session);
        await slot.DisposeAsync();

        EndpointAttachmentPropertyOperationResult result = await slot.WriteAsync(
            InstrumentId(),
            Kel103SetpointMapping.Current.PropertyId,
            0.25m);

        Assert.Equal(EndpointAttachmentPropertyOperationStatus.Unavailable, result.Status);
        Assert.Null(result.ConfirmedValue);
        Assert.DoesNotContain("0.25", result.Diagnostic ?? string.Empty, StringComparison.Ordinal);
        Assert.Empty(session.Queries);
        Assert.Empty(session.Commands);
    }

    [Fact]
    public async Task WriteAsync_AfterReplacementUsesOnlyTheReplacementConnection()
    {
        RuntimeEndpoint endpoint = CreateVersionFourEndpoint();
        var initialSession = new FakeSession();
        Kel103OperationalConnection initialConnection = CreateConnection(endpoint, initialSession);
        var replacementStream = new ScriptedSerialByteStream(
            "RND 320-KEL103 V3.30 SN:REDACTED\n",
            "0.0000V\n",
            "0.0000A\n",
            "0.0000W\n",
            "CC\n",
            "OFF\n",
            "0.1000V\n",
            "0.1000A\n",
            "0.1000OHM\n",
            "0.1000W\n",
            "OFF\n",
            "0.25A\n",
            "CC\n");
        var connectionFactory = new Kel103OperationalConnectionFactory(
            endpoint.Context,
            new SingleSerialFactory(replacementStream),
            new FixedTimeProvider());
        await using var slot = new Kel103PublishedConnectionSlot(
            initialConnection,
            connectionFactory,
            new FixedTimeProvider());
        endpoint.UpdateConnectionStatus(new EndpointConnectionStatus(EndpointConnectionState.Faulted));

        await slot.ReplaceAsync(SupportedOptions());
        EndpointAttachmentPropertyOperationResult result = await slot.WriteAsync(
            InstrumentId(),
            Kel103SetpointMapping.Current.PropertyId,
            0.25m);

        Assert.True(result.IsSuccess);
        Assert.Equal(0.25m, result.ConfirmedValue!.Value);
        Assert.Equal(ScpiTextSessionState.Disposed, initialSession.State);
        Assert.Equal(EndpointConnectionState.Ready, endpoint.ConnectionStatus.State);
        Assert.Equal(1, replacementStream.Writes.Count(
            value => value == ":CURRent 0.25A\r"));
    }

    private static Kel103PublishedConnectionSlot CreateSlot(
        RuntimeEndpoint endpoint,
        FakeSession session)
    {
        Kel103OperationalConnection connection = CreateConnection(endpoint, session);
        var connectionFactory = new Kel103OperationalConnectionFactory(
            endpoint.Context,
            new RejectingSerialFactory(),
            new FixedTimeProvider());
        return new Kel103PublishedConnectionSlot(
            connection,
            connectionFactory,
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

    private static RuntimeEndpoint CreateVersionTwoEndpoint() =>
        new RuntimeContext().CreateEndpoint(
            Kel103ReadOnlyMeasurementDefinition.EndpointDefinition.Materialize(
                new EndpointId("kel-test-01")));

    private static RuntimeEndpoint CreateVersionFourEndpoint() =>
        new RuntimeContext().CreateEndpoint(
            Kel103ControlledSetpointDefinition.EndpointDefinition.Materialize(
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
        public ScpiTextSessionState State { get; private set; } = ScpiTextSessionState.Open;

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
            throw new InvalidOperationException("A replacement connection was not expected.");
    }

    private sealed class SingleSerialFactory(ISerialByteStream stream)
        : ISerialByteStreamFactory
    {
        private bool opened;

        public ValueTask<ISerialByteStream> OpenAsync(
            SerialTransportOptions options,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (opened)
            {
                throw new InvalidOperationException("The replacement stream was already opened.");
            }

            opened = true;
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
            new(2026, 8, 4, 18, 0, 0, TimeSpan.Zero);
    }
}
