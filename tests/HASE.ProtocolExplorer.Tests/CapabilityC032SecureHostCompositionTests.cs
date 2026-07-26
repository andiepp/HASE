using Hase.ProtocolExplorer.Scenarios;
using Hase.Runtime.Remote.Grpc.Adapter;
using Microsoft.Extensions.DependencyInjection;
using Northbound = global::Hase.Runtime.Northbound;
using Xunit;

namespace Hase.ProtocolExplorer.Tests;

public sealed class CapabilityC032SecureHostCompositionTests
{
    private static readonly DateTimeOffset ValidationTimeUtc =
        new(
            2026,
            7,
            26,
            14,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public async Task CreateAsync_ShouldRegisterSuppliedOperationalServices()
    {
        var snapshotProvider =
            new TestSnapshotProvider();
        var propertyService =
            new TestPropertyService();

        await using CapabilityC032SecureHostComposition composition =
            await CapabilityC032SecureHostComposition.CreateAsync(
                snapshotProvider,
                propertyService,
                ValidationTimeUtc);

        Assert.Same(
            snapshotProvider,
            composition.Application.Services.GetRequiredService<
                Northbound.IRuntimeHostSnapshotProvider>());
        Assert.Same(
            propertyService,
            composition.Application.Services.GetRequiredService<
                Northbound.IRuntimeHostPropertyService>());
    }

    [Fact]
    public async Task CreateAsync_ShouldRegisterOwnedAuthenticationComposition()
    {
        await using CapabilityC032SecureHostComposition composition =
            await CapabilityC032SecureHostComposition.CreateAsync(
                new TestSnapshotProvider(),
                new TestPropertyService(),
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
        CapabilityC032SecureHostComposition composition =
            await CapabilityC032SecureHostComposition.CreateAsync(
                new TestSnapshotProvider(),
                new TestPropertyService(),
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
                    "runtime-host-c032-composition"),
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
}
