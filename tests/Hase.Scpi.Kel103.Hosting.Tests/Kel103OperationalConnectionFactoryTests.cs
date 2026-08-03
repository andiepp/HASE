using System.Text;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Hase.Transport.Serial;

namespace Hase.Scpi.Kel103.Hosting.Tests;

public sealed class Kel103OperationalConnectionFactoryTests
{
    [Fact]
    public async Task OpenAsync_SynchronizesOneStagedEndpointUsingFixedQueryOrder()
    {
        var context = new RuntimeContext();
        var stream = SuccessfulStream();
        var transport = new RecordingFactory(stream);
        var factory = new Kel103OperationalConnectionFactory(
            context,
            transport,
            new FixedTimeProvider());
        var endpointId = new EndpointId("kel-test-01");
        var options = SupportedOptions();

        await using Kel103OperationalConnection connection = await factory.OpenAsync(
            endpointId,
            options);

        Assert.Same(options, transport.Options);
        Assert.Equal(endpointId, connection.RuntimeEndpoint.Descriptor.Id);
        Assert.Equal(EndpointConnectionState.Disconnected, connection.RuntimeEndpoint.ConnectionStatus.State);
        Assert.Empty(context.Endpoints);
        Assert.Equal(
            ["*IDN?\r", ":MEASure:VOLTage?\r", ":MEASure:CURRent?\r", ":MEASure:POWer?\r"],
            stream.Writes);
        Assert.Equal(
            ["KEL-103", "V3.30", 9.8864m, 0.1000m, 0.9893m],
            connection.RuntimeEndpoint.Instruments.Single().Properties
                .Select(property => property.CurrentValue!.Value).ToArray());
        Assert.All(
            connection.RuntimeEndpoint.Instruments.Single().Properties,
            property => Assert.Equal(FixedTimeProvider.Timestamp, property.CurrentValue!.TimestampUtc));
    }

    [Theory]
    [InlineData(9600, 8, SerialParity.None, SerialStopBits.One, SerialHandshake.None)]
    [InlineData(115200, 7, SerialParity.None, SerialStopBits.One, SerialHandshake.None)]
    [InlineData(115200, 8, SerialParity.Even, SerialStopBits.One, SerialHandshake.None)]
    [InlineData(115200, 8, SerialParity.None, SerialStopBits.Two, SerialHandshake.None)]
    [InlineData(115200, 8, SerialParity.None, SerialStopBits.One, SerialHandshake.RequestToSend)]
    public async Task OpenAsync_RejectsUnsupportedProfileBeforeOpening(
        int baudRate,
        int dataBits,
        SerialParity parity,
        SerialStopBits stopBits,
        SerialHandshake handshake)
    {
        var transport = new RecordingFactory(SuccessfulStream());
        var factory = new Kel103OperationalConnectionFactory(new RuntimeContext(), transport);
        var options = new SerialTransportOptions(
            "TEST-PORT", baudRate, dataBits, parity, stopBits, handshake);

        await Assert.ThrowsAsync<ArgumentException>(() => factory.OpenAsync(
            new EndpointId("kel-test-01"), options));

        Assert.Equal(0, transport.OpenCount);
    }

    [Fact]
    public async Task OpenAsync_PreCanceledOperationDoesNotOpenTransport()
    {
        var transport = new RecordingFactory(SuccessfulStream());
        var factory = new Kel103OperationalConnectionFactory(new RuntimeContext(), transport);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => factory.OpenAsync(
            new EndpointId("kel-test-01"), SupportedOptions(), cancellation.Token));

