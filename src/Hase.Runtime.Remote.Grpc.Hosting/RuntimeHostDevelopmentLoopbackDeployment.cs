using Microsoft.AspNetCore.Builder;
using Northbound = global::Hase.Runtime.Northbound;

namespace Hase.Runtime.Remote.Grpc.Hosting;

/// <summary>
/// Owns one explicitly labeled certificate-free loopback development
/// runtime-host application. The listener is restricted to one validated
/// loopback address without TLS and without client certificates; every
/// non-loopback deployment requires the secured private-network deployment.
/// </summary>
public sealed class RuntimeHostDevelopmentLoopbackDeployment
    : IAsyncDisposable
{
    private bool disposed;

    private RuntimeHostDevelopmentLoopbackDeployment(
        WebApplication application)
    {
        Application =
            application;
    }

    /// <summary>
    /// Gets the unstarted configured development runtime-host application.
    /// </summary>
    public WebApplication Application
    {
        get;
    }

    /// <summary>
    /// Creates one unstarted loopback-only development deployment.
    /// </summary>
    public static RuntimeHostDevelopmentLoopbackDeployment Create(
        LoopbackGrpcBinding binding,
        Northbound.IRuntimeHostSnapshotProvider snapshotProvider,
        Northbound.IRuntimeHostPropertyService? propertyService = null,
        Northbound.IRuntimeHostCommandService? commandService = null,
        Northbound.IRuntimeHostObservationService? observationService = null)
    {
        ArgumentNullException.ThrowIfNull(
            binding);
        ArgumentNullException.ThrowIfNull(
            snapshotProvider);

        WebApplication application =
            CreateApplication(
                binding,
                snapshotProvider,
                propertyService,
                commandService,
                observationService);

        return new RuntimeHostDevelopmentLoopbackDeployment(
            application);
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

        await Application.DisposeAsync();
    }

    private static WebApplication CreateApplication(
        LoopbackGrpcBinding binding,
        Northbound.IRuntimeHostSnapshotProvider snapshotProvider,
        Northbound.IRuntimeHostPropertyService? propertyService,
        Northbound.IRuntimeHostCommandService? commandService,
        Northbound.IRuntimeHostObservationService? observationService)
    {
        if (observationService is not null)
        {
            return LoopbackGrpcHostFactory.Create(
                binding,
                snapshotProvider,
                propertyService,
                commandService,
                observationService);
        }

        if (commandService is not null)
        {
            return LoopbackGrpcHostFactory.Create(
                binding,
                snapshotProvider,
                propertyService,
                commandService);
        }

        if (propertyService is not null)
        {
            return LoopbackGrpcHostFactory.Create(
                binding,
                snapshotProvider,
                propertyService);
        }

        return LoopbackGrpcHostFactory.Create(
            binding,
            snapshotProvider);
    }
}
