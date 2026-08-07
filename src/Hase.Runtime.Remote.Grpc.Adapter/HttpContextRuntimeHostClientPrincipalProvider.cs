using System.Globalization;
using System.Security.Claims;
using Grpc.Core;
using Microsoft.AspNetCore.Http;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Reconstructs the authenticated HASE client principal from the claims
/// projected into the current ASP.NET Core request context.
/// </summary>
public sealed class HttpContextRuntimeHostClientPrincipalProvider
    : IRuntimeHostClientPrincipalProvider
{
    private readonly IHttpContextAccessor httpContextAccessor;

    public HttpContextRuntimeHostClientPrincipalProvider(
        IHttpContextAccessor httpContextAccessor)
    {
        this.httpContextAccessor = httpContextAccessor
            ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public RuntimeHostClientPrincipal GetPrincipal(ServerCallContext? context)
    {
        ArgumentNullException.ThrowIfNull(context);

        ClaimsPrincipal claimsPrincipal = httpContextAccessor.HttpContext?.User
            ?? throw new InvalidOperationException(
                "The gRPC request does not have an active HTTP context.");
        if (claimsPrincipal.Identity?.IsAuthenticated != true)
        {
            throw new InvalidOperationException(
                "The gRPC request does not contain an authenticated HASE principal.");
        }

        string principalId = ReadExactlyOne(
            claimsPrincipal,
            RuntimeHostClientClaimTypes.PrincipalId);
        string credentialId = ReadExactlyOne(
            claimsPrincipal,
            RuntimeHostClientClaimTypes.CredentialId);
        string mechanism = ReadExactlyOne(
            claimsPrincipal,
            RuntimeHostClientClaimTypes.AuthenticationMechanism);
        string authenticatedAtText = ReadExactlyOne(
            claimsPrincipal,
            RuntimeHostClientClaimTypes.AuthenticatedAtUtc);
        string trustPolicyId = ReadExactlyOne(
            claimsPrincipal,
            RuntimeHostClientClaimTypes.TrustPolicyId);

        if (!DateTimeOffset.TryParseExact(
                authenticatedAtText,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTimeOffset authenticatedAtUtc)
            || authenticatedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                "The authenticated HASE principal contains an invalid UTC timestamp.");
        }

        return new RuntimeHostClientPrincipal(
            principalId,
            credentialId,
            mechanism,
            authenticatedAtUtc,
            trustPolicyId);
    }

    private static string ReadExactlyOne(
        ClaimsPrincipal principal,
        string claimType)
    {
        Claim[] claims = principal.FindAll(claimType).ToArray();
        if (claims.Length != 1 || string.IsNullOrWhiteSpace(claims[0].Value))
        {
            throw new InvalidOperationException(
                "The authenticated HASE principal contains missing, duplicate, "
                + "or empty required claims.");
        }

        return claims[0].Value;
    }
}
