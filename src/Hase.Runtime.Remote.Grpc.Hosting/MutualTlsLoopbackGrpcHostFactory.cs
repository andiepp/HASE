using Hase.Runtime.Remote.Grpc.Adapter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
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
        return CreateCore(
            binding,
            mutualTlsOptions,
            snapshotProvider,
            propertyService: null,
            certificateAuthenticationService,
            timeProvider);
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
            propertyService);

        return CreateCore(
            binding,
            mutualTlsOptions,
            snapshotProvider,
            propertyService,
            certificateAuthenticationService,
            timeProvider);
    }

    private static WebApplication CreateCore(
        LoopbackGrpcBinding binding,
        RuntimeHostMutualTlsOptions mutualTlsOptions,
        Northbound.IRuntimeHostSnapshotProvider snapshotProvider,
        Northbound.IRuntimeHostPropertyService? propertyService,
        IRuntimeHostCertificateAuthenticationService
            certificateAuthenticationService,
        TimeProvider? timeProvider)
    {
        ArgumentNullException.ThrowIfNull(
            binding);
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

        builder.WebHost.ConfigureKestrel(
            options =>
                options.Listen(
                    binding.Address,
                    binding.Port,
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

        return application;
    }
}
