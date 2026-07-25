namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostClientAuthenticationServiceTests
{
    [Fact]
    public void Constructor_NullRegistry_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "enrollmentRegistry",
            () =>
                new RuntimeHostClientAuthenticationService(
                    null!));
    }

    [Fact]
    public void Authenticate_KnownCredential_ShouldReturnAuthenticatedPrincipal()
    {
        RuntimeHostClientCredentialIdentity credentialIdentity =
            CreateCredentialIdentity();
        DateTimeOffset authenticatedAtUtc =
            new(
                2026,
                7,
                25,
                22,
                0,
                0,
                TimeSpan.Zero);
        RuntimeHostClientAuthenticationService service =
            new(
                new RuntimeHostClientCredentialEnrollmentRegistry(
                    [
                        new RuntimeHostClientCredentialEnrollment(
                            credentialIdentity,
                            new RuntimeHostClientPrincipalId(
                                "protocol-explorer"),
                            "development-trust-v1")
                    ]));

        RuntimeHostAuthenticationResult result =
            service.Authenticate(
                credentialIdentity,
                authenticatedAtUtc);

        Assert.True(
            result.IsAuthenticated);
        Assert.NotNull(
            result.Principal);
        Assert.Equal(
            "protocol-explorer",
            result.Principal.PrincipalId);
        Assert.Equal(
            "certificate-01",
            result.Principal.CredentialId);
        Assert.Equal(
            "mutual-tls",
            result.Principal.AuthenticationMechanism);
        Assert.Equal(
            authenticatedAtUtc,
            result.Principal.AuthenticatedAtUtc);
        Assert.Equal(
            "development-trust-v1",
            result.Principal.TrustPolicyId);
    }

    [Fact]
    public void Authenticate_UnknownCredential_ShouldFailClosed()
    {
        RuntimeHostClientAuthenticationService service =
            new(
                new RuntimeHostClientCredentialEnrollmentRegistry(
                    []));

        RuntimeHostAuthenticationResult result =
            service.Authenticate(
                CreateCredentialIdentity(),
                DateTimeOffset.UtcNow);

        Assert.False(
            result.IsAuthenticated);
        Assert.Null(
            result.Principal);
        Assert.Equal(
            RuntimeHostAuthenticationFailureReason.UnknownCredential,
            result.FailureReason);
    }

    [Fact]
    public void Authenticate_DefaultCredentialIdentity_ShouldThrow()
    {
        RuntimeHostClientAuthenticationService service =
            new(
                new RuntimeHostClientCredentialEnrollmentRegistry(
                    []));

        Assert.Throws<ArgumentException>(
            "credentialIdentity",
            () =>
                service.Authenticate(
                    default,
                    DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Authenticate_NonUtcTimestamp_ShouldThrowBeforeRegistry()
    {
        TrackingEnrollmentRegistry registry =
            new();
        RuntimeHostClientAuthenticationService service =
            new(
                registry);
        DateTimeOffset nonUtcTimestamp =
            new(
                2026,
                7,
                26,
                0,
                0,
                0,
                TimeSpan.FromHours(2));

        Assert.Throws<ArgumentException>(
            "authenticatedAtUtc",
            () =>
                service.Authenticate(
                    CreateCredentialIdentity(),
                    nonUtcTimestamp));

        Assert.False(
            registry.WasCalled);
    }

    [Fact]
    public void Authenticate_UnresolvedCredentialWithPrincipal_ShouldRejectRegistryViolation()
    {
        RuntimeHostClientAuthenticationService service =
            new(
                new InvalidUnresolvedEnrollmentRegistry());

        Assert.Throws<InvalidOperationException>(
            () =>
                service.Authenticate(
                    CreateCredentialIdentity(),
                    DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Authenticate_ResolvedCredentialWithoutPrincipal_ShouldRejectRegistryViolation()
    {
        RuntimeHostClientAuthenticationService service =
            new(
                new InvalidResolvedEnrollmentRegistry());

        Assert.Throws<InvalidOperationException>(
            () =>
                service.Authenticate(
                    CreateCredentialIdentity(),
                    DateTimeOffset.UtcNow));
    }

    private static RuntimeHostClientCredentialIdentity CreateCredentialIdentity()
    {
        return new RuntimeHostClientCredentialIdentity(
            RuntimeHostAuthenticationMechanism.MutualTls,
            new RuntimeHostClientCredentialId(
                "certificate-01"));
    }

    private sealed class TrackingEnrollmentRegistry
        : IRuntimeHostClientCredentialEnrollmentRegistry
    {
        public bool WasCalled { get; private set; }

        public bool TryResolve(
            RuntimeHostClientCredentialIdentity credentialIdentity,
            DateTimeOffset authenticatedAtUtc,
            out RuntimeHostClientPrincipal? principal)
        {
            WasCalled = true;
            principal = null;
            return false;
        }
    }

    private sealed class InvalidUnresolvedEnrollmentRegistry
        : IRuntimeHostClientCredentialEnrollmentRegistry
    {
        public bool TryResolve(
            RuntimeHostClientCredentialIdentity credentialIdentity,
            DateTimeOffset authenticatedAtUtc,
            out RuntimeHostClientPrincipal? principal)
        {
            principal =
                new RuntimeHostClientPrincipal(
                    "client-01",
                    "certificate-01",
                    "mutual-tls",
                    authenticatedAtUtc,
                    "trust-v1");
            return false;
        }
    }

    private sealed class InvalidResolvedEnrollmentRegistry
        : IRuntimeHostClientCredentialEnrollmentRegistry
    {
        public bool TryResolve(
            RuntimeHostClientCredentialIdentity credentialIdentity,
            DateTimeOffset authenticatedAtUtc,
            out RuntimeHostClientPrincipal? principal)
        {
            principal = null;
            return true;
        }
    }
}
