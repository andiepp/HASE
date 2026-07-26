using Hase.ProtocolExplorer.Scenarios;
using Hase.Runtime.Remote.Grpc.Adapter;
using Microsoft.Extensions.DependencyInjection;
using Northbound = global::Hase.Runtime.Northbound;
using Xunit;

namespace Hase.ProtocolExplorer.Tests;

public sealed class CapabilityC034SecureHostCompositionTests
{
    private static readonly DateTimeOffset ValidationTimeUtc =
        new(
            2026,
            7,
            26,
            17,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public async Task CreateAsync_ShouldRegisterObservationAndAuthentication()
    {
        var snapshotProvider =
            new TestSnapshotProvider();
        var observationService =
            new TestObservationService();

        await using CapabilityC034SecureHostComposition composition =
            await CapabilityC034SecureHostComposition.CreateAsync(
                snapshotProvider,
                observationService,
                ValidationTimeUtc);

        Assert.Same(
            snapshotProvider,
            composition.Application.Services.GetRequiredService<
                Northbound.IRuntimeHostSnapshotProvider>());
        Assert.Same(
            observationService,
            composition.Application.Services.GetRequiredService<
                Northbound.IRuntimeHostObservationService>());
        Assert.NotNull(
            composition.Application.Services.GetRequiredService<
                IObservationInitialSnapshotMapper>());
        Assert.NotNull(
            composition.Application.Services.GetRequiredService<
                IRuntimeHostObservationMapper>());
        Assert.Same(
            composition.AuthenticationComposition.AuthenticationService,
            composition.Application.Services.GetRequiredService<
                IRuntimeHostCertificateAuthenticationService>());
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_ShouldBeIdempotent()
    {
        CapabilityC034SecureHostComposition composition =
            await CapabilityC034SecureHostComposition.CreateAsync(
                new TestSnapshotProvider(),
                new TestObservationService(),
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
                    "runtime-host-c034-composition"),
                Northbound.RuntimeHostApiVersion.Current,
                Array.Empty<
                    Northbound.PublishedRuntimeEndpointSnapshot>());
        }
    }

    private sealed class TestObservationService
        : Northbound.IRuntimeHostObservationService
    {
        public Task<Northbound.RuntimeHostObservationSubscription>
            OpenSubscriptionAsync(
                Northbound.RuntimeHostObservationSubscriptionOptions options,
                CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
