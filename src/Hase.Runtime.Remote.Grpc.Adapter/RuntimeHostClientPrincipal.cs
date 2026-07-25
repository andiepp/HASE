namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Identifies one authenticated northbound client application independently
/// from the individual credential used for the current authentication.
/// </summary>
public sealed record RuntimeHostClientPrincipal
{
    /// <summary>
    /// Initializes one immutable authenticated client principal from the
    /// transport-independent authentication values.
    /// </summary>
    public RuntimeHostClientPrincipal(
        RuntimeHostClientPrincipalId principalIdentifier,
        RuntimeHostClientCredentialId credentialIdentifier,
        RuntimeHostAuthenticationMechanism authenticationMechanismValue,
        DateTimeOffset authenticatedAtUtc,
        string trustPolicyId)
    {
        if (principalIdentifier == default)
        {
            throw new ArgumentException(
                "The client-principal identifier must be specified.",
                nameof(principalIdentifier));
        }

        if (credentialIdentifier == default)
        {
            throw new ArgumentException(
                "The client-credential identifier must be specified.",
                nameof(credentialIdentifier));
        }

        if (authenticationMechanismValue == default)
        {
            throw new ArgumentException(
                "The authentication mechanism must be specified.",
                nameof(authenticationMechanismValue));
        }

        TrustPolicyId = RequireNonEmpty(
            trustPolicyId,
            nameof(trustPolicyId));

        if (authenticatedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "The authentication timestamp must use UTC.",
                nameof(authenticatedAtUtc));
        }

        PrincipalIdentifier = principalIdentifier;
        CredentialIdentifier = credentialIdentifier;
        AuthenticationMechanismValue = authenticationMechanismValue;
        AuthenticatedAtUtc = authenticatedAtUtc;
    }

    /// <summary>
    /// Initializes one immutable authenticated client principal while
    /// preserving the original string-based construction boundary.
    /// </summary>
    public RuntimeHostClientPrincipal(
        string principalId,
        string credentialId,
        string authenticationMechanism,
        DateTimeOffset authenticatedAtUtc,
        string trustPolicyId)
        : this(
            new RuntimeHostClientPrincipalId(
                principalId),
            new RuntimeHostClientCredentialId(
                credentialId),
            new RuntimeHostAuthenticationMechanism(
                authenticationMechanism),
            authenticatedAtUtc,
            trustPolicyId)
    {
    }

    /// <summary>
    /// Gets the stable typed HASE application-principal identifier.
    /// </summary>
    public RuntimeHostClientPrincipalId PrincipalIdentifier { get; }

    /// <summary>
    /// Gets the typed identifier of the individual credential used to
    /// authenticate.
    /// </summary>
    public RuntimeHostClientCredentialId CredentialIdentifier { get; }

    /// <summary>
    /// Gets the typed authentication mechanism that established the principal.
    /// </summary>
    public RuntimeHostAuthenticationMechanism AuthenticationMechanismValue
    {
        get;
    }

    /// <summary>
    /// Gets the stable HASE application-principal identifier.
    /// </summary>
    public string PrincipalId =>
        PrincipalIdentifier.Value;

    /// <summary>
    /// Gets the identifier of the individual credential used to authenticate.
    /// </summary>
    public string CredentialId =>
        CredentialIdentifier.Value;

    /// <summary>
    /// Gets the authentication mechanism that established the principal.
    /// </summary>
    public string AuthenticationMechanism =>
        AuthenticationMechanismValue.Value;

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
