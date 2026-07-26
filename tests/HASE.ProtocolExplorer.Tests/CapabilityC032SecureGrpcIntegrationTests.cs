using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.ProtocolExplorer.Scenarios;
using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;
using Xunit;

namespace Hase.ProtocolExplorer.Tests;

public sealed class CapabilityC032SecureGrpcIntegrationTests
{
    private static readonly DateTimeOffset ValidationTimeUtc =
        new(
            2026,
            7,
            26,
            15,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public async Task StartAsync_ShouldResolveEphemeralHttpsAddress()
    {
        await using CapabilityC032SecureHostComposition composition =
            await CapabilityC032SecureHostComposition.CreateAsync(
                new TestSnapshotProvider(),
                new TrackingPropertyService(),
                ValidationTimeUtc);

        Uri address =
            await composition.StartAsync();

        Assert.Equal(
            Uri.UriSchemeHttps,
            address.Scheme);
        Assert.True(
            address.IsLoopback);
        Assert.True(
            address.Port > 0);
    }

    [Fact]
    public async Task ReadAuthoritativePropertyAsync_ShouldReachSuppliedService()
    {
        var propertyService =
            new TrackingPropertyService();

        await using CapabilityC032SecureHostComposition composition =
            await CapabilityC032SecureHostComposition.CreateAsync(
                new TestSnapshotProvider(),
                propertyService,
                ValidationTimeUtc);

        Uri address =
            await composition.StartAsync();

        using CapabilityC032SecureGrpcClient client =
            CapabilityC032SecureGrpcClient.Create(
                address,
                composition.AuthenticationComposition
                    .Certificates
                    .ClientCertificate,
                composition.AuthenticationComposition
                    .Certificates
                    .ServerCertificate);

        var target =
            new Northbound.RuntimeHostPropertyTarget(
                new EndpointId(
                    "endpoint-01"),
                new Northbound.RuntimeEndpointAttachmentGeneration(
                    new Guid(
                        "868e79d4-b1a4-4a63-81cd-5a800d9ba3fd")),
                new InstrumentId(
                    "environment-sensor-01"),
                new PropertyId(
                    "physical.environment-sensor.temperature"));

        GrpcV1.PropertyOperationResult response =
            await client.ReadAuthoritativePropertyAsync(
                target,
                DateTime.UtcNow.AddSeconds(
                    10));

        Assert.Equal(
            1,
            propertyService.ReadCount);
        Assert.Equal(
            target,
            propertyService.ReadTarget);
        Assert.Null(
            propertyService.CachedTarget);
        Assert.Equal(
            GrpcV1.PropertyOperationStatus.Success,
            response.Status);
        Assert.NotNull(
            response.ConfirmedValue);
        Assert.Equal(
            23.75,
            response.ConfirmedValue.Value.NumericValue);
        Assert.Equal(
            GrpcV1.PropertyQuality.Good,
            response.ConfirmedValue.Quality);
        Assert.Equal(
            DateTimeOffset.UnixEpoch,
            response.ConfirmedValue.TimestampUtc.ToDateTimeOffset());
    }

    private sealed class TestSnapshotProvider
        : Northbound.IRuntimeHostSnapshotProvider
    {
        public Northbound.PublishedRuntimeHostSnapshot Capture()
        {
            return new Northbound.PublishedRuntimeHostSnapshot(
                new Northbound.RuntimeHostId(
                    "runtime-host-c032-secure-grpc"),
                Northbound.RuntimeHostApiVersion.Current,
                Array.Empty<
                    Northbound.PublishedRuntimeEndpointSnapshot>());
        }
    }

    private sealed class TrackingPropertyService
        : Northbound.IRuntimeHostPropertyService
    {
        public int ReadCount
        {
            get;
            private set;
        }

        public Northbound.RuntimeHostPropertyTarget? CachedTarget
        {
            get;
            private set;
        }

        public Northbound.RuntimeHostPropertyTarget? ReadTarget
        {
            get;
            private set;
        }

        public Northbound.RuntimeHostCachedPropertyResult GetCached(
            Northbound.RuntimeHostPropertyTarget target)
        {
            CachedTarget =
                target;

            throw new InvalidOperationException(
                "The authoritative read used the cached path.");
        }

        public Task<Northbound.RuntimeHostPropertyOperationResult> ReadAsync(
            Northbound.RuntimeHostPropertyTarget target,
            CancellationToken cancellationToken = default)
        {
            ReadCount++;
            ReadTarget =
                target;

            return Task.FromResult(
                Northbound.RuntimeHostPropertyOperationResult.Successful(
                    new PropertyValue(
                        23.75,
                        DateTimeOffset.UnixEpoch)));
        }

        public Task<Northbound.RuntimeHostPropertyOperationResult> WriteAsync(
            Northbound.RuntimeHostPropertyTarget target,
            object? requestedValue,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "The authoritative read used the write path.");
        }
    }
}
