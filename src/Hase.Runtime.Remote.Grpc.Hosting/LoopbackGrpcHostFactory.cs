using Hase.Runtime.Remote.Grpc.Adapter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Northbound = global::Hase.Runtime.Northbound;

namespace Hase.Runtime.Remote.Grpc.Hosting;

/// <summary>
/// Creates an unstarted ASP.NET Core gRPC host restricted to one validated
/// loopback HTTP/2 listener.
/// </summary>
public static class LoopbackGrpcHostFactory
{
    /// <summary>
    /// Creates an unstarted loopback-only gRPC application.
    /// </summary>
    public static WebApplication Create(
        LoopbackGrpcBinding binding,
        Northbound.IRuntimeHostSnapshotProvider snapshotProvider)
    {
        return CreateCore(
            binding,
            snapshotProvider,
            propertyService: null,
            commandService: null,
            observationService: null);
    }

    /// <summary>
    /// Creates an unstarted loopback-only gRPC application with Property
    /// operations enabled.
    /// </summary>
    public static WebApplication Create(
        LoopbackGrpcBinding binding,
        Northbound.IRuntimeHostSnapshotProvider snapshotProvider,
        Northbound.IRuntimeHostPropertyService propertyService)
    {
        ArgumentNullException.ThrowIfNull(
            propertyService);

        return CreateCore(
            binding,
            snapshotProvider,
            propertyService,
            commandService: null,
            observationService: null);
    }

    /// <summary>
    /// Creates an unstarted loopback-only gRPC application with Command
    /// operations enabled and optional Property operations.
    /// </summary>
    public static WebApplication Create(
        LoopbackGrpcBinding binding,
        Northbound.IRuntimeHostSnapshotProvider snapshotProvider,
        Northbound.IRuntimeHostPropertyService? propertyService,
        Northbound.IRuntimeHostCommandService commandService)
    {
        ArgumentNullException.ThrowIfNull(
            commandService);

        return CreateCore(
            binding,
            snapshotProvider,
            propertyService,
            commandService,
            observationService: null);
    }

    /// <summary>
    /// Creates an unstarted loopback-only gRPC application with observation
    /// enabled and optional Property and Command operations.
    /// </summary>
    public static WebApplication Create(
        LoopbackGrpcBinding binding,
        Northbound.IRuntimeHostSnapshotProvider snapshotProvider,
        Northbound.IRuntimeHostPropertyService? propertyService,
        Northbound.IRuntimeHostCommandService? commandService,
        Northbound.IRuntimeHostObservationService observationService)
    {
        ArgumentNullException.ThrowIfNull(
            observationService);

        return CreateCore(
            binding,
            snapshotProvider,
            propertyService,
            commandService,
            observationService);
    }

    private static WebApplication CreateCore(
        LoopbackGrpcBinding binding,
        Northbound.IRuntimeHostSnapshotProvider snapshotProvider,
        Northbound.IRuntimeHostPropertyService? propertyService,
        Northbound.IRuntimeHostCommandService? commandService,
        Northbound.IRuntimeHostObservationService? observationService)
    {
        ArgumentNullException.ThrowIfNull(
            binding);
        ArgumentNullException.ThrowIfNull(
            snapshotProvider);

        WebApplicationBuilder builder =
            WebApplication.CreateBuilder(
                new WebApplicationOptions
                {
                    Args =
                        Array.Empty<string>(),
                    ApplicationName =
                        typeof(LoopbackGrpcHostFactory).Assembly.FullName
                });

        builder.WebHost.ConfigureKestrel(
            options =>
                options.Listen(
                    binding.Address,
                    binding.Port,
                    listenOptions =>
                        listenOptions.Protocols =
                            HttpProtocols.Http2));

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

        WebApplication application =
            builder.Build();

        application.MapGrpcService<RuntimeHostRemoteApiService>();

        return application;
    }
}
