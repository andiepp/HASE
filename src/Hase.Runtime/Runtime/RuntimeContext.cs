using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Diagnostics;

namespace Hase.Runtime.Runtime;

/// <summary>
/// Root object of a HASE runtime instance.
/// It maintains the live engineering model for one application.
/// </summary>
public sealed class RuntimeContext : IRuntimeNode, IPropertyValueObserver
{
    private readonly List<RuntimeEndpoint> _endpoints = [];
    private readonly List<IPropertyValueObserver> _observers = [];
    private readonly RuntimeDiagnosticPublisher _diagnostics;

    public RuntimeContext(
        RuntimeDiagnosticPublisher? diagnostics = null)
    {
        _diagnostics =
            diagnostics ??
            new RuntimeDiagnosticPublisher();
    }

    public IReadOnlyList<RuntimeEndpoint> Endpoints => _endpoints;

    public IRuntimeNode? Parent => null;

    public IReadOnlyList<IRuntimeNode> Children =>
        _endpoints.Cast<IRuntimeNode>().ToArray();

    /// <summary>
    /// Gets the structured diagnostic publisher used by this runtime.
    /// </summary>
    public RuntimeDiagnosticPublisher Diagnostics =>
        _diagnostics;

    /// <summary>
    /// Constructs a runtime endpoint associated with this context without
    /// publishing it in the context endpoint inventory.
    /// </summary>
    public RuntimeEndpoint CreateEndpoint(
        EndpointDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(
            descriptor);

        return new RuntimeEndpoint(
            this,
            descriptor);
    }

    /// <summary>
    /// Publishes a staged runtime endpoint in this context.
    /// </summary>
    public RuntimeEndpoint PublishEndpoint(
        RuntimeEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(
            endpoint);

        if (!ReferenceEquals(
                endpoint.Context,
                this))
        {
            throw new ArgumentException(
                "The runtime endpoint belongs to a different "
                + "runtime context.",
                nameof(endpoint));
        }

        if (_endpoints.Any(
                existingEndpoint =>
                    existingEndpoint.Descriptor.Id
                    == endpoint.Descriptor.Id))
        {
            throw new InvalidOperationException(
                $"An endpoint with id "
                + $"'{endpoint.Descriptor.Id}' already exists.");
        }

        endpoint.Subscribe(
            this);

        endpoint.SubscribeConnectionStatus(
            new RuntimeEndpointLifecycleDiagnosticObserver(
                endpoint,
                _diagnostics));

        _endpoints.Add(
            endpoint);

        PublishAttachmentInventoryEvent(
            endpoint,
            "EndpointPublished");

        return endpoint;
    }

    /// <summary>
    /// Constructs and immediately publishes a runtime endpoint.
    /// </summary>
    public RuntimeEndpoint AddEndpoint(
        EndpointDescriptor descriptor)
    {
        RuntimeEndpoint endpoint =
            CreateEndpoint(
                descriptor);

        return PublishEndpoint(
            endpoint);
    }

    public bool RemoveEndpoint(
        RuntimeEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(
            endpoint);

        if (!_endpoints.Remove(
                endpoint))
        {
            return false;
        }

        endpoint.Unsubscribe(
            this);

        PublishAttachmentInventoryEvent(
            endpoint,
            "EndpointRemoved");

        return true;
    }

    public RuntimeEndpoint? FindEndpoint(
        EndpointId id)
    {
        ArgumentNullException.ThrowIfNull(
            id);

        return _endpoints.FirstOrDefault(
            endpoint =>
                endpoint.Descriptor.Id
                == id);
    }

    public void Subscribe(
        IPropertyValueObserver observer)
    {
        ArgumentNullException.ThrowIfNull(
            observer);

        if (!_observers.Contains(
                observer))
        {
            _observers.Add(
                observer);
        }
    }

    public void Unsubscribe(
        IPropertyValueObserver observer)
    {
        ArgumentNullException.ThrowIfNull(
            observer);

        _observers.Remove(
            observer);
    }

    public void OnPropertyValueChanged(
        PropertyValueChanged change)
    {
        foreach (
            IPropertyValueObserver observer
            in _observers)
        {
            observer.OnPropertyValueChanged(
                change);
        }
    }

    private void PublishAttachmentInventoryEvent(
        RuntimeEndpoint endpoint,
        string eventName)
    {
        _diagnostics.Publish(
            RuntimeDiagnosticLevel.Operational,
            () =>
                new RuntimeDiagnosticEvent(
                    RuntimeDiagnosticLevel.Operational,
                    RuntimeDiagnosticCategory.RuntimeAttachment,
                    eventName,
                    endpointId:
                        endpoint.Descriptor.Id.Value));
    }
}