        Assert.Equal(0, transport.OpenCount);
    }

    [Fact]
    public async Task OpenAsync_InvalidIdentityDisposesOpenedStreamWithoutRetry()
    {
        var stream = new ScriptedSerialByteStream("unexpected\n");
        var transport = new RecordingFactory(stream);
        var factory = new Kel103OperationalConnectionFactory(new RuntimeContext(), transport);

        await Assert.ThrowsAsync<InvalidDataException>(() => factory.OpenAsync(
            new EndpointId("kel-test-01"), SupportedOptions()));

        Assert.Equal(1, transport.OpenCount);
        Assert.Equal(1, stream.DisposeCount);
        Assert.Equal(["*IDN?\r"], stream.Writes);
    }

    [Fact]
    public async Task OpenAsync_MeasurementFailureDoesNotExposePartialEndpointOrRetry()
    {
        var context = new RuntimeContext();
        var stream = new ScriptedSerialByteStream(
            IdentityResponse(),
            "9.8864V\n",
            "invalid\n");
        var transport = new RecordingFactory(stream);
        var factory = new Kel103OperationalConnectionFactory(context, transport);

        await Assert.ThrowsAsync<InvalidDataException>(() => factory.OpenAsync(
            new EndpointId("kel-test-01"), SupportedOptions()));

        Assert.Empty(context.Endpoints);
        Assert.Equal(1, transport.OpenCount);
        Assert.Equal(1, stream.DisposeCount);
        Assert.Equal(3, stream.Writes.Count);
    }

    [Fact]
    public async Task PropertyOperations_ReadAuthoritativelyAndWriteWithoutIo()
    {
        var stream = new ScriptedSerialByteStream(
            IdentityResponse(), "9.8864V\n", "0.1000A\n", "0.9893W\n", "10.0000V\n");
        var factory = new Kel103OperationalConnectionFactory(
            new RuntimeContext(),
            new RecordingFactory(stream),
            new FixedTimeProvider());
        await using Kel103OperationalConnection connection = await factory.OpenAsync(
            new EndpointId("kel-test-01"), SupportedOptions());
        var instrumentId = new InstrumentId("electronic-load-01");

        EndpointAttachmentPropertyOperationResult read = await connection.PropertyOperations.ReadAsync(
            instrumentId,
            new PropertyId("measured-voltage"));
        int writesAfterRead = stream.Writes.Count;
        EndpointAttachmentPropertyOperationResult write = await connection.PropertyOperations.WriteAsync(
            instrumentId,
            new PropertyId("measured-voltage"),
            12m);

        Assert.True(read.IsSuccess);
        Assert.Equal(10.0000m, read.ConfirmedValue!.Value);
        Assert.Equal(EndpointAttachmentPropertyOperationStatus.NotSupported, write.Status);
        Assert.Equal(writesAfterRead, stream.Writes.Count);
    }

    [Fact]
    public async Task DisposeAsync_IsIdempotentAndClosesUnderlyingStreamOnce()
    {
        var stream = SuccessfulStream();
        var factory = new Kel103OperationalConnectionFactory(
            new RuntimeContext(),
            new RecordingFactory(stream));
        Kel103OperationalConnection connection = await factory.OpenAsync(
            new EndpointId("kel-test-01"), SupportedOptions());

        await Task.WhenAll(
            connection.DisposeAsync().AsTask(),
            connection.DisposeAsync().AsTask(),
            connection.DisposeAsync().AsTask());

        Assert.Equal(1, stream.DisposeCount);
    }

    [Fact]
    public void Constructor_RejectsNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new Kel103OperationalConnectionFactory(null!, new RecordingFactory(SuccessfulStream())));
        Assert.Throws<ArgumentNullException>(() =>
            new Kel103OperationalConnectionFactory(new RuntimeContext(), null!));
    }

    [Fact]
    public async Task OpenAsync_RejectsNullArgumentsBeforeOpening()
    {
        var transport = new RecordingFactory(SuccessfulStream());
        var factory = new Kel103OperationalConnectionFactory(new RuntimeContext(), transport);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            factory.OpenAsync(null!, SupportedOptions()));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            factory.OpenAsync(new EndpointId("kel-test-01"), null!));
        Assert.Equal(0, transport.OpenCount);
    }

    private static SerialTransportOptions SupportedOptions() =>
        new("TEST-PORT", 115200, 8, SerialParity.None, SerialStopBits.One, SerialHandshake.None);

    private static ScriptedSerialByteStream SuccessfulStream() => new(
        IdentityResponse(),
        "9.8864V\n",
        "0.1000A\n",
        "0.9893W\n");

    private static string IdentityResponse() => "RND 320-KEL103 V3.30 SN:REDACTED\n";

    private sealed class RecordingFactory(ISerialByteStream stream) : ISerialByteStreamFactory
    {
        public int OpenCount { get; private set; }
        public SerialTransportOptions? Options { get; private set; }

        public ValueTask<ISerialByteStream> OpenAsync(
            SerialTransportOptions options,
            CancellationToken cancellationToken = default)
        {
            OpenCount++;
            Options = options;
            return ValueTask.FromResult(stream);
        }
    }

    private sealed class ScriptedSerialByteStream(params string[] responses) : ISerialByteStream
    {
        private readonly Queue<byte[]> remaining = new(
            responses.Select(Encoding.ASCII.GetBytes));

        public List<string> Writes { get; } = [];
        public int DisposeCount { get; private set; }

        public ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!remaining.TryDequeue(out byte[]? response))
            {
                return ValueTask.FromResult(0);
            }

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

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public static DateTimeOffset Timestamp { get; } =
            new(2026, 8, 3, 18, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Timestamp;
    }
}
