using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Http;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Authenticates one presented mutual-TLS client certificate through C-030 and
/// projects the authenticated HASE principal into the current HTTP context.
/// </summary>
public sealed class RuntimeHostMutualTlsRequestAuthenticator
{
    private readonly RuntimeHostMutualTlsClientCertificateAuthenticator
        certificateAuthenticator;
    private readonly RuntimeHostHttpContextIdentityProjector identityProjector;

    /// <summary>
    /// Initializes one request-level mutual-TLS authentication integration.
    /// </summary>
    public RuntimeHostMutualTlsRequestAuthenticator(
        RuntimeHostMutualTlsClientCertificateAuthenticator
            certificateAuthenticator,
        RuntimeHostHttpContextIdentityProjector identityProjector)
    {
        this.certificateAuthenticator =
            certificateAuthenticator
            ?? throw new ArgumentNullException(
                nameof(certificateAuthenticator));
        this.identityProjector =
            identityProjector
            ?? throw new ArgumentNullException(
                nameof(identityProjector));
    }

    /// <summary>
    /// Authenticates the presented certificate and projects the resulting
    /// principal into <see cref="HttpContext.User"/> only when authentication
    /// succeeds.
    /// </summary>
    public RuntimeHostMutualTlsClientCertificateAuthenticationResult
        Authenticate(
            HttpContext httpContext,
            X509Certificate2? certificate,
            DateTimeOffset authenticatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(
            httpContext);

        RuntimeHostMutualTlsClientCertificateAuthenticationResult result =
            certificateAuthenticator.Authenticate(
                certificate,
                authenticatedAtUtc);

        if (!result.IsAccepted)
        {
            return result;
        }

        RuntimeHostClientPrincipal principal =
            result.Principal
            ?? throw new InvalidOperationException(
                "Mutual-TLS certificate authentication reported acceptance "
                + "without a principal.");

        identityProjector.Project(
            httpContext,
            principal);

        return result;
    }
}
