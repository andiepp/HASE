namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Identifies one transport-independent client credential by the
/// authentication mechanism that presents it and its credential identifier.
/// </summary>
public readonly record struct RuntimeHostClientCredentialIdentity
{
    /// <summary>
    /// Initializes one client-credential identity.
    /// </summary>
    public RuntimeHostClientCredentialIdentity(
        RuntimeHostAuthenticationMechanism authenticationMechanism,
        RuntimeHostClientCredentialId credentialId)
    {
        if (authenticationMechanism == default)
        {
            throw new ArgumentException(
                "The authentication mechanism must be specified.",
                nameof(authenticationMechanism));
        }

        if (credentialId == default)
        {
            throw new ArgumentException(
                "The client-credential identifier must be specified.",
                nameof(credentialId));
        }

        AuthenticationMechanism = authenticationMechanism;
        CredentialId = credentialId;
    }

    /// <summary>
    /// Gets the mechanism through which the credential is presented.
    /// </summary>
    public RuntimeHostAuthenticationMechanism AuthenticationMechanism
    {
        get;
    }

    /// <summary>
    /// Gets the identifier of the presented credential.
    /// </summary>
    public RuntimeHostClientCredentialId CredentialId { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{AuthenticationMechanism.Value}:{CredentialId.Value}";
    }
}
