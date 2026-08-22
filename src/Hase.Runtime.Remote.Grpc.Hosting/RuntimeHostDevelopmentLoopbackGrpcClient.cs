using Grpc.Net.Client;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;
using MediaV1 = global::Hase.Runtime.Media.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Hosting;

/// <summary>
/// Owns one certificate-free plaintext HTTP/2 gRPC channel and generated
/// version-1 runtime-host client for the loopback development profile. Every
/// non-loopback runtime host requires the mutual-TLS private-network client.
/// </summary>
public sealed class RuntimeHostDevelopmentLoopbackGrpcClient
    : IDisposable
{
    private readonly SocketsHttpHandler handler;
    private readonly GrpcChannel channel;
    private readonly GrpcV1.RuntimeHostRemoteApi.RuntimeHostRemoteApiClient
        client;
    private readonly MediaV1.RuntimeHostMediaControl.RuntimeHostMediaControlClient
        mediaClient;
    private bool disposed;

    private RuntimeHostDevelopmentLoopbackGrpcClient(
        SocketsHttpHandler handler,
        GrpcChannel channel)
    {
        this.handler =
            handler;
        this.channel =
            channel;
        client =
            new GrpcV1.RuntimeHostRemoteApi.RuntimeHostRemoteApiClient(
                channel);
        mediaClient =
            new MediaV1.RuntimeHostMediaControl.RuntimeHostMediaControlClient(
                channel);
    }

    /// <summary>
    /// Gets the generated version-1 gRPC client.
    /// </summary>
    public GrpcV1.RuntimeHostRemoteApi.RuntimeHostRemoteApiClient Client
    {
        get
        {
            ObjectDisposedException.ThrowIf(
                disposed,
                this);

            return client;
        }
    }

    /// <summary>
    /// Gets the generated version-1 media-control gRPC client. The
    /// development runtime host registers no media service; calls fail with
    /// an unimplemented status.
    /// </summary>
    public MediaV1.RuntimeHostMediaControl.RuntimeHostMediaControlClient
        MediaClient
    {
        get
        {
            ObjectDisposedException.ThrowIf(
                disposed,
                this);

            return mediaClient;
        }
    }

    /// <summary>
    /// Creates a client from one validated development loopback
    /// configuration.
    /// </summary>
    public static RuntimeHostDevelopmentLoopbackGrpcClient Create(
        RuntimeHostDevelopmentLoopbackClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(
            options);

        var handler =
            new SocketsHttpHandler();

        try
        {
            GrpcChannel channel =
                GrpcChannel.ForAddress(
                    options.Address,
                    new GrpcChannelOptions
                    {
                        HttpHandler =
                            handler
                    });

            return new RuntimeHostDevelopmentLoopbackGrpcClient(
                handler,
                channel);
        }
        catch
        {
            handler.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed =
            true;

        channel.Dispose();
        handler.Dispose();
    }
}
