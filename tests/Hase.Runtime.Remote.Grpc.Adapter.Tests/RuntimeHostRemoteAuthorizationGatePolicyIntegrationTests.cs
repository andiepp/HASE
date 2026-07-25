namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostRemoteAuthorizationGatePolicyIntegrationTests
{
    [Fact]
    public void Authorize_GrantedOperation_ShouldAllow()
    {
        RuntimeHostRemoteAuthorizationGate gate =
            CreateGate(
                new RuntimeHostPermissionGrant(
                    "client-01",
                    RuntimeHostPermission.ExecuteCommand));

        RuntimeHostAuthorizationDecision decision =
            gate.Authorize(
                CreatePrincipal(
                    "client-01"),
                RuntimeHostRemoteOperation.ExecuteCommand);

        Assert.True(decision.IsAllowed);
    }

    [Fact]
    public void Authorize_DifferentOperation_ShouldDeny()
    {
        RuntimeHostRemoteAuthorizationGate gate =
            CreateGate(
                new RuntimeHostPermissionGrant(
                    "client-01",
                    RuntimeHostPermission.ReadSnapshot));

        RuntimeHostAuthorizationDecision decision =
            gate.Authorize(
                CreatePrincipal(
                    "client-01"),
                RuntimeHostRemoteOperation.ExecuteCommand);

        Assert.False(decision.IsAllowed);
    }

    [Fact]
    public void Authorize_UnknownPrincipal_ShouldDeny()
    {
        RuntimeHostRemoteAuthorizationGate gate =
            CreateGate(
                new RuntimeHostPermissionGrant(
                    "client-01",
                    RuntimeHostPermission.ReadSnapshot));

        RuntimeHostAuthorizationDecision decision =
            gate.Authorize(
                CreatePrincipal(
                    "client-02"),
                RuntimeHostRemoteOperation.GetSnapshot);

        Assert.False(decision.IsAllowed);
    }

    [Fact]
    public void Authorize_DefaultDenyService_ShouldDenyEveryOperation()
    {
        RuntimeHostRemoteAuthorizationGate gate =
            new(
                new RuntimeHostRemoteOperationPermissionMapper(),
                new DefaultDenyRuntimeHostAuthorizationService());

        RuntimeHostClientPrincipal principal =
            CreatePrincipal(
                "client-01");

        foreach (
            RuntimeHostRemoteOperation operation
            in Enum.GetValues<RuntimeHostRemoteOperation>()
                .Where(
                    value =>
                        value
                        != RuntimeHostRemoteOperation.Unspecified))
        {
            RuntimeHostAuthorizationDecision decision =
                gate.Authorize(
                    principal,
                    operation);

            Assert.False(decision.IsAllowed);
        }
    }

    private static RuntimeHostRemoteAuthorizationGate CreateGate(
        params RuntimeHostPermissionGrant[] grants)
    {
        RuntimeHostAuthorizationPolicy policy =
            new(
                grants);

        return new RuntimeHostRemoteAuthorizationGate(
            new RuntimeHostRemoteOperationPermissionMapper(),
            new PolicyRuntimeHostAuthorizationService(
                policy));
    }

    private static RuntimeHostClientPrincipal CreatePrincipal(
        string principalId)
    {
        return new RuntimeHostClientPrincipal(
            principalId,
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
}
