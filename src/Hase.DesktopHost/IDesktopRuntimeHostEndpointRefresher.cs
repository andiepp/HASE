namespace Hase.DesktopHost;

/// <summary>
/// Performs one explicit operator-requested search for configured physical
/// endpoints that are not currently published by the Runtime Host.
/// </summary>
public interface IDesktopRuntimeHostEndpointRefresher
{
    /// <summary>
    /// Searches for and authoritatively attaches eligible configured
    /// endpoints without disturbing existing attachments.
    /// </summary>
    Task RefreshEndpointsAsync(
        CancellationToken cancellationToken = default);
}
