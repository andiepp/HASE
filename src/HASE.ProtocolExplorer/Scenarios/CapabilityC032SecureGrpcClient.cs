using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Grpc.Net.Client;
using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Owns the authenticated gRPC client used by one C-032 validation run.
/// </summary>
internal sealed class CapabilityC032SecureGrpcClient
    : IDisposable
{
    private readonly HttpClientHandler handler;
    private readonly GrpcChannel channel;
    private readonly GrpcV1.RuntimeHostRemoteApi.RuntimeHostRemoteApiClient
        client;
    private bool disposed;

    private CapabilityC032SecureGrpcClient(
        HttpClientHandler handler,
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
    /// Creates a client that presents the enrolled certificate and accepts only
    /// the expected validation server certificate.
    /// </summary>
    public static CapabilityC032SecureGrpcClient Create(
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
                "The C-032 gRPC address must use HTTPS.",
                nameof(address));
        }

        string expectedServerThumbprint =
            expectedServerCertificate.Thumbprint;

        var handler =
            new HttpClientHandler
            {
                ClientCertificateOptions =
                    ClientCertificateOption.Manual,
                ServerCertificateCustomValidationCallback =
                    (
                        _,
                        certificate,
                        _,
                        sslPolicyErrors) =>
                        sslPolicyErrors
                            is SslPolicyErrors.None
                                or SslPolicyErrors
                                    .RemoteCertificateChainErrors
                        && certificate is not null
                        && string.Equals(
                            expectedServerThumbprint,
                            certificate.Thumbprint,
                            StringComparison.OrdinalIgnoreCase)
            };

        handler.ClientCertificates.Add(
            clientCertificate);

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

            return new CapabilityC032SecureGrpcClient(
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
    /// Executes one authoritative Property read through the secure remote API.
    /// </summary>
    public async Task<GrpcV1.PropertyOperationResult>
        ReadAuthoritativePropertyAsync(
            Northbound.RuntimeHostPropertyTarget target,
            DateTime deadlineUtc,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            target);

        ObjectDisposedException.ThrowIf(
            disposed,
            this);

        return await client.ReadAuthoritativePropertyAsync(
            new GrpcV1.ReadAuthoritativePropertyRequest
            {
                Target =
                    new GrpcV1.PropertyTarget
                    {
                        EndpointId =
                            target.EndpointId.Value,
                        AttachmentGeneration =
                            target.AttachmentGeneration.ToString(),
                        InstrumentId =
                            target.InstrumentId.Value,
                        PropertyId =
                            target.PropertyId.Value
                    }
            },
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
