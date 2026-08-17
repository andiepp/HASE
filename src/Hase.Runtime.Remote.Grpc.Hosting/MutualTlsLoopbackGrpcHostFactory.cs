using Hase.Runtime.Remote.Grpc.Adapter;
using Hase.Runtime.Media;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Northbound = global::Hase.Runtime.Northbound;

namespace Hase.Runtime.Remote.Grpc.Hosting;

/// <summary>
/// Creates an unstarted ASP.NET Core gRPC host restricted to one validated
/// loopback listener protected by mutual TLS and the C-030 authentication
/// pipeline.
/// </summary>
public static class MutualTlsLoopbackGrpcHostFactory
{
    /// <summary>
    /// Creates an unstarted mutual-TLS loopback gRPC application exposing the
    /// runtime-host snapshot operation.
    /// </summary>
    public static WebApplication Create(
        LoopbackGrpcBinding binding,
        RuntimeHostMutualTlsOptions mutualTlsOptions,
        Northbound.IRuntimeHostSnapshotProvider snapshotProvider,
        IRuntimeHostCertificateAuthenticationService
            certificateAuthenticationService,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(
            binding);

        return CreateCore(
            binding.Address,
            binding.Port,
            mutualTlsOptions,
            snapshotProvider,
            propertyService: null,
            commandService: null,
            observationService: null,
            diagnosticProjectionService: null,
            authorizationPolicy: null,
            certificateAuthenticationService,
            timeProvider,
            clearLoggingProviders:
                false);
    }

    /// <summary>
    /// Creates an unstarted mutual-TLS loopback gRPC application exposing the
    /// runtime-host snapshot and Property operations.
    /// </summary>
    public static WebApplication Create(
        LoopbackGrpcBinding binding,
        RuntimeHostMutualTlsOptions mutualTlsOptions,
        Northbound.IRuntimeHostSnapshotProvider snapshotProvider,
        Northbound.IRuntimeHostPropertyService propertyService,
        IRuntimeHostCertificateAuthenticationService
            certificateAuthenticationService,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(
            binding);
        ArgumentNullException.ThrowIfNull(
            propertyService);

        return CreateCore(
            binding.Address,
            binding.Port,
            mutualTlsOptions,
            snapshotProvider,
            propertyService,
            commandService: null,
            observationService: null,
            diagnosticProjectionService: null,
            authorizationPolicy: null,
            certificateAuthenticationService,
            timeProvider,
            clearLoggingProviders:
                false);
    }

    /// <summary>
    /// Creates an unstarted mutual-TLS loopback gRPC application exposing
    /// Command operations and optional Property operations.
    /// </summary>
    public static WebApplication Create(
        LoopbackGrpcBinding binding,
        RuntimeHostMutualTlsOptions mutualTlsOptions,
        Northbound.IRuntimeHostSnapshotProvider snapshotProvider,
        Northbound.IRuntimeHostPropertyService? propertyService,
        Northbound.IRuntimeHostCommandService commandService,
        IRuntimeHostCertificateAuthenticationService
            certificateAuthenticationService,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(
            binding);
        ArgumentNullException.ThrowIfNull(
            commandService);

        return CreateCore(
            binding.Address,
            binding.Port,
            mutualTlsOptions,
            snapshotProvider,
            propertyService,
            commandService,
            observationService: null,
            diagnosticProjectionService: null,
            authorizationPolicy: null,
            certificateAuthenticationService,
            timeProvider,
            clearLoggingProviders:
                false);
    }

    /// <summary>
    /// Creates an unstarted mutual-TLS loopback gRPC application exposing
    /// observation and optional Property and Command operations.
    /// </summary>
    public static WebApplication Create(
        LoopbackGrpcBinding binding,
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
            binding);
        ArgumentNullException.ThrowIfNull(
            observationService);

