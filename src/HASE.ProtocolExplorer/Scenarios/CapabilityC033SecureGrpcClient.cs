using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Grpc.Net.Client;
using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Owns one secure gRPC client used by C-033 Command validation.
/// </summary>
internal sealed class CapabilityC033SecureGrpcClient
    : IDisposable
{
    private readonly HttpClientHandler handler;
    private readonly GrpcChannel channel;
    private readonly GrpcV1.RuntimeHostRemoteApi.RuntimeHostRemoteApiClient
        client;
    private bool disposed;

    private CapabilityC033SecureGrpcClient(
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
    /// Creates a client with an optional client certificate that accepts only
    /// the expected validation server certificate.
    /// </summary>
    public static CapabilityC033SecureGrpcClient Create(
        Uri address,
        X509Certificate2? clientCertificate,
        X509Certificate2 expectedServerCertificate)
    {
        ArgumentNullException.ThrowIfNull(
            address);
        ArgumentNullException.ThrowIfNull(
            expectedServerCertificate);

        if (!string.Equals(
                Uri.UriSchemeHttps,
                address.Scheme,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The C-033 gRPC address must use HTTPS.",
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

        if (clientCertificate is not null)
        {
            handler.ClientCertificates.Add(
                clientCertificate);
        }

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

            return new CapabilityC033SecureGrpcClient(
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
    /// Executes one Command through the secure remote API.
    /// </summary>
    public async Task<GrpcV1.CommandOperationResult> ExecuteCommandAsync(
        Northbound.RuntimeHostCommandTarget target,
        object? argument,
        DateTime deadlineUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            target);

        ObjectDisposedException.ThrowIf(
            disposed,
            this);

        var remoteTarget =
            new GrpcV1.CommandTarget
            {
                EndpointId =
                    target.EndpointId.Value,
                AttachmentGeneration =
                    target.AttachmentGeneration.ToString(),
                InstrumentId =
                    target.InstrumentId.Value
            };

        remoteTarget.CommandPathSegments.AddRange(
            target.CommandPath.Segments);

        var request =
            new GrpcV1.ExecuteCommandRequest
            {
                Target =
                    remoteTarget
            };

        if (argument is not null)
        {
            request.Argument =
                MapValue(
                    argument);
        }

        return await client.ExecuteCommandAsync(
            request,
            deadline:
                deadlineUtc,
            cancellationToken:
                cancellationToken);
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

    private static GrpcV1.RemoteValue MapValue(
        object value)
    {
        return value switch
        {
            bool booleanValue =>
                new GrpcV1.RemoteValue
                {
                    BooleanValue =
                        booleanValue
                },

            string stringValue =>
                new GrpcV1.RemoteValue
                {
                    StringValue =
                        stringValue
                },

            double numericValue =>
                new GrpcV1.RemoteValue
                {
                    NumericValue =
                        numericValue
                },

            _ =>
                throw new ArgumentException(
                    $"The value type '{value.GetType().FullName}' is not "
                    + "supported by the version 1 remote contract.",
                    nameof(value))
        };
    }
}
