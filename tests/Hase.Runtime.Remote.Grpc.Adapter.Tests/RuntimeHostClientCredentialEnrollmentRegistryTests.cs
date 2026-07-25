namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostClientCredentialEnrollmentRegistryTests
{
    [Fact]
    public void Constructor_DuplicateCredentialIdentity_ShouldThrow()
    {
        RuntimeHostClientCredentialIdentity credentialIdentity =
            CreateCredentialIdentity(
                "certificate-01");

        RuntimeHostClientCredentialEnrollment first =
            CreateEnrollment(
                credentialIdentity,
                "client-01");
        RuntimeHostClientCredentialEnrollment second =
            CreateEnrollment(
                credentialIdentity,
                "client-02");

        Assert.Throws<ArgumentException>(
            "enrollments",
            () =>
                new RuntimeHostClientCredentialEnrollmentRegistry(
                    [
                        first,
                        second
                    ]));
    }

    [Fact]
    public void Constructor_NullEnrollment_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "enrollments",
            () =>
                new RuntimeHostClientCredentialEnrollmentRegistry(
                    [
                        null!
                    ]));
    }

    [Fact]
    public void TryResolve_KnownCredential_ShouldReturnPrincipal()
    {
        RuntimeHostClientCredentialIdentity credentialIdentity =
            CreateCredentialIdentity(
                "certificate-01");
        RuntimeHostClientCredentialEnrollmentRegistry registry =
            new(
                [
                    CreateEnrollment(
                        credentialIdentity,
                        "client-01")
                ]);
        DateTimeOffset authenticatedAtUtc =
            new(
                2026,
                7,
                25,
                21,
                0,
                0,
                TimeSpan.Zero);

        bool resolved =
            registry.TryResolve(
                credentialIdentity,
                authenticatedAtUtc,
                out RuntimeHostClientPrincipal? principal);

        Assert.True(
            resolved);
        Assert.NotNull(
            principal);
        Assert.Equal(
            "client-01",
            principal.PrincipalId);
        Assert.Equal(
            "certificate-01",
            principal.CredentialId);
        Assert.Equal(
            authenticatedAtUtc,
            principal.AuthenticatedAtUtc);
    }

    [Fact]
    public void TryResolve_UnknownCredential_ShouldFailClosed()
    {
        RuntimeHostClientCredentialEnrollmentRegistry registry =
            new(
                [
                    CreateEnrollment(
                        CreateCredentialIdentity(
                            "certificate-01"),
                        "client-01")
                ]);

        bool resolved =
            registry.TryResolve(
                CreateCredentialIdentity(
                    "certificate-02"),
                DateTimeOffset.UtcNow,
                out RuntimeHostClientPrincipal? principal);

        Assert.False(
            resolved);
        Assert.Null(
            principal);
    }

    [Fact]
    public void TryResolve_SameCredentialIdUnderDifferentMechanism_ShouldRemainDistinct()
    {
        RuntimeHostClientCredentialId credentialId =
            new(
                "credential-01");
        RuntimeHostClientCredentialIdentity mutualTlsIdentity =
            new(
                RuntimeHostAuthenticationMechanism.MutualTls,
                credentialId);
        RuntimeHostClientCredentialIdentity loopbackIdentity =
            new(
                RuntimeHostAuthenticationMechanism.TrustedLoopback,
                credentialId);
        RuntimeHostClientCredentialEnrollmentRegistry registry =
            new(
                [
                    CreateEnrollment(
                        mutualTlsIdentity,
                        "remote-client"),
                    CreateEnrollment(
                        loopbackIdentity,
                        "local-client")
                ]);

        Assert.True(
            registry.TryResolve(
                mutualTlsIdentity,
                DateTimeOffset.UtcNow,
                out RuntimeHostClientPrincipal? remotePrincipal));
        Assert.True(
            registry.TryResolve(
                loopbackIdentity,
                DateTimeOffset.UtcNow,
                out RuntimeHostClientPrincipal? localPrincipal));
        Assert.Equal(
            "remote-client",
            remotePrincipal!.PrincipalId);
        Assert.Equal(
            "local-client",
            localPrincipal!.PrincipalId);
    }

    [Fact]
    public void TryResolve_DefaultCredentialIdentity_ShouldThrow()
    {
        RuntimeHostClientCredentialEnrollmentRegistry registry =
            new(
                []);

        Assert.Throws<ArgumentException>(
            "credentialIdentity",
            () =>
                registry.TryResolve(
                    default,
                    DateTimeOffset.UtcNow,
                    out _));
    }

    [Fact]
    public void TryResolve_NonUtcTimestamp_ShouldThrow()
    {
        RuntimeHostClientCredentialEnrollmentRegistry registry =
            new(
                []);
        DateTimeOffset nonUtcTimestamp =
            new(
                2026,
                7,
                25,
                23,
                0,
                0,
                TimeSpan.FromHours(2));

        Assert.Throws<ArgumentException>(
            "authenticatedAtUtc",
            () =>
                registry.TryResolve(
                    CreateCredentialIdentity(
                        "certificate-01"),
                    nonUtcTimestamp,
                    out _));
    }

    private static RuntimeHostClientCredentialEnrollment CreateEnrollment(
        RuntimeHostClientCredentialIdentity credentialIdentity,
        string principalId)
    {
        return new RuntimeHostClientCredentialEnrollment(
            credentialIdentity,
            new RuntimeHostClientPrincipalId(
                principalId),
            "development-trust-v1");
    }

    private static RuntimeHostClientCredentialIdentity CreateCredentialIdentity(
        string credentialId)
    {
        return new RuntimeHostClientCredentialIdentity(
            RuntimeHostAuthenticationMechanism.MutualTls,
            new RuntimeHostClientCredentialId(
                credentialId));
    }
}
