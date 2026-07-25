namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class DefaultDenyRuntimeHostAuthorizationServiceTests
{
    [Fact]
    public void Authorize_AnyPrincipalAndPermission_ShouldDeny()
    {
        DefaultDenyRuntimeHostAuthorizationService service =
            new();

        RuntimeHostAuthorizationDecision decision =
            service.Authorize(
                CreatePrincipal(),
                RuntimeHostPermission.ReadSnapshot);

        Assert.False(decision.IsAllowed);
        Assert.Equal(
            DefaultDenyRuntimeHostAuthorizationService
                .DefaultDenialReason,
            decision.Reason);
    }

    [Theory]
    [MemberData(nameof(Permissions))]
    public void Authorize_EveryVersionOnePermission_ShouldDeny(
        RuntimeHostPermission permission)
    {
        DefaultDenyRuntimeHostAuthorizationService service =
            new();

        RuntimeHostAuthorizationDecision decision =
            service.Authorize(
                CreatePrincipal(),
                permission);

        Assert.False(decision.IsAllowed);
    }

    [Fact]
    public void Authorize_NullPrincipal_ShouldThrow()
    {
        DefaultDenyRuntimeHostAuthorizationService service =
            new();

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
        DefaultDenyRuntimeHostAuthorizationService service =
            new();

        Assert.Throws<ArgumentException>(
            "permission",
            () =>
                service.Authorize(
                    CreatePrincipal(),
                    default));
    }

    public static TheoryData<RuntimeHostPermission> Permissions =>
        new()
        {
            RuntimeHostPermission.ReadSnapshot,
            RuntimeHostPermission.ReadCachedProperty,
            RuntimeHostPermission.ReadAuthoritativeProperty,
            RuntimeHostPermission.WriteProperty,
            RuntimeHostPermission.ExecuteCommand,
            RuntimeHostPermission.SubscribeObservation
        };

    private static RuntimeHostClientPrincipal CreatePrincipal()
    {
        return new RuntimeHostClientPrincipal(
            "test-client",
            "test-credential",
            "test-authentication",
            new DateTimeOffset(
                2026,
                7,
                25,
                19,
                30,
                0,
                TimeSpan.Zero),
            "test-trust-policy");
    }
}
