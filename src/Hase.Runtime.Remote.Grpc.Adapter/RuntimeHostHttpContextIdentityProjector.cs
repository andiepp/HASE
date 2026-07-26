using Microsoft.AspNetCore.Http;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Projects one authenticated HASE runtime-host client principal into the
/// current ASP.NET Core HTTP context.
/// </summary>
public sealed class RuntimeHostHttpContextIdentityProjector
{
    /// <summary>
    /// Assigns the authenticated HASE claims principal to
    /// <see cref="HttpContext.User"/>.
    /// </summary>
    public void Project(
        HttpContext httpContext,
        RuntimeHostClientPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(
            httpContext);
        ArgumentNullException.ThrowIfNull(
            principal);

        httpContext.User =
            RuntimeHostClaimsPrincipalFactory.Create(
                principal);
    }
}
