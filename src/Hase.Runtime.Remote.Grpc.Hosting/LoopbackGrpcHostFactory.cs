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
            propertyService: null);
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
            propertyService);
    }

    private static WebApplication CreateCore(
        LoopbackGrpcBinding binding,
        Northbound.IRuntimeHostSnapshotProvider snapshotProvider,
        Northbound.IRuntimeHostPropertyService? propertyService)
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
        }

        WebApplication application =
            builder.Build();

        application.MapGrpcService<RuntimeHostRemoteApiService>();

        return application;
    }
}
