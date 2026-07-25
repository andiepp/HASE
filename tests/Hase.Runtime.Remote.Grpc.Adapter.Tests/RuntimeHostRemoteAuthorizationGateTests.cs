namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostRemoteAuthorizationGateTests
{
    [Fact]
    public void Constructor_NullPermissionMapper_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "permissionMapper",
            () =>
                new RuntimeHostRemoteAuthorizationGate(
                    null!,
                    new TestAuthorizationService(
                        RuntimeHostAuthorizationDecision.Deny(
                            "Denied."))));
    }

    [Fact]
    public void Constructor_NullAuthorizationService_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "authorizationService",
            () =>
                new RuntimeHostRemoteAuthorizationGate(
                    new TestPermissionMapper(
                        RuntimeHostPermission.ReadSnapshot),
                    null!));
    }

    [Fact]
    public void Authorize_ShouldMapOperationAndEvaluatePermission()
    {
        TestPermissionMapper permissionMapper =
            new(
                RuntimeHostPermission.ExecuteCommand);

        TestAuthorizationService authorizationService =
            new(
                RuntimeHostAuthorizationDecision.Allow(
                    "Granted."));

        RuntimeHostRemoteAuthorizationGate gate =
            new(
                permissionMapper,
                authorizationService);

        RuntimeHostClientPrincipal principal =
            CreatePrincipal();

        RuntimeHostAuthorizationDecision decision =
            gate.Authorize(
                principal,
                RuntimeHostRemoteOperation.ExecuteCommand);

        Assert.True(decision.IsAllowed);
        Assert.Same(
            principal,
            authorizationService.ObservedPrincipal);
        Assert.Equal(
            RuntimeHostPermission.ExecuteCommand,
            authorizationService.ObservedPermission);
        Assert.Equal(
            RuntimeHostRemoteOperation.ExecuteCommand,
            permissionMapper.ObservedOperation);
    }

    [Fact]
    public void Authorize_DeniedPolicy_ShouldPreserveDecision()
    {
        RuntimeHostAuthorizationDecision expectedDecision =
            RuntimeHostAuthorizationDecision.Deny(
                "No matching grant.");

        RuntimeHostRemoteAuthorizationGate gate =
            new(
                new TestPermissionMapper(
                    RuntimeHostPermission.WriteProperty),
                new TestAuthorizationService(
                    expectedDecision));

        RuntimeHostAuthorizationDecision decision =
            gate.Authorize(
                CreatePrincipal(),
                RuntimeHostRemoteOperation.WriteProperty);

        Assert.Same(
            expectedDecision,
            decision);
    }

    [Fact]
    public void Authorize_NullPrincipal_ShouldThrowBeforeMapping()
    {
        TestPermissionMapper permissionMapper =
            new(
                RuntimeHostPermission.ReadSnapshot);

        RuntimeHostRemoteAuthorizationGate gate =
            new(
                permissionMapper,
                new TestAuthorizationService(
                    RuntimeHostAuthorizationDecision.Deny(
                        "Denied.")));

        Assert.Throws<ArgumentNullException>(
            "principal",
            () =>
                gate.Authorize(
                    null!,
                    RuntimeHostRemoteOperation.GetSnapshot));

        Assert.Null(
            permissionMapper.ObservedOperation);
    }

    [Fact]
    public void Authorize_UnspecifiedOperation_ShouldFailClosed()
    {
        RuntimeHostRemoteAuthorizationGate gate =
            new(
                new RuntimeHostRemoteOperationPermissionMapper(),
                new TestAuthorizationService(
                    RuntimeHostAuthorizationDecision.Allow(
                        "Should not be reached.")));

        Assert.Throws<ArgumentOutOfRangeException>(
            "operation",
            () =>
                gate.Authorize(
                    CreatePrincipal(),
                    RuntimeHostRemoteOperation.Unspecified));
    }

    [Theory]
    [MemberData(nameof(OperationMappings))]
    public void Authorize_EveryVersionOneOperation_ShouldUseExpectedPermission(
        RuntimeHostRemoteOperation operation,
        RuntimeHostPermission expectedPermission)
    {
        TestAuthorizationService authorizationService =
            new(
                RuntimeHostAuthorizationDecision.Deny(
                    "Observed."));

        RuntimeHostRemoteAuthorizationGate gate =
            new(
                new RuntimeHostRemoteOperationPermissionMapper(),
                authorizationService);

        gate.Authorize(
            CreatePrincipal(),
            operation);

        Assert.Equal(
            expectedPermission,
            authorizationService.ObservedPermission);
    }

    public static TheoryData<
        RuntimeHostRemoteOperation,
        RuntimeHostPermission> OperationMappings =>
        new()
        {
            {
                RuntimeHostRemoteOperation.GetSnapshot,
                RuntimeHostPermission.ReadSnapshot
            },
            {
                RuntimeHostRemoteOperation.ReadCachedProperty,
                RuntimeHostPermission.ReadCachedProperty
            },
            {
                RuntimeHostRemoteOperation.ReadAuthoritativeProperty,
                RuntimeHostPermission.ReadAuthoritativeProperty
            },
            {
                RuntimeHostRemoteOperation.WriteProperty,
                RuntimeHostPermission.WriteProperty
            },
            {
                RuntimeHostRemoteOperation.ExecuteCommand,
                RuntimeHostPermission.ExecuteCommand
            },
            {
                RuntimeHostRemoteOperation.Observe,
                RuntimeHostPermission.SubscribeObservation
            }
        };

    private static RuntimeHostClientPrincipal CreatePrincipal()
    {
        return new RuntimeHostClientPrincipal(
            "client-01",
            "credential-01",
            "mutual-tls",
            new DateTimeOffset(
                2026,
                7,
                25,
                20,
                30,
                0,
                TimeSpan.Zero),
            "trust-v1");
    }

    private sealed class TestPermissionMapper
        : IRuntimeHostRemoteOperationPermissionMapper
    {
        private readonly RuntimeHostPermission permission;

        public TestPermissionMapper(
            RuntimeHostPermission permission)
        {
            this.permission = permission;
        }

        public RuntimeHostRemoteOperation? ObservedOperation
        {
            get;
            private set;
        }

        public RuntimeHostPermission Map(
            RuntimeHostRemoteOperation operation)
        {
            ObservedOperation = operation;
            return permission;
        }
    }

    private sealed class TestAuthorizationService
        : IRuntimeHostAuthorizationService
    {
        private readonly RuntimeHostAuthorizationDecision decision;

        public TestAuthorizationService(
            RuntimeHostAuthorizationDecision decision)
        {
            this.decision = decision;
        }

        public RuntimeHostClientPrincipal? ObservedPrincipal
        {
            get;
            private set;
        }

        public RuntimeHostPermission ObservedPermission
        {
            get;
            private set;
        }

        public RuntimeHostAuthorizationDecision Authorize(
            RuntimeHostClientPrincipal principal,
            RuntimeHostPermission permission)
        {
            ObservedPrincipal = principal;
            ObservedPermission = permission;
            return decision;
        }
    }
}
