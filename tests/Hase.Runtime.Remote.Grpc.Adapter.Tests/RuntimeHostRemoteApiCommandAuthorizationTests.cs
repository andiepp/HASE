using Grpc.Core;
using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostRemoteApiCommandAuthorizationTests
{
    [Fact]
    public async Task ExecuteCommand_Denied_ShouldFailBeforeConfiguration()
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
                    service.ExecuteCommand(
                        new GrpcV1.ExecuteCommandRequest(),
                        null!));

        Assert.Equal(
            StatusCode.PermissionDenied,
            exception.StatusCode);
        Assert.Equal(
            RuntimeHostRemoteOperation.ExecuteCommand,
            authorizationGate.ObservedOperation);
    }

    [Fact]
    public async Task ExecuteCommand_Allowed_ShouldContinueToOperation()
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
                    service.ExecuteCommand(
                        new GrpcV1.ExecuteCommandRequest(),
                        null!));

        Assert.Equal(
            "Command execution is not configured.",
            exception.Message);
        Assert.Equal(
            RuntimeHostRemoteOperation.ExecuteCommand,
            authorizationGate.ObservedOperation);
    }

    [Fact]
    public async Task ExecuteCommand_NoAuthorizationConfiguration_ShouldPreserveBehavior()
    {
        RuntimeHostRemoteApiService service =
            new(
                new TestSnapshotProvider(),
                RuntimeHostSnapshotMapperFactory.Create());

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    service.ExecuteCommand(
                        new GrpcV1.ExecuteCommandRequest(),
                        null!));

        Assert.Equal(
            "Command execution is not configured.",
            exception.Message);
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
                0,
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
}
