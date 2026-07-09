using Hase.Core.Domain.Identity;

namespace Hase.Runtime.Runtime;

/// <summary>
/// Root object of a HASE runtime instance.
/// It maintains the live engineering model for one application.
/// </summary>
public sealed class RuntimeContext
{
    private readonly List<RuntimeEndpoint> _endpoints = [];

    /// <summary>
    /// Gets the runtime endpoints currently known to this context.
    /// </summary>
    public IReadOnlyList<RuntimeEndpoint> Endpoints => _endpoints;

    /// <summary>
    /// Adds an endpoint to the runtime.
    /// </summary>
    public void AddEndpoint(RuntimeEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        if (_endpoints.Any(e =>
                e.Descriptor.Id == endpoint.Descriptor.Id))
        {
            throw new InvalidOperationException(
                $"An endpoint with id '{endpoint.Descriptor.Id}' already exists.");
        }

        _endpoints.Add(endpoint);
    }

    /// <summary>
    /// Removes an endpoint from the runtime.
    /// </summary>
    public bool RemoveEndpoint(RuntimeEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        return _endpoints.Remove(endpoint);
    }

    public RuntimeEndpoint? FindEndpoint(EndpointId id)
    {
        ArgumentNullException.ThrowIfNull(id);

        return _endpoints.FirstOrDefault(
            endpoint => endpoint.Descriptor.Id == id);
    }

}