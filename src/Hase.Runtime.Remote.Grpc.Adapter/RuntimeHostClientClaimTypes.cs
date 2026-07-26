namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Defines the stable claim types used to project one authenticated HASE
/// runtime-host client principal into ASP.NET Core.
/// </summary>
public static class RuntimeHostClientClaimTypes
{
    /// <summary>
    /// Gets the claim type containing the stable HASE client-principal
    /// identifier.
    /// </summary>
    public const string PrincipalId =
        "urn:hase:runtime-host:client:principal-id";

    /// <summary>
    /// Gets the claim type containing the individual credential identifier.
    /// </summary>
    public const string CredentialId =
        "urn:hase:runtime-host:client:credential-id";

    /// <summary>
    /// Gets the claim type containing the authentication mechanism.
    /// </summary>
    public const string AuthenticationMechanism =
        "urn:hase:runtime-host:client:authentication-mechanism";

    /// <summary>
    /// Gets the claim type containing the UTC authentication timestamp.
    /// </summary>
    public const string AuthenticatedAtUtc =
        "urn:hase:runtime-host:client:authenticated-at-utc";

    /// <summary>
    /// Gets the claim type containing the trust-policy identifier.
    /// </summary>
    public const string TrustPolicyId =
        "urn:hase:runtime-host:client:trust-policy-id";
}
