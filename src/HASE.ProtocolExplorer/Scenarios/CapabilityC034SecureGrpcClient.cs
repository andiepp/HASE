using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Grpc.Core;
using Grpc.Net.Client;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Owns one authenticated gRPC client used by C-034 observation validation.
/// Each streaming call returned by <see cref="Observe"/> remains caller-owned.
/// </summary>
internal sealed class CapabilityC034SecureGrpcClient
    : IDisposable
{
    private readonly SocketsHttpHandler handler;
    private readonly GrpcChannel channel;
    private readonly GrpcV1.RuntimeHostRemoteApi.RuntimeHostRemoteApiClient
        client;
    private bool disposed;

    private CapabilityC034SecureGrpcClient(
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
    /// Creates a client that presents the enrolled validation certificate and
    /// accepts only the expected validation server certificate.
    /// </summary>
    public static CapabilityC034SecureGrpcClient Create(
        Uri address,
        X509Certificate2 clientCertificate,
        X509Certificate2 expectedServerCertificate)
    {
        ArgumentNullException.ThrowIfNull(
            address);
        ArgumentNullException.ThrowIfNull(
            clientCertificate);
        ArgumentNullException.ThrowIfNull(
            expectedServerCertificate);

        if (!string.Equals(
                Uri.UriSchemeHttps,
                address.Scheme,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The C-034 gRPC address must use HTTPS.",
                nameof(address));
        }

        string expectedServerThumbprint =
            expectedServerCertificate.Thumbprint;
        var handler =
            new SocketsHttpHandler();

        handler.SslOptions.EnabledSslProtocols =
            SslProtocols.Tls12
            | SslProtocols.Tls13;
        handler.SslOptions.LocalCertificateSelectionCallback =
            (_, _, _, _, _) =>
                clientCertificate;
        handler.SslOptions.RemoteCertificateValidationCallback =
            (
                _,
                certificate,
                _,
                sslPolicyErrors) =>
                sslPolicyErrors
                    is SslPolicyErrors.None
                        or SslPolicyErrors.RemoteCertificateChainErrors
                && certificate is not null
                && string.Equals(
                    expectedServerThumbprint,
                    certificate.GetCertHashString(),
                    StringComparison.OrdinalIgnoreCase);

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

            return new CapabilityC034SecureGrpcClient(
                handler,
                channel);
        }
        catch
        {
            handler.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Opens one authenticated server-streaming observation call.
    /// </summary>
    public AsyncServerStreamingCall<GrpcV1.ObserveResponse> Observe(
        DateTime deadlineUtc,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(
            disposed,
            this);

        return client.Observe(
            new GrpcV1.ObserveRequest(),
            deadline:
                deadlineUtc,
            cancellationToken:
                cancellationToken);
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
