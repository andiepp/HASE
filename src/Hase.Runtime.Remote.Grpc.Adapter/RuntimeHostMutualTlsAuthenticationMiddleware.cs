using Microsoft.AspNetCore.Http;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Authenticates the client certificate presented by the current HTTPS
/// connection before allowing the ASP.NET Core request pipeline to continue.
/// </summary>
public sealed class RuntimeHostMutualTlsAuthenticationMiddleware
{
    private readonly RequestDelegate next;
    private readonly RuntimeHostMutualTlsRequestAuthenticator
        requestAuthenticator;
    private readonly TimeProvider timeProvider;

    /// <summary>
    /// Initializes one runtime-host mutual-TLS authentication middleware.
    /// </summary>
    public RuntimeHostMutualTlsAuthenticationMiddleware(
        RequestDelegate next,
        RuntimeHostMutualTlsRequestAuthenticator requestAuthenticator,
        TimeProvider timeProvider)
    {
        this.next =
            next
            ?? throw new ArgumentNullException(
                nameof(next));
        this.requestAuthenticator =
            requestAuthenticator
            ?? throw new ArgumentNullException(
                nameof(requestAuthenticator));
        this.timeProvider =
            timeProvider
            ?? throw new ArgumentNullException(
                nameof(timeProvider));
    }

    /// <summary>
    /// Authenticates the current HTTPS request and invokes the next component
    /// only after successful mutual-TLS authentication.
    /// </summary>
    public async Task InvokeAsync(
        HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(
            httpContext);

        DateTimeOffset authenticatedAtUtc =
            timeProvider.GetUtcNow();

        RuntimeHostMutualTlsClientCertificateAuthenticationResult result =
            requestAuthenticator.Authenticate(
                httpContext,
                await httpContext.Connection.GetClientCertificateAsync(
                    httpContext.RequestAborted),
                authenticatedAtUtc);

        httpContext.Items[
            RuntimeHostMutualTlsHttpContextItems.AuthenticationResult] =
            result;

        if (!result.IsAccepted)
        {
            httpContext.Response.StatusCode =
                StatusCodes.Status401Unauthorized;
            return;
        }

        await next(
            httpContext);
    }
}