        return CreateCore(
            binding.Address,
            binding.Port,
            mutualTlsOptions,
            snapshotProvider,
            propertyService,
            commandService,
            observationService,
            diagnosticProjectionService: null,
            authorizationPolicy,
            certificateAuthenticationService,
            timeProvider,
            clearLoggingProviders:
                false,
            mediaSessionOwner);
    }

    /// <summary>
    /// Creates an unstarted mutual-TLS loopback gRPC application with optional
    /// runtime operations and an explicitly supplied diagnostic projection.
    /// </summary>
    public static WebApplication Create(
        LoopbackGrpcBinding binding,
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
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(diagnosticProjectionService);

        return CreateCore(
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
            timeProvider,
            clearLoggingProviders: false,
            mediaSessionOwner);
    }

    internal static WebApplication CreateCore(
        System.Net.IPAddress address,
        int port,
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
        bool clearLoggingProviders,
        RuntimeHostMediaSessionOwner? mediaSessionOwner = null)
    {
        ArgumentNullException.ThrowIfNull(
            address);
        ArgumentNullException.ThrowIfNull(
            mutualTlsOptions);
        ArgumentNullException.ThrowIfNull(
            snapshotProvider);
        ArgumentNullException.ThrowIfNull(
            certificateAuthenticationService);

        RuntimeHostMutualTlsKestrelConfiguration kestrelConfiguration =
            RuntimeHostMutualTlsKestrelConfigurationFactory.Create(
                mutualTlsOptions);

        /*
         * Kestrel requires a client certificate during the TLS handshake.
         * Platform trust acceptance is deliberately deferred to the complete
         * C-030 authentication pipeline so that HASE owns deterministic trust,
         * enrollment, and principal resolution. No request reaches gRPC before
         * that pipeline accepts the certificate.
         */
        kestrelConfiguration.HttpsOptions.ClientCertificateValidation =
            static (_, _, _) => true;

        WebApplicationBuilder builder =
            WebApplication.CreateBuilder(
                new WebApplicationOptions
                {
                    Args =
                        Array.Empty<string>(),
                    ApplicationName =
                        typeof(MutualTlsLoopbackGrpcHostFactory)
                            .Assembly
                            .FullName
                });

        if (clearLoggingProviders)
        {
            builder.Logging.ClearProviders();
        }

        builder.WebHost.ConfigureKestrel(
            options =>
                options.Listen(
                    address,
                    port,
                    listenOptions =>
                    {
                        listenOptions.Protocols =
                            kestrelConfiguration.Protocols;
                        listenOptions.UseHttps(
                            kestrelConfiguration.HttpsOptions);
                    }));

        builder.Services.AddGrpc();
        builder.Services.AddSingleton(
            snapshotProvider);
        builder.Services.AddSingleton(
            RuntimeHostSnapshotMapperFactory.Create());

        if (propertyService is not null)
        {
            RuntimeHostPropertyMappers propertyMappers =
                RuntimeHostPropertyMapperFactory.Create();

            builder.Services.AddSingleton(
                propertyService);
            builder.Services.AddSingleton(
                propertyMappers.TargetMapper);
            builder.Services.AddSingleton(
                propertyMappers.CachedResultMapper);
            builder.Services.AddSingleton(
                propertyMappers.OperationResultMapper);
            builder.Services.AddSingleton(
                propertyMappers.RemoteValueMapper);
        }

        if (commandService is not null)
        {
            RuntimeHostCommandMappers commandMappers =
                RuntimeHostCommandMapperFactory.Create();

            builder.Services.AddSingleton(
                commandService);
            builder.Services.AddSingleton(
                commandMappers.TargetMapper);
            builder.Services.AddSingleton(
                commandMappers.OperationResultMapper);

            if (propertyService is null)
            {
                builder.Services.AddSingleton(
                    commandMappers.RemoteValueMapper);
            }
        }

        if (observationService is not null)
        {
            RuntimeHostObservationMappers observationMappers =
                RuntimeHostObservationMapperFactory.Create();

            builder.Services.AddSingleton(
                observationService);
            builder.Services.AddSingleton(
                observationMappers.InitialSnapshotMapper);
            builder.Services.AddSingleton(
                observationMappers.ObservationMapper);
        }

        if (diagnosticProjectionService is not null)
        {
            builder.Services.AddSingleton(diagnosticProjectionService);
            builder.Services.AddSingleton<
                RuntimeHostProjectedDiagnosticObservationMapper>();
        }

        if (authorizationPolicy is not null)
        {
            var authorizationService =
                new PolicyRuntimeHostAuthorizationService(
                    authorizationPolicy);
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddSingleton<IRuntimeHostClientPrincipalProvider,
                HttpContextRuntimeHostClientPrincipalProvider>();
            builder.Services.AddSingleton<
                IRuntimeHostRemoteOperationPermissionMapper,
                RuntimeHostRemoteOperationPermissionMapper>();
            builder.Services.AddSingleton<IRuntimeHostAuthorizationService>(
                authorizationService);
            builder.Services.AddSingleton<IRuntimeHostRemoteAuthorizationGate>(
                serviceProvider =>
                    new RuntimeHostRemoteAuthorizationGate(
                        serviceProvider.GetRequiredService<
                            IRuntimeHostRemoteOperationPermissionMapper>(),
                        serviceProvider.GetRequiredService<
                            IRuntimeHostAuthorizationService>()));
        }

        if (mediaSessionOwner is not null)
        {
            if (authorizationPolicy is null)
            {
                throw new InvalidOperationException(
                    "Runtime Host media requires an explicit authorization policy.");
            }

            builder.Services.AddSingleton(mediaSessionOwner);
            builder.Services.AddSingleton(
                snapshotProvider.Capture().RuntimeHostId.Value);
            builder.Services.AddSingleton<RuntimeHostMediaAuthorizationGate>();
            builder.Services.AddSingleton<RuntimeHostMediaCapabilityMapper>();
            builder.Services.AddSingleton<RuntimeHostMediaControlLimitsMapper>();
            builder.Services.AddSingleton<RuntimeHostMediaControlContractValidator>();
            builder.Services.AddSingleton<RuntimeHostMediaGrpcMapper>();
        }

        builder.Services.AddSingleton(
            certificateAuthenticationService);
        builder.Services.AddSingleton(
            new RuntimeHostMutualTlsClientCertificateAuthenticator(
                certificateAuthenticationService));
        builder.Services.AddSingleton<
            RuntimeHostHttpContextIdentityProjector>();
        builder.Services.AddSingleton<
            RuntimeHostMutualTlsRequestAuthenticator>();
        builder.Services.AddSingleton(
            timeProvider
            ?? TimeProvider.System);

        WebApplication application =
            builder.Build();

        application.UseMiddleware<
            RuntimeHostMutualTlsAuthenticationMiddleware>();
        application.MapGrpcService<RuntimeHostRemoteApiService>();
        if (mediaSessionOwner is not null)
        {
            application.MapGrpcService<RuntimeHostMediaControlService>();
        }

        return application;
    }
}
