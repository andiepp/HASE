using System.Net;
using Hase.Runtime.Remote.Grpc.Adapter;
using Hase.Runtime.Remote.Grpc.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Northbound = global::Hase.Runtime.Northbound;

namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Owns one unstarted mutual-TLS loopback host and its isolated C-032
/// authentication resources.
/// </summary>
internal sealed class CapabilityC032SecureHostComposition
    : IAsyncDisposable
{
    private bool disposed;

    private CapabilityC032SecureHostComposition(
        WebApplication application,
        CapabilityC032AuthenticationComposition authenticationComposition)
    {
        Application =
            application;

        AuthenticationComposition =
            authenticationComposition;
    }

    /// <summary>
    /// Gets the unstarted mutual-TLS loopback application.
    /// </summary>
    public WebApplication Application
    {
        get;
    }

    /// <summary>
    /// Gets the authentication and certificate resources owned by this host.
    /// </summary>
    public CapabilityC032AuthenticationComposition AuthenticationComposition
    {
        get;
    }

    /// <summary>
    /// Starts the secure host and returns its single resolved HTTPS address.
    /// </summary>
    public async Task<Uri> StartAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(
            disposed,
            this);

        await Application.StartAsync(
            cancellationToken);

        IServer server =
            Application.Services.GetRequiredService<IServer>();
        IServerAddressesFeature addressesFeature =
            server.Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException(
                "The server addresses feature is unavailable.");

        return new Uri(
            addressesFeature.Addresses.Single());
    }

    /// <summary>
    /// Creates an unstarted secure host exposing supplied snapshot and
    /// Property services.
    /// </summary>
    public static async Task<CapabilityC032SecureHostComposition> CreateAsync(
        Northbound.IRuntimeHostSnapshotProvider snapshotProvider,
        Northbound.IRuntimeHostPropertyService propertyService,
        DateTimeOffset validationTimeUtc)
    {
        ArgumentNullException.ThrowIfNull(
            snapshotProvider);
        ArgumentNullException.ThrowIfNull(
            propertyService);

        CapabilityC032AuthenticationComposition? authenticationComposition =
            null;
        WebApplication? application =
            null;

        try
        {
            authenticationComposition =
                CapabilityC032AuthenticationComposition.Create(
                    validationTimeUtc);

            application =
                MutualTlsLoopbackGrpcHostFactory.Create(
                    new LoopbackGrpcBinding(
                        IPAddress.Loopback,
                        0),
                    RuntimeHostMutualTlsOptions.EnabledWith(
                        authenticationComposition
                            .Certificates
                            .ServerCertificate),
                    snapshotProvider,
                    propertyService,
                    authenticationComposition.AuthenticationService,
                    new FixedTimeProvider(
                        validationTimeUtc));

            var result =
                new CapabilityC032SecureHostComposition(
                    application,
                    authenticationComposition);

            application =
                null;
            authenticationComposition =
                null;

            return result;
        }
        finally
        {
            try
            {
                if (application is not null)
                {
                    await application.DisposeAsync();
                }
            }
            finally
            {
                authenticationComposition?.Dispose();
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed =
            true;

        try
        {
            await Application.DisposeAsync();
        }
        finally
        {
            AuthenticationComposition.Dispose();
        }
    }

    private sealed class FixedTimeProvider
        : TimeProvider
    {
        private readonly DateTimeOffset utcNow;

        public FixedTimeProvider(
            DateTimeOffset utcNow)
        {
            this.utcNow =
                utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
