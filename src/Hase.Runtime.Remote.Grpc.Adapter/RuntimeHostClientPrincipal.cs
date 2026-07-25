namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Identifies one authenticated northbound client application independently
/// from the individual credential used for the current authentication.
/// </summary>
public sealed record RuntimeHostClientPrincipal
{
    /// <summary>
    /// Initializes one immutable authenticated client principal.
    /// </summary>
    public RuntimeHostClientPrincipal(
        string principalId,
        string credentialId,
        string authenticationMechanism,
        DateTimeOffset authenticatedAtUtc,
        string trustPolicyId)
    {
        PrincipalId = RequireNonEmpty(
            principalId,
            nameof(principalId));
        CredentialId = RequireNonEmpty(
            credentialId,
            nameof(credentialId));
        AuthenticationMechanism = RequireNonEmpty(
            authenticationMechanism,
            nameof(authenticationMechanism));
        TrustPolicyId = RequireNonEmpty(
            trustPolicyId,
            nameof(trustPolicyId));

        if (authenticatedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "The authentication timestamp must use UTC.",
                nameof(authenticatedAtUtc));
        }

        AuthenticatedAtUtc = authenticatedAtUtc;
    }

    /// <summary>
    /// Gets the stable HASE application-principal identifier.
    /// </summary>
    public string PrincipalId { get; }

    /// <summary>
    /// Gets the identifier of the individual credential used to authenticate.
    /// </summary>
    public string CredentialId { get; }

    /// <summary>
    /// Gets the authentication mechanism that established the principal.
    /// </summary>
    public string AuthenticationMechanism { get; }

    /// <summary>
    /// Gets the UTC time at which authentication completed.
    /// </summary>
    public DateTimeOffset AuthenticatedAtUtc { get; }

    /// <summary>
    /// Gets the trust-policy identifier used during authentication.
    /// </summary>
    public string TrustPolicyId { get; }

    private static string RequireNonEmpty(
        string value,
        string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            value,
            parameterName);

        return value;
    }
}
