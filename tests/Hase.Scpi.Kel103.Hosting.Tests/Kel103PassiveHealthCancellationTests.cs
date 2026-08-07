using System.Text;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Transport.Serial;

namespace Hase.Scpi.Kel103.Hosting.Tests;

public sealed class Kel103PassiveHealthCancellationTests
{
    [Fact]
    public async Task ProbeHealthAsync_OrderlyCancellationDoesNotProjectCommunicationFault()
    {
        var context = new RuntimeContext();
        var stream = new CancelableHealthStream();
        var factory = new Kel103PublishedAttachmentFactory(
            context,
            new SingleStreamFactory(stream));
        await using Kel103PublishedAttachment attachment = await factory.OpenAsync(
            new EndpointId("kel-test-01"),
            SupportedOptions());
        using var cancellation = new CancellationTokenSource();

        Task probe = attachment.ProbeHealthAsync(cancellation.Token);
        await stream.HealthReadStarted.Task;
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => probe);
        Assert.Equal(
            EndpointConnectionState.Ready,
            attachment.RuntimeEndpoint.ConnectionStatus.State);
    }

    private static SerialTransportOptions SupportedOptions() =>
        new(
            "TEST-PORT",
            115200,
            8,
            SerialParity.None,
            SerialStopBits.One,
            SerialHandshake.None);

    private sealed class SingleStreamFactory(ISerialByteStream stream)
        : ISerialByteStreamFactory
    {
        public ValueTask<ISerialByteStream> OpenAsync(
            SerialTransportOptions options,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(stream);
        }
    }

    private sealed class CancelableHealthStream : ISerialByteStream
    {
        private readonly Queue<byte[]> synchronizationResponses = new(
        [
            Encoding.ASCII.GetBytes(
                "RND 320-KEL103 V3.30 SN:REDACTED\n"),
            Encoding.ASCII.GetBytes("9.0000V\n"),
            Encoding.ASCII.GetBytes("0.1000A\n"),
            Encoding.ASCII.GetBytes("0.9000W\n")
        ]);

        public TaskCompletionSource HealthReadStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (synchronizationResponses.TryDequeue(out byte[]? response))
            {
                response.AsSpan().CopyTo(buffer.Span);
                return response.Length;
            }

            HealthReadStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }

        public ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
