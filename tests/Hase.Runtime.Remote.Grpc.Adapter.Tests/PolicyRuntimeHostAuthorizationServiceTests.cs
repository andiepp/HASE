namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class PolicyRuntimeHostAuthorizationServiceTests
{
    [Fact]
    public void Constructor_NullPolicy_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "policy",
            () =>
                new PolicyRuntimeHostAuthorizationService(
                    null!));
    }

    [Fact]
    public void Authorize_ExactGrant_ShouldAllow()
    {
        PolicyRuntimeHostAuthorizationService service =
            CreateService(
                new RuntimeHostPermissionGrant(
                    "client-01",
                    RuntimeHostPermission.ReadSnapshot));

        RuntimeHostAuthorizationDecision decision =
            service.Authorize(
                CreatePrincipal(
                    "client-01",
                    "credential-01"),
                RuntimeHostPermission.ReadSnapshot);

        Assert.True(decision.IsAllowed);
        Assert.Equal(
            PolicyRuntimeHostAuthorizationService.AllowedReason,
            decision.Reason);
    }

    [Fact]
    public void Authorize_UnknownPrincipal_ShouldDeny()
    {
        PolicyRuntimeHostAuthorizationService service =
            CreateService(
                new RuntimeHostPermissionGrant(
                    "client-01",
                    RuntimeHostPermission.ReadSnapshot));

        RuntimeHostAuthorizationDecision decision =
            service.Authorize(
                CreatePrincipal(
                    "client-02",
                    "credential-02"),
                RuntimeHostPermission.ReadSnapshot);

        Assert.False(decision.IsAllowed);
        Assert.Equal(
            PolicyRuntimeHostAuthorizationService.DeniedReason,
            decision.Reason);
    }

    [Fact]
    public void Authorize_MissingPermission_ShouldDeny()
    {
        PolicyRuntimeHostAuthorizationService service =
            CreateService(
                new RuntimeHostPermissionGrant(
                    "client-01",
                    RuntimeHostPermission.ReadSnapshot));

        RuntimeHostAuthorizationDecision decision =
            service.Authorize(
                CreatePrincipal(
                    "client-01",
                    "credential-01"),
                RuntimeHostPermission.ExecuteCommand);

        Assert.False(decision.IsAllowed);
    }

    [Fact]
    public void Authorize_RotatedCredentialForSamePrincipal_ShouldAllow()
    {
        PolicyRuntimeHostAuthorizationService service =
            CreateService(
                new RuntimeHostPermissionGrant(
                    "client-01",
                    RuntimeHostPermission.ReadSnapshot));

        RuntimeHostAuthorizationDecision decision =
            service.Authorize(
                CreatePrincipal(
                    "client-01",
                    "credential-rotated"),
                RuntimeHostPermission.ReadSnapshot);

        Assert.True(decision.IsAllowed);
    }

    [Fact]
    public void Authorize_NullPrincipal_ShouldThrow()
    {
        PolicyRuntimeHostAuthorizationService service =
            CreateService();

        Assert.Throws<ArgumentNullException>(
            "principal",
            () =>
                service.Authorize(
                    null!,
                    RuntimeHostPermission.ReadSnapshot));
    }

    [Fact]
    public void Authorize_DefaultPermission_ShouldThrow()
    {
        PolicyRuntimeHostAuthorizationService service =
            CreateService();

        Assert.Throws<ArgumentException>(
            "permission",
            () =>
                service.Authorize(
                    CreatePrincipal(
                        "client-01",
                        "credential-01"),
                    default));
    }

    private static PolicyRuntimeHostAuthorizationService CreateService(
        params RuntimeHostPermissionGrant[] grants)
    {
        return new PolicyRuntimeHostAuthorizationService(
            new RuntimeHostAuthorizationPolicy(
                grants));
    }

    private static RuntimeHostClientPrincipal CreatePrincipal(
        string principalId,
        string credentialId)
    {
        return new RuntimeHostClientPrincipal(
            principalId,
            credentialId,
            "mutual-tls",
            new DateTimeOffset(
                2026,
                7,
                25,
                20,
                0,
                0,
                TimeSpan.Zero),
            "trust-v1");
    }
}
