using Hase.ProtocolExplorer.Scenarios;
using Hase.Runtime.Remote.Grpc.Adapter;
using Microsoft.Extensions.DependencyInjection;
using Northbound = global::Hase.Runtime.Northbound;
using Xunit;

namespace Hase.ProtocolExplorer.Tests;

public sealed class CapabilityC033SecureHostCompositionTests
{
    private static readonly DateTimeOffset ValidationTimeUtc =
        new(
            2026,
            7,
            26,
            14,
            30,
            0,
            TimeSpan.Zero);

    [Fact]
    public async Task CreateAsync_ShouldRegisterSuppliedOperationalServices()
    {
        var snapshotProvider =
            new TestSnapshotProvider();
        var propertyService =
            new TestPropertyService();
        var commandService =
            new TestCommandService();

        await using CapabilityC033SecureHostComposition composition =
            await CapabilityC033SecureHostComposition.CreateAsync(
                snapshotProvider,
                propertyService,
                commandService,
                ValidationTimeUtc);

        Assert.Same(
            snapshotProvider,
            composition.Application.Services.GetRequiredService<
                Northbound.IRuntimeHostSnapshotProvider>());
        Assert.Same(
            propertyService,
            composition.Application.Services.GetRequiredService<
                Northbound.IRuntimeHostPropertyService>());
        Assert.Same(
            commandService,
            composition.Application.Services.GetRequiredService<
                Northbound.IRuntimeHostCommandService>());
    }

    [Fact]
    public async Task CreateAsync_ShouldRegisterOwnedAuthenticationComposition()
    {
        await using CapabilityC033SecureHostComposition composition =
            await CapabilityC033SecureHostComposition.CreateAsync(
                new TestSnapshotProvider(),
                new TestPropertyService(),
                new TestCommandService(),
                ValidationTimeUtc);

        Assert.Same(
            composition.AuthenticationComposition.AuthenticationService,
            composition.Application.Services.GetRequiredService<
                IRuntimeHostCertificateAuthenticationService>());

        RuntimeHostCertificateAuthenticationResult authenticationResult =
            composition.Application.Services.GetRequiredService<
                    IRuntimeHostCertificateAuthenticationService>()
                .Authenticate(
                    composition.AuthenticationComposition
                        .Certificates
                        .ClientCertificate,
                    ValidationTimeUtc);

        Assert.True(
            authenticationResult.IsAuthenticated);
        Assert.Equal(
            "client-01",
            authenticationResult.Principal?.PrincipalId);
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_ShouldBeIdempotent()
    {
        CapabilityC033SecureHostComposition composition =
            await CapabilityC033SecureHostComposition.CreateAsync(
                new TestSnapshotProvider(),
                new TestPropertyService(),
                new TestCommandService(),
                ValidationTimeUtc);

        await composition.DisposeAsync();

        await composition.DisposeAsync();
    }

    private sealed class TestSnapshotProvider
        : Northbound.IRuntimeHostSnapshotProvider
    {
        public Northbound.PublishedRuntimeHostSnapshot Capture()
        {
            return new Northbound.PublishedRuntimeHostSnapshot(
                new Northbound.RuntimeHostId(
                    "runtime-host-c033-composition"),
                Northbound.RuntimeHostApiVersion.Current,
                Array.Empty<
                    Northbound.PublishedRuntimeEndpointSnapshot>());
        }
    }

    private sealed class TestPropertyService
        : Northbound.IRuntimeHostPropertyService
    {
        public Northbound.RuntimeHostCachedPropertyResult GetCached(
            Northbound.RuntimeHostPropertyTarget target)
        {
            throw new NotSupportedException();
        }

        public Task<Northbound.RuntimeHostPropertyOperationResult> ReadAsync(
            Northbound.RuntimeHostPropertyTarget target,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Northbound.RuntimeHostPropertyOperationResult> WriteAsync(
            Northbound.RuntimeHostPropertyTarget target,
            object? requestedValue,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class TestCommandService
        : Northbound.IRuntimeHostCommandService
    {
        public Task<Northbound.RuntimeHostCommandOperationResult> ExecuteAsync(
            Northbound.RuntimeHostCommandTarget target,
            object? argument,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
