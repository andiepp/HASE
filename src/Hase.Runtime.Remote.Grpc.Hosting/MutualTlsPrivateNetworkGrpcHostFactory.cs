using Hase.Runtime.Remote.Grpc.Adapter;
using Hase.Runtime.Media;
using Microsoft.AspNetCore.Builder;
using Northbound = global::Hase.Runtime.Northbound;

namespace Hase.Runtime.Remote.Grpc.Hosting;

/// <summary>
/// Creates an unstarted ASP.NET Core gRPC host restricted to one explicit
/// private-network listener protected by mutual TLS and provisioned client
/// authentication.
/// </summary>
public static class MutualTlsPrivateNetworkGrpcHostFactory
{
    /// <summary>
    /// Creates an unstarted private-network host exposing the snapshot
    /// operation.
    /// </summary>
    public static WebApplication Create(
        PrivateNetworkGrpcBinding binding,
        RuntimeHostMutualTlsOptions mutualTlsOptions,
        Northbound.IRuntimeHostSnapshotProvider snapshotProvider,
        IRuntimeHostCertificateAuthenticationService
            certificateAuthenticationService,
        TimeProvider? timeProvider = null)
    {
        return CreateCore(
            binding,
            mutualTlsOptions,
            snapshotProvider,
            propertyService: null,
            commandService: null,
            observationService: null,
            diagnosticProjectionService: null,
            authorizationPolicy: null,
            certificateAuthenticationService,
            timeProvider);
    }

    /// <summary>
    /// Creates an unstarted private-network host exposing snapshot and
    /// Property operations.
    /// </summary>
    public static WebApplication Create(
        PrivateNetworkGrpcBinding binding,
        RuntimeHostMutualTlsOptions mutualTlsOptions,
        Northbound.IRuntimeHostSnapshotProvider snapshotProvider,
        Northbound.IRuntimeHostPropertyService propertyService,
        IRuntimeHostCertificateAuthenticationService
            certificateAuthenticationService,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(
            propertyService);

        return CreateCore(
            binding,
            mutualTlsOptions,
            snapshotProvider,
            propertyService,
            commandService: null,
            observationService: null,
            diagnosticProjectionService: null,
            authorizationPolicy: null,
            certificateAuthenticationService,
            timeProvider);
    }

    /// <summary>
    /// Creates an unstarted private-network host exposing Command operations
    /// and optional Property operations.
    /// </summary>
    public static WebApplication Create(
        PrivateNetworkGrpcBinding binding,
        RuntimeHostMutualTlsOptions mutualTlsOptions,
        Northbound.IRuntimeHostSnapshotProvider snapshotProvider,
        Northbound.IRuntimeHostPropertyService? propertyService,
        Northbound.IRuntimeHostCommandService commandService,
        IRuntimeHostCertificateAuthenticationService
            certificateAuthenticationService,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(
            commandService);

        return CreateCore(
            binding,
            mutualTlsOptions,
            snapshotProvider,
            propertyService,
            commandService,
            observationService: null,
            diagnosticProjectionService: null,
            authorizationPolicy: null,
            certificateAuthenticationService,
            timeProvider);
    }

    /// <summary>
    /// Creates an unstarted private-network host exposing observation and
    /// optional Property and Command operations.
    /// </summary>
    public static WebApplication Create(
        PrivateNetworkGrpcBinding binding,
        RuntimeHostMutualTlsOptions mutualTlsOptions,
        Northbound.IRuntimeHostSnapshotProvider snapshotProvider,
        Northbound.IRuntimeHostPropertyService? propertyService,
        Northbound.IRuntimeHostCommandService? commandService,
        Northbound.IRuntimeHostObservationService observationService,
        IRuntimeHostCertificateAuthenticationService
            certificateAuthenticationService,
        TimeProvider? timeProvider = null,
        RuntimeHostAuthorizationPolicy? authorizationPolicy = null,
        RuntimeHostMediaSessionOwner? mediaSessionOwner = null)
    {
        ArgumentNullException.ThrowIfNull(
            observationService);

        return CreateCore(
            binding,
            mutualTlsOptions,
            snapshotProvider,
            propertyService,
            commandService,
            observationService,
            diagnosticProjectionService: null,
            authorizationPolicy,
            certificateAuthenticationService,
            timeProvider,
            mediaSessionOwner);
    }

    /// <summary>
    /// Creates an unstarted private-network host with optional runtime
    /// operations and an explicitly supplied diagnostic projection.
    /// </summary>
    public static WebApplication Create(
        PrivateNetworkGrpcBinding binding,
        RuntimeHostMutualTlsOptions mutualTlsOptions,
        Northbound.IRuntimeHostSnapshotProvider snapshotProvider,
        Northbound.IRuntimeHostPropertyService? propertyService,
        Northbound.IRuntimeHostCommandService? commandService,
        Northbound.IRuntimeHostObservationService? observationService,
        Northbound.RuntimeHostDiagnosticProjectionService
            diagnosticProjectionService,
        IRuntimeHostCertificateAuthenticationService
            certificateAuthenticationService,
        TimeProvider? timeProvider = null,
        RuntimeHostAuthorizationPolicy? authorizationPolicy = null,
        RuntimeHostMediaSessionOwner? mediaSessionOwner = null)
    {
        ArgumentNullException.ThrowIfNull(diagnosticProjectionService);

        return CreateCore(
            binding,
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
    }

    private static WebApplication CreateCore(
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
        RuntimeHostMediaSessionOwner? mediaSessionOwner = null)
    {
        ArgumentNullException.ThrowIfNull(
            binding);
        ArgumentNullException.ThrowIfNull(
            mutualTlsOptions);
        ArgumentNullException.ThrowIfNull(
            snapshotProvider);
        ArgumentNullException.ThrowIfNull(
            certificateAuthenticationService);

        TimeProvider effectiveTimeProvider =
            timeProvider
            ?? TimeProvider.System;

        RuntimeHostPrivateNetworkServerCertificateValidator.Validate(
            mutualTlsOptions.ServerCertificate
            ?? throw new InvalidOperationException(
                "The private-network listener requires a server "
                + "certificate."),
            binding,
            effectiveTimeProvider.GetUtcNow());

        return MutualTlsLoopbackGrpcHostFactory.CreateCore(
            binding.Address,
            binding.Port,
            mutualTlsOptions,
            snapshotProvider,
            propertyService,
            commandService,
            observationService,
            diagnosticProjectionService,
            authorizationPolicy,
            certificateAuthenticationService,
            effectiveTimeProvider,
            clearLoggingProviders:
                true,
            mediaSessionOwner);
    }
}
