namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostAuthorizationPolicyTests
{
    [Fact]
    public void Constructor_NullGrants_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "grants",
            () =>
                new RuntimeHostAuthorizationPolicy(
                    null!));
    }

    [Fact]
    public void IsGranted_ExactGrant_ShouldReturnTrue()
    {
        RuntimeHostAuthorizationPolicy policy =
            new(
                [
                    new RuntimeHostPermissionGrant(
                        "client-01",
                        RuntimeHostPermission.ReadSnapshot)
                ]);

        bool granted =
            policy.IsGranted(
                "client-01",
                RuntimeHostPermission.ReadSnapshot);

        Assert.True(granted);
    }

    [Fact]
    public void IsGranted_UnknownPrincipal_ShouldReturnFalse()
    {
        RuntimeHostAuthorizationPolicy policy =
            new(
                [
                    new RuntimeHostPermissionGrant(
                        "client-01",
                        RuntimeHostPermission.ReadSnapshot)
                ]);

        bool granted =
            policy.IsGranted(
                "client-02",
                RuntimeHostPermission.ReadSnapshot);

        Assert.False(granted);
    }

    [Fact]
    public void IsGranted_MissingPermission_ShouldReturnFalse()
    {
        RuntimeHostAuthorizationPolicy policy =
            new(
                [
                    new RuntimeHostPermissionGrant(
                        "client-01",
                        RuntimeHostPermission.ReadSnapshot)
                ]);

        bool granted =
            policy.IsGranted(
                "client-01",
                RuntimeHostPermission.ExecuteCommand);

        Assert.False(granted);
    }

    [Fact]
    public void IsGranted_CredentialIdentity_ShouldNotAffectGrant()
    {
        RuntimeHostAuthorizationPolicy policy =
            new(
                [
                    new RuntimeHostPermissionGrant(
                        "client-01",
                        RuntimeHostPermission.ReadSnapshot)
                ]);

        RuntimeHostClientPrincipal rotatedCredentialPrincipal =
            new(
                "client-01",
                "credential-02",
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

        bool granted =
            policy.IsGranted(
                rotatedCredentialPrincipal.PrincipalId,
                RuntimeHostPermission.ReadSnapshot);

        Assert.True(granted);
    }

    [Fact]
    public void Constructor_DuplicateGrants_ShouldRemainGranted()
    {
        RuntimeHostPermissionGrant grant =
            new(
                "client-01",
                RuntimeHostPermission.ReadSnapshot);

        RuntimeHostAuthorizationPolicy policy =
            new(
                [
                    grant,
                    grant
                ]);

        Assert.True(
            policy.IsGranted(
                "client-01",
                RuntimeHostPermission.ReadSnapshot));
    }
}
