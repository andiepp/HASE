namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps one enrolled client credential to one stable HASE client principal
/// under one explicit trust policy.
/// </summary>
public sealed record RuntimeHostClientCredentialEnrollment
{
    /// <summary>
    /// Initializes one immutable credential enrollment.
    /// </summary>
    public RuntimeHostClientCredentialEnrollment(
        RuntimeHostClientCredentialIdentity credentialIdentity,
        RuntimeHostClientPrincipalId principalId,
        string trustPolicyId)
    {
        if (credentialIdentity == default)
        {
            throw new ArgumentException(
                "The client-credential identity must be specified.",
                nameof(credentialIdentity));
        }

        if (principalId == default)
        {
            throw new ArgumentException(
                "The client-principal identifier must be specified.",
                nameof(principalId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(
            trustPolicyId,
            nameof(trustPolicyId));

        CredentialIdentity = credentialIdentity;
        PrincipalId = principalId;
        TrustPolicyId = trustPolicyId;
    }

    /// <summary>
    /// Gets the enrolled credential identity.
    /// </summary>
    public RuntimeHostClientCredentialIdentity CredentialIdentity { get; }

    /// <summary>
    /// Gets the stable HASE client principal assigned to the credential.
    /// </summary>
    public RuntimeHostClientPrincipalId PrincipalId { get; }

    /// <summary>
    /// Gets the trust-policy identifier governing this enrollment.
    /// </summary>
    public string TrustPolicyId { get; }

    /// <summary>
    /// Creates the authenticated principal produced when this enrollment is
    /// accepted at the supplied UTC authentication time.
    /// </summary>
    public RuntimeHostClientPrincipal CreatePrincipal(
        DateTimeOffset authenticatedAtUtc)
    {
        return new RuntimeHostClientPrincipal(
            PrincipalId,
            CredentialIdentity.CredentialId,
            CredentialIdentity.AuthenticationMechanism,
            authenticatedAtUtc,
            TrustPolicyId);
    }
}
