using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;

namespace Hase.Runtime.Runtime;

public sealed class RuntimeContext
{
    private readonly List<RuntimeEndpoint> _endpoints = [];

    public IReadOnlyList<RuntimeEndpoint> Endpoints => _endpoints;

    public RuntimeEndpoint AddEndpoint(EndpointDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (_endpoints.Any(e => e.Descriptor.Id == descriptor.Id))
        {
            throw new InvalidOperationException(
                $"An endpoint with id '{descriptor.Id}' already exists.");
        }

        var endpoint = new RuntimeEndpoint(this, descriptor);
        _endpoints.Add(endpoint);

        return endpoint;
    }

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