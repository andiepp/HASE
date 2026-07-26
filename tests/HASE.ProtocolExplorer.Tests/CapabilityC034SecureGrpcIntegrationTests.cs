using System.Runtime.CompilerServices;
using Grpc.Core;
using Hase.ProtocolExplorer.Scenarios;
using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;
using Xunit;

namespace Hase.ProtocolExplorer.Tests;

public sealed class CapabilityC034SecureGrpcIntegrationTests
{
    [Fact]
    public async Task ObserveAsync_ShouldReachSuppliedServiceThroughMutualTls()
    {
        var observationService =
            new TestObservationService();

        await using CapabilityC034SecureHostComposition composition =
            await CapabilityC034SecureHostComposition.CreateAsync(
                new TestSnapshotProvider(),
                observationService,
                DateTimeOffset.UtcNow);

        Uri address =
            await composition.StartAsync();

        using CapabilityC034SecureGrpcClient client =
            CapabilityC034SecureGrpcClient.Create(
                address,
                composition.AuthenticationComposition
                    .Certificates
                    .ClientCertificate,
                composition.AuthenticationComposition
                    .Certificates
                    .ServerCertificate);
        using AsyncServerStreamingCall<GrpcV1.ObserveResponse> call =
            client.Observe(
                DateTime.UtcNow.AddSeconds(
                    10));
        var messages =
            new List<GrpcV1.ObserveResponse>();

        while (await call.ResponseStream.MoveNext())
        {
            messages.Add(
                call.ResponseStream.Current);
        }

        Assert.Collection(
            messages,
            initial =>
            {
                Assert.Equal(
                    GrpcV1.ObserveResponse.ContentOneofCase.InitialSnapshot,
                    initial.ContentCase);
                Assert.Equal(
                    "runtime-host-c034-secure-integration",
                    initial.InitialSnapshot.Snapshot.RuntimeHostId);
                Assert.Equal(
                    0UL,
                    initial.InitialSnapshot.SnapshotSequence);
            },
            observation =>
            {
                Assert.Equal(
                    GrpcV1.ObserveResponse.ContentOneofCase.Observation,
                    observation.ContentCase);
                Assert.Equal(
                    GrpcV1.RuntimeHostObservationKind.EventOccurred,
                    observation.Observation.Kind);
                Assert.Equal(
                    1UL,
                    observation.Observation.Sequence);
                Assert.Equal(
                    "pressed",
                    observation.Observation.EventOccurred.Value.StringValue);
            });
        Assert.Equal(
            1,
            observationService.OpenCount);
        Assert.Equal(
            1,
            observationService.Subscription.DisposeCount);
    }

    private sealed class TestSnapshotProvider
        : Northbound.IRuntimeHostSnapshotProvider
    {
        public Northbound.PublishedRuntimeHostSnapshot Capture()
        {
            return new Northbound.PublishedRuntimeHostSnapshot(
                new Northbound.RuntimeHostId(
                    "runtime-host-c034-snapshot"),
                Northbound.RuntimeHostApiVersion.Current,
                Array.Empty<
                    Northbound.PublishedRuntimeEndpointSnapshot>());
        }
    }

    private sealed class TestObservationService
        : Northbound.IRuntimeHostObservationService
    {
        public TestObservationService()
        {
            var snapshot =
                new Northbound.PublishedRuntimeHostSnapshot(
                    new Northbound.RuntimeHostId(
                        "runtime-host-c034-secure-integration"),
                    Northbound.RuntimeHostApiVersion.Current,
                    Array.Empty<
                        Northbound.PublishedRuntimeEndpointSnapshot>());
            var observation =
                new Northbound.RuntimeHostObservation(
                    new Northbound.RuntimeHostObservationSequence(
                        1),
                    new Hase.Core.Domain.Identity.EndpointId(
                        "doit-esp32-devkitc-v4-01"),
                    new Northbound.RuntimeEndpointAttachmentGeneration(
                        new Guid(
                            "868e79d4-b1a4-4a63-81cd-5a800d9ba3fd")),
                    new Northbound.RuntimeHostEventOccurredObservationPayload(
                        new Hase.Core.Domain.Identity.InstrumentId(
                            "controller-01"),
                        new Hase.Core.Domain.Properties.DescriptorPath(
                            "Controller",
                            "ButtonPressed"),
                        DateTimeOffset.UnixEpoch,
                        "pressed"));

            Subscription =
                new TestSubscription(
                    snapshot,
                    observation);
        }

        public int OpenCount
        {
            get;
            private set;
        }

        public TestSubscription Subscription
        {
            get;
        }

        public Task<Northbound.RuntimeHostObservationSubscription>
            OpenSubscriptionAsync(
                Northbound.RuntimeHostObservationSubscriptionOptions options,
                CancellationToken cancellationToken = default)
        {
            OpenCount++;

            return Task.FromResult<
                Northbound.RuntimeHostObservationSubscription>(
                    Subscription);
        }
    }

    private sealed class TestSubscription
        : Northbound.RuntimeHostObservationSubscription
    {
        private readonly Northbound.RuntimeHostObservation observation;

        public TestSubscription(
            Northbound.PublishedRuntimeHostSnapshot initialSnapshot,
            Northbound.RuntimeHostObservation observation)
            : base(
                initialSnapshot,
                new Northbound.RuntimeHostObservationSequence(
                    0))
        {
            this.observation =
                observation;
        }

        public int DisposeCount
        {
            get;
            private set;
        }

        public override async IAsyncEnumerable<
            Northbound.RuntimeHostObservation> ReadAllAsync(
                [EnumeratorCancellation]
                CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            yield return observation;

            await Task.CompletedTask;
        }

        public override ValueTask DisposeAsync()
        {
            DisposeCount++;

            return ValueTask.CompletedTask;
        }
    }
}
