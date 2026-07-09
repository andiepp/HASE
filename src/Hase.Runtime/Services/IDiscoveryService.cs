using Hase.Runtime.Runtime;

namespace Hase.Runtime.Services;

/// <summary>
/// Discovers endpoints and adds them to a runtime context.
/// </summary>
public interface IDiscoveryService
{
    Task DiscoverAsync(
        RuntimeContext context,
        CancellationToken cancellationToken = default);
}