using System.Text;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Transport.Serial;

namespace Hase.Scpi.Kel103.Hosting.Tests;

public sealed class Kel103PublishedAttachmentHealthProbeTests
{
    [Fact]
    public async Task ProbeHealthAsync_ValidIdentityPreservesReadyStateAndPropertyCache()
    {
        var context = new RuntimeContext();
        var stream = new ScriptedSerialByteStream(
            IdentityResponse(),
            "9.0000V\n",
            "0.1000A\n",
            "0.9000W\n",
            IdentityResponse());
        var factory = new Kel103PublishedAttachmentFactory(
            context,
            new SingleStreamFactory(stream));
        await using Kel103PublishedAttachment attachment = await factory.OpenAsync(
            new EndpointId("kel-test-01"),
            SupportedOptions());
        object?[] cachedValues = CachedValues(attachment.RuntimeEndpoint);

        await attachment.ProbeHealthAsync();

        Assert.Equal(EndpointConnectionState.Ready, attachment.RuntimeEndpoint.ConnectionStatus.State);
        Assert.Equal(cachedValues, CachedValues(attachment.RuntimeEndpoint));
        Assert.Equal(5, stream.Writes.Count);
        Assert.Equal("*IDN?\r", stream.Writes[^1]);
        Assert.DoesNotContain(
            stream.Writes,
            command => !command.EndsWith("?\r", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProbeHealthAsync_InvalidIdentityProjectsSanitizedFaultAndPreservesCache()
    {
        var context = new RuntimeContext();
        var stream = new ScriptedSerialByteStream(
            IdentityResponse(),
            "9.0000V\n",
            "0.1000A\n",
            "0.9000W\n",
            "unexpected instrument\n");
        var factory = new Kel103PublishedAttachmentFactory(
            context,
            new SingleStreamFactory(stream),
            new FixedTimeProvider());
        await using Kel103PublishedAttachment attachment = await factory.OpenAsync(
            new EndpointId("kel-test-01"),
            SupportedOptions());
        object?[] cachedValues = CachedValues(attachment.RuntimeEndpoint);

        await Assert.ThrowsAsync<InvalidDataException>(() => attachment.ProbeHealthAsync());

        Assert.Equal(EndpointConnectionState.Faulted, attachment.RuntimeEndpoint.ConnectionStatus.State);
        Assert.Equal(
            "The KEL-103 passive health probe failed.",
            attachment.RuntimeEndpoint.ConnectionStatus.Detail);
        Assert.Equal(cachedValues, CachedValues(attachment.RuntimeEndpoint));
        Assert.DoesNotContain(
            "unexpected",
            attachment.RuntimeEndpoint.ConnectionStatus.Detail,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "SN:",
            attachment.RuntimeEndpoint.ConnectionStatus.Detail,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProbeHealthAsync_AfterDisposalDoesNotOpenOrUseTransport()
    {
        var context = new RuntimeContext();
        var stream = new ScriptedSerialByteStream(
            IdentityResponse(),
            "9.0000V\n",
            "0.1000A\n",
            "0.9000W\n");
        var transport = new SingleStreamFactory(stream);
        Kel103PublishedAttachment attachment = await new Kel103PublishedAttachmentFactory(
                context,
                transport)
            .OpenAsync(
                new EndpointId("kel-test-01"),
                SupportedOptions());
        await attachment.DisposeAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => attachment.ProbeHealthAsync());

        Assert.Equal(1, transport.OpenCount);
        Assert.Equal(4, stream.Writes.Count);
    }

    private static object?[] CachedValues(RuntimeEndpoint endpoint) =>
        endpoint.Instruments.Single().Properties
            .Select(property => property.CurrentValue!.Value)
            .ToArray();

    private static SerialTransportOptions SupportedOptions() =>
        new(
            "TEST-PORT",
            115200,
            8,
            SerialParity.None,
            SerialStopBits.One,
            SerialHandshake.None);

    private static string IdentityResponse() =>
        "RND 320-KEL103 V3.30 SN:REDACTED\n";

    private sealed class SingleStreamFactory(ISerialByteStream stream)
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
        private readonly Queue<byte[]> remaining = new(
            responses.Select(Encoding.ASCII.GetBytes));

        public List<string> Writes { get; } = [];

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

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(2026, 8, 7, 7, 0, 0, TimeSpan.Zero);
    }
}
