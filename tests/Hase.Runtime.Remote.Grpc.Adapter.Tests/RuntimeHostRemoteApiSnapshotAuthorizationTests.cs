using Grpc.Core;
using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostRemoteApiSnapshotAuthorizationTests
{
    [Fact]
    public void Constructor_PrincipalProviderWithoutGate_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "authorizationGate",
            () =>
                new RuntimeHostRemoteApiService(
                    new CountingSnapshotProvider(
                        CreateSnapshot()),
                    RuntimeHostSnapshotMapperFactory.Create(),
                    principalProvider:
                        new TestPrincipalProvider(
                            CreatePrincipal()),
                    authorizationGate:
                        null));
    }

    [Fact]
    public void Constructor_GateWithoutPrincipalProvider_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "principalProvider",
            () =>
                new RuntimeHostRemoteApiService(
                    new CountingSnapshotProvider(
                        CreateSnapshot()),
                    RuntimeHostSnapshotMapperFactory.Create(),
                    principalProvider:
                        null,
                    authorizationGate:
                        new TestAuthorizationGate(
                            RuntimeHostAuthorizationDecision.Allow(
                                "Granted."))));
    }

    [Fact]
    public async Task GetSnapshot_Allowed_ShouldCaptureSnapshot()
    {
        CountingSnapshotProvider snapshotProvider =
            new(
                CreateSnapshot());
        TestPrincipalProvider principalProvider =
            new(
                CreatePrincipal());
        TestAuthorizationGate authorizationGate =
            new(
                RuntimeHostAuthorizationDecision.Allow(
                    "Granted."));

        RuntimeHostRemoteApiService service =
            CreateService(
                snapshotProvider,
                principalProvider,
                authorizationGate);

        GrpcV1.GetSnapshotResponse response =
            await service.GetSnapshot(
                new GrpcV1.GetSnapshotRequest(),
                null!);

        Assert.Equal(
            1,
            snapshotProvider.CaptureCount);
        Assert.Equal(
            "runtime-host-1",
            response.RuntimeHostId);
        Assert.Same(
            principalProvider.Principal,
            authorizationGate.ObservedPrincipal);
        Assert.Equal(
            RuntimeHostRemoteOperation.GetSnapshot,
            authorizationGate.ObservedOperation);
    }

    [Fact]
    public async Task GetSnapshot_Denied_ShouldNotCaptureSnapshot()
    {
        CountingSnapshotProvider snapshotProvider =
            new(
                CreateSnapshot());

        RuntimeHostRemoteApiService service =
            CreateService(
                snapshotProvider,
                new TestPrincipalProvider(
                    CreatePrincipal()),
                new TestAuthorizationGate(
                    RuntimeHostAuthorizationDecision.Deny(
                        "No grant.")));

        RpcException exception =
            await Assert.ThrowsAsync<RpcException>(
                () =>
                    service.GetSnapshot(
                        new GrpcV1.GetSnapshotRequest(),
                        null!));

        Assert.Equal(
            StatusCode.PermissionDenied,
            exception.StatusCode);
        Assert.Equal(
            0,
            snapshotProvider.CaptureCount);
    }

    [Fact]
    public async Task GetSnapshot_PrincipalProviderReturnsNull_ShouldNotCapture()
    {
        CountingSnapshotProvider snapshotProvider =
            new(
                CreateSnapshot());

        RuntimeHostRemoteApiService service =
            CreateService(
                snapshotProvider,
                new TestPrincipalProvider(
                    null!),
                new TestAuthorizationGate(
                    RuntimeHostAuthorizationDecision.Allow(
                        "Granted.")));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                service.GetSnapshot(
                    new GrpcV1.GetSnapshotRequest(),
                    null!));

        Assert.Equal(
            0,
            snapshotProvider.CaptureCount);
    }

    [Fact]
    public async Task GetSnapshot_AuthorizationGateReturnsNull_ShouldNotCapture()
    {
        CountingSnapshotProvider snapshotProvider =
            new(
                CreateSnapshot());

        RuntimeHostRemoteApiService service =
            CreateService(
                snapshotProvider,
                new TestPrincipalProvider(
                    CreatePrincipal()),
                new TestAuthorizationGate(
                    null!));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                service.GetSnapshot(
                    new GrpcV1.GetSnapshotRequest(),
                    null!));

        Assert.Equal(
            0,
            snapshotProvider.CaptureCount);
    }

    [Fact]
    public async Task GetSnapshot_NoAuthorizationConfiguration_ShouldRemainAvailable()
    {
        CountingSnapshotProvider snapshotProvider =
            new(
                CreateSnapshot());

        RuntimeHostRemoteApiService service =
            new(
                snapshotProvider,
                RuntimeHostSnapshotMapperFactory.Create());

        await service.GetSnapshot(
            new GrpcV1.GetSnapshotRequest(),
            null!);

        Assert.Equal(
            1,
            snapshotProvider.CaptureCount);
    }

    private static RuntimeHostRemoteApiService CreateService(
        CountingSnapshotProvider snapshotProvider,
        IRuntimeHostClientPrincipalProvider principalProvider,
        IRuntimeHostRemoteAuthorizationGate authorizationGate)
    {
        return new RuntimeHostRemoteApiService(
            snapshotProvider,
            RuntimeHostSnapshotMapperFactory.Create(),
            principalProvider:
                principalProvider,
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
                21,
                0,
                0,
                TimeSpan.Zero),
            "trusted-loopback-v1");
    }

    private static Northbound.PublishedRuntimeHostSnapshot CreateSnapshot()
    {
        return new Northbound.PublishedRuntimeHostSnapshot(
            new Northbound.RuntimeHostId(
                "runtime-host-1"),
            Northbound.RuntimeHostApiVersion.Current,
            []);
    }

    private sealed class CountingSnapshotProvider
        : Northbound.IRuntimeHostSnapshotProvider
    {
        private readonly Northbound.PublishedRuntimeHostSnapshot snapshot;

        public CountingSnapshotProvider(
            Northbound.PublishedRuntimeHostSnapshot snapshot)
        {
            this.snapshot = snapshot;
        }

        public int CaptureCount
        {
            get;
            private set;
        }

        public Northbound.PublishedRuntimeHostSnapshot Capture()
        {
            CaptureCount++;
            return snapshot;
        }
    }

    private sealed class TestPrincipalProvider
        : IRuntimeHostClientPrincipalProvider
    {
        public TestPrincipalProvider(
            RuntimeHostClientPrincipal principal)
        {
            Principal = principal;
        }

        public RuntimeHostClientPrincipal Principal
        {
            get;
        }

        public RuntimeHostClientPrincipal GetPrincipal(
            ServerCallContext? context)
        {
            return Principal;
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

        public RuntimeHostClientPrincipal? ObservedPrincipal
        {
            get;
            private set;
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
            ObservedPrincipal = principal;
            ObservedOperation = operation;
            return decision;
        }
    }
}
