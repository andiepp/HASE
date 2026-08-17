using System.Security.Cryptography.X509Certificates;
using Hase.Runtime.Remote.Grpc.Adapter;
using Hase.Runtime.Media;
using Microsoft.AspNetCore.Builder;
using Northbound = global::Hase.Runtime.Northbound;

namespace Hase.Runtime.Remote.Grpc.Hosting;

/// <summary>
/// Owns one configured private-network runtime-host application and its
/// externally provisioned server certificate.
/// </summary>
public sealed class RuntimeHostPrivateNetworkDeployment
    : IAsyncDisposable
{
    private readonly X509Certificate2 serverCertificate;
    private bool disposed;

    private RuntimeHostPrivateNetworkDeployment(
        WebApplication application,
        X509Certificate2 serverCertificate)
    {
        Application =
            application;
        this.serverCertificate =
            serverCertificate;
    }

    /// <summary>
    /// Gets the unstarted configured runtime-host application.
    /// </summary>
    public WebApplication Application
    {
        get;
    }

    /// <summary>
    /// Creates one unstarted secured private-network deployment.
    /// </summary>
    public static async Task<RuntimeHostPrivateNetworkDeployment> CreateAsync(
        RuntimeHostPrivateNetworkDeploymentOptions options,
        Northbound.IRuntimeHostSnapshotProvider snapshotProvider,
        Northbound.IRuntimeHostPropertyService? propertyService = null,
        Northbound.IRuntimeHostCommandService? commandService = null,
        Northbound.IRuntimeHostObservationService? observationService = null,
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default,
        Northbound.RuntimeHostDiagnosticProjectionService?
            diagnosticProjectionService = null,
        RuntimeHostAuthorizationPolicy? authorizationPolicy = null,
        RuntimeHostMediaSessionOwner? mediaSessionOwner = null)
    {
        ArgumentNullException.ThrowIfNull(
            options);
        ArgumentNullException.ThrowIfNull(
            snapshotProvider);

        cancellationToken.ThrowIfCancellationRequested();

        X509Certificate2 serverCertificate =
            RuntimeHostCertificateStoreLoader.Load(
                options.ServerCertificate,
                requirePrivateKey: true);

        try
        {
            IRuntimeHostCertificateAuthenticationService
                certificateAuthenticationService =
                    await RuntimeHostProvisionedCertificateAuthenticationFactory
                        .CreateSystemTrustAsync(
                            options.ClientEnrollmentFilePath,
                            cancellationToken)
                        .ConfigureAwait(
                            false);

            cancellationToken.ThrowIfCancellationRequested();

            RuntimeHostMutualTlsOptions mutualTlsOptions =
                RuntimeHostMutualTlsOptions.EnabledWith(
                    serverCertificate);
            WebApplication application =
                CreateApplication(
                    options.Binding,
                    mutualTlsOptions,
                    snapshotProvider,
                    propertyService,
                    commandService,
                    observationService,
                    diagnosticProjectionService,
                    authorizationPolicy,
                    certificateAuthenticationService,
                    timeProvider,
                    mediaSessionOwner);

            return new RuntimeHostPrivateNetworkDeployment(
                application,
                serverCertificate);
        }
        catch
        {
            serverCertificate.Dispose();
            throw;
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
            serverCertificate.Dispose();
        }
    }

    private static WebApplication CreateApplication(
        PrivateNetworkGrpcBinding binding,
        RuntimeHostMutualTlsOptions mutualTlsOptions,
        Northbound.IRuntimeHostSnapshotProvider snapshotProvider,
        Northbound.IRuntimeHostPropertyService? propertyService,
        Northbound.IRuntimeHostCommandService? commandService,
        Northbound.IRuntimeHostObservationService? observationService,
        Northbound.RuntimeHostDiagnosticProjectionService?
            diagnosticProjectionService,
        RuntimeHostAuthorizationPolicy? authorizationPolicy,
        IRuntimeHostCertificateAuthenticationService
            certificateAuthenticationService,
        TimeProvider? timeProvider,
        RuntimeHostMediaSessionOwner? mediaSessionOwner)
    {
        if (diagnosticProjectionService is not null)
        {
            return MutualTlsPrivateNetworkGrpcHostFactory.Create(
                binding,
                mutualTlsOptions,
                snapshotProvider,
                propertyService,
                commandService,
                observationService,
                diagnosticProjectionService,
                certificateAuthenticationService,
                timeProvider,
                authorizationPolicy,
                mediaSessionOwner);
        }

        if (observationService is not null)
        {
            return MutualTlsPrivateNetworkGrpcHostFactory.Create(
                binding,
                mutualTlsOptions,
                snapshotProvider,
                propertyService,
                commandService,
                observationService,
                certificateAuthenticationService,
                timeProvider,
                authorizationPolicy,
                mediaSessionOwner);
        }

        if (authorizationPolicy is not null)
        {
            throw new InvalidOperationException(
                "Semantic authorization requires observation or explicitly "
                + "composed diagnostic projection hosting in this increment.");
        }

        if (commandService is not null)
        {
            return MutualTlsPrivateNetworkGrpcHostFactory.Create(
                binding,
                mutualTlsOptions,
                snapshotProvider,
                propertyService,
                commandService,
                certificateAuthenticationService,
                timeProvider);
        }

        if (propertyService is not null)
        {
            return MutualTlsPrivateNetworkGrpcHostFactory.Create(
                binding,
                mutualTlsOptions,
                snapshotProvider,
                propertyService,
                certificateAuthenticationService,
                timeProvider);
        }

        return MutualTlsPrivateNetworkGrpcHostFactory.Create(
            binding,
            mutualTlsOptions,
            snapshotProvider,
            certificateAuthenticationService,
            timeProvider);
    }
}
