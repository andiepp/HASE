using System.Globalization;
using System.Security.Claims;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Projects one authenticated HASE runtime-host client principal into the
/// standard ASP.NET Core claims model.
/// </summary>
public static class RuntimeHostClaimsPrincipalFactory
{
    /// <summary>
    /// Creates the claims principal for one authenticated HASE client.
    /// </summary>
    public static ClaimsPrincipal Create(
        RuntimeHostClientPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(
            principal);

        Claim[] claims =
        [
            new Claim(
                RuntimeHostClientClaimTypes.PrincipalId,
                principal.PrincipalId),
            new Claim(
                RuntimeHostClientClaimTypes.CredentialId,
                principal.CredentialId),
            new Claim(
                RuntimeHostClientClaimTypes.AuthenticationMechanism,
                principal.AuthenticationMechanism),
            new Claim(
                RuntimeHostClientClaimTypes.AuthenticatedAtUtc,
                principal.AuthenticatedAtUtc.ToString(
                    "O",
                    CultureInfo.InvariantCulture)),
            new Claim(
                RuntimeHostClientClaimTypes.TrustPolicyId,
                principal.TrustPolicyId)
        ];

        ClaimsIdentity identity =
            new(
                claims,
                RuntimeHostMutualTlsAuthenticationDefaults
                    .AuthenticationScheme,
                RuntimeHostClientClaimTypes.PrincipalId,
                ClaimsIdentity.DefaultRoleClaimType);

        return new ClaimsPrincipal(
            identity);
    }
}
