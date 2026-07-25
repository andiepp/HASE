using Grpc.Core;
using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostRemoteApiObservationAuthorizationTests
{
    [Fact]
    public async Task Observe_Denied_ShouldFailBeforeConfiguration()
    {
        TestAuthorizationGate authorizationGate =
            new(
                RuntimeHostAuthorizationDecision.Deny(
                    "No grant."));

        RuntimeHostRemoteApiService service =
            CreateService(
                authorizationGate);

        RpcException exception =
            await Assert.ThrowsAsync<RpcException>(
                () =>
                    service.Observe(
                        new GrpcV1.ObserveRequest(),
                        new TestStreamWriter(),
                        null!));

        Assert.Equal(
            StatusCode.PermissionDenied,
            exception.StatusCode);
        Assert.Equal(
            RuntimeHostRemoteOperation.Observe,
            authorizationGate.ObservedOperation);
    }

    [Fact]
    public async Task Observe_Allowed_ShouldContinueToOperation()
    {
        TestAuthorizationGate authorizationGate =
            new(
                RuntimeHostAuthorizationDecision.Allow(
                    "Granted."));

        RuntimeHostRemoteApiService service =
            CreateService(
                authorizationGate);

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    service.Observe(
                        new GrpcV1.ObserveRequest(),
                        new TestStreamWriter(),
                        null!));

        Assert.Equal(
            "Runtime-host observation is not configured.",
            exception.Message);
        Assert.Equal(
            RuntimeHostRemoteOperation.Observe,
            authorizationGate.ObservedOperation);
    }

    [Fact]
    public async Task Observe_NoAuthorizationConfiguration_ShouldPreserveBehavior()
    {
        RuntimeHostRemoteApiService service =
            new(
                new TestSnapshotProvider(),
                RuntimeHostSnapshotMapperFactory.Create());

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    service.Observe(
                        new GrpcV1.ObserveRequest(),
                        new TestStreamWriter(),
                        null!));

        Assert.Equal(
            "Runtime-host observation is not configured.",
            exception.Message);
    }

    [Fact]
    public async Task Observe_NullStream_ShouldFailBeforeAuthorization()
    {
        TestAuthorizationGate authorizationGate =
            new(
                RuntimeHostAuthorizationDecision.Allow(
                    "Granted."));

        RuntimeHostRemoteApiService service =
            CreateService(
                authorizationGate);

        await Assert.ThrowsAsync<ArgumentNullException>(
            "responseStream",
            () =>
                service.Observe(
                    new GrpcV1.ObserveRequest(),
                    null!,
                    null!));

        Assert.Null(
            authorizationGate.ObservedOperation);
    }

    private static RuntimeHostRemoteApiService CreateService(
        IRuntimeHostRemoteAuthorizationGate authorizationGate)
    {
        return new RuntimeHostRemoteApiService(
            new TestSnapshotProvider(),
            RuntimeHostSnapshotMapperFactory.Create(),
            principalProvider:
                new TrustedLoopbackRuntimeHostClientPrincipalProvider(
                    CreatePrincipal()),
            authorizationGate:
                authorizationGate);
    }

    private static RuntimeHostClientPrincipal CreatePrincipal()
    {
        return new RuntimeHostClientPrincipal(
            "trusted-loopback-client",
            "trusted-loopback-profile",
            "trusted-loopback",
            new DateTimeOffset(
                2026,
                7,
                25,
                23,
                30,
                0,
                TimeSpan.Zero),
            "trusted-loopback-v1");
    }

    private sealed class TestSnapshotProvider
        : Northbound.IRuntimeHostSnapshotProvider
    {
        public Northbound.PublishedRuntimeHostSnapshot Capture()
        {
            return new Northbound.PublishedRuntimeHostSnapshot(
                new Northbound.RuntimeHostId(
                    "runtime-host-1"),
                Northbound.RuntimeHostApiVersion.Current,
                []);
        }
    }

    private sealed class TestAuthorizationGate
        : IRuntimeHostRemoteAuthorizationGate
    {
        private readonly RuntimeHostAuthorizationDecision decision;

        public TestAuthorizationGate(
            RuntimeHostAuthorizationDecision decision)
        {
            this.decision = decision;
        }

        public RuntimeHostRemoteOperation? ObservedOperation
        {
            get;
            private set;
        }

        public RuntimeHostAuthorizationDecision Authorize(
            RuntimeHostClientPrincipal principal,
            RuntimeHostRemoteOperation operation)
        {
            ObservedOperation = operation;
            return decision;
        }
    }

    private sealed class TestStreamWriter
        : IServerStreamWriter<GrpcV1.ObserveResponse>
    {
        public WriteOptions? WriteOptions
        {
            get;
            set;
        }

        public Task WriteAsync(
            GrpcV1.ObserveResponse message)
        {
            return Task.CompletedTask;
        }
    }
}
