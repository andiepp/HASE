using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;

namespace Hase.Runtime.Runtime;

/// <summary>
/// Root object of a HASE runtime instance.
/// It maintains the live engineering model for one application.
/// </summary>
public sealed class RuntimeContext : IRuntimeNode, IPropertyValueObserver
{
    private readonly List<RuntimeEndpoint> _endpoints = [];
    private readonly List<IPropertyValueObserver> _observers = [];

    public IReadOnlyList<RuntimeEndpoint> Endpoints => _endpoints;

    public IRuntimeNode? Parent => null;

    public IReadOnlyList<IRuntimeNode> Children =>
        _endpoints.Cast<IRuntimeNode>().ToArray();

    public RuntimeEndpoint AddEndpoint(EndpointDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (_endpoints.Any(e => e.Descriptor.Id == descriptor.Id))
        {
            throw new InvalidOperationException(
                $"An endpoint with id '{descriptor.Id}' already exists.");
        }

        var endpoint = new RuntimeEndpoint(this, descriptor);

        endpoint.Subscribe(this);

        _endpoints.Add(endpoint);

        return endpoint;
    }

    public bool RemoveEndpoint(RuntimeEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        endpoint.Unsubscribe(this);

        return _endpoints.Remove(endpoint);
    }

    public RuntimeEndpoint? FindEndpoint(EndpointId id)
    {
        ArgumentNullException.ThrowIfNull(id);

        return _endpoints.FirstOrDefault(
            endpoint => endpoint.Descriptor.Id == id);
    }

    public void Subscribe(IPropertyValueObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);

        if (!_observers.Contains(observer))
        {
            _observers.Add(observer);
        }
    }

    public void Unsubscribe(IPropertyValueObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);

        _observers.Remove(observer);
    }

    public void OnPropertyValueChanged(PropertyValueChanged change)
    {
        foreach (var observer in _observers)
        {
            observer.OnPropertyValueChanged(change);
        }
    }
}