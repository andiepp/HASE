using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Grpc.Net.Client;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Hosting;

/// <summary>
/// Owns one mutual-TLS gRPC channel and generated version-1 runtime-host
/// client for private-network access.
/// </summary>
public sealed class RuntimeHostPrivateNetworkGrpcClient
    : IDisposable
{
    private readonly SocketsHttpHandler handler;
    private readonly GrpcChannel channel;
    private readonly GrpcV1.RuntimeHostRemoteApi.RuntimeHostRemoteApiClient
        client;
    private bool disposed;

    private RuntimeHostPrivateNetworkGrpcClient(
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
    /// Creates a client from externally provisioned client and trusted server
    /// certificates.
    /// </summary>
    public static RuntimeHostPrivateNetworkGrpcClient Create(
        Uri address,
        X509Certificate2 clientCertificate,
        X509Certificate2 trustedServerCertificate)
    {
        ArgumentNullException.ThrowIfNull(
            address);
        ArgumentNullException.ThrowIfNull(
            clientCertificate);
        ArgumentNullException.ThrowIfNull(
            trustedServerCertificate);

        if (!string.Equals(
                address.Scheme,
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The private-network gRPC address must use HTTPS.",
                nameof(address));
        }

        if (!clientCertificate.HasPrivateKey)
        {
            throw new ArgumentException(
                "The private-network client certificate must have an "
                + "accessible private key.",
                nameof(clientCertificate));
        }

        var serverCertificateValidator =
            new RuntimeHostPinnedServerCertificateValidator(
                trustedServerCertificate);
        var handler =
            new SocketsHttpHandler();

        handler.SslOptions.EnabledSslProtocols =
            SslProtocols.Tls12
            | SslProtocols.Tls13;
        handler.SslOptions.LocalCertificateSelectionCallback =
            (_, _, _, _, _) =>
                clientCertificate;
        handler.SslOptions.RemoteCertificateValidationCallback =
            serverCertificateValidator.Validate;

        try
        {
            GrpcChannel channel =
                GrpcChannel.ForAddress(
                    address,
                    new GrpcChannelOptions
                    {
                        HttpHandler =
                            handler
                    });

            return new RuntimeHostPrivateNetworkGrpcClient(
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
