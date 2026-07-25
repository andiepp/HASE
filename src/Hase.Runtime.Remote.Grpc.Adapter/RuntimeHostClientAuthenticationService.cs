namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Resolves validated client credentials through the configured enrollment
/// registry and produces transport-independent authentication results.
/// </summary>
public sealed class RuntimeHostClientAuthenticationService
    : IRuntimeHostClientAuthenticationService
{
    private readonly IRuntimeHostClientCredentialEnrollmentRegistry
        enrollmentRegistry;

    /// <summary>
    /// Initializes the authentication service.
    /// </summary>
    public RuntimeHostClientAuthenticationService(
        IRuntimeHostClientCredentialEnrollmentRegistry enrollmentRegistry)
    {
        this.enrollmentRegistry =
            enrollmentRegistry
            ?? throw new ArgumentNullException(
                nameof(enrollmentRegistry));
    }

    /// <inheritdoc />
    public RuntimeHostAuthenticationResult Authenticate(
        RuntimeHostClientCredentialIdentity credentialIdentity,
        DateTimeOffset authenticatedAtUtc)
    {
        if (credentialIdentity == default)
        {
            throw new ArgumentException(
                "The client-credential identity must be specified.",
                nameof(credentialIdentity));
        }

        if (authenticatedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "The authentication timestamp must use UTC.",
                nameof(authenticatedAtUtc));
        }

        bool resolved =
            enrollmentRegistry.TryResolve(
                credentialIdentity,
                authenticatedAtUtc,
                out RuntimeHostClientPrincipal? principal);

        if (!resolved)
        {
            if (principal is not null)
            {
                throw new InvalidOperationException(
                    "The enrollment registry returned a principal for "
                    + "an unresolved credential.");
            }

            return RuntimeHostAuthenticationResult.Failed(
                RuntimeHostAuthenticationFailureReason.UnknownCredential);
        }

        return RuntimeHostAuthenticationResult.Authenticated(
            principal
            ?? throw new InvalidOperationException(
                "The enrollment registry resolved a credential without "
                + "returning a principal."));
    }
}
