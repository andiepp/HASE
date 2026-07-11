using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;

namespace Hase.Runtime.Runtime;

public sealed class RuntimeEndpoint : IPropertyValueObserver, IRuntimeNode
{
    private readonly List<RuntimeInstrument> _instruments = [];
    private readonly List<IPropertyValueObserver> _observers = [];

    public RuntimeEndpoint(RuntimeContext context, EndpointDescriptor descriptor)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));

        foreach (var instrument in descriptor.Instruments)
        {
            var runtimeInstrument = new RuntimeInstrument(this, instrument);
            runtimeInstrument.Subscribe(this);
            _instruments.Add(runtimeInstrument);
        }
    }

    public RuntimeContext Context { get; }

    public EndpointDescriptor Descriptor { get; }

    public IRuntimeNode Parent => Context;

    public IReadOnlyList<IRuntimeNode> Children =>
        _instruments.Cast<IRuntimeNode>().ToArray();

    private readonly List<IEndpointConnectionStatusObserver>
    _connectionStatusObservers = [];

    public EndpointConnectionStatus ConnectionStatus { get; private set; } =
    new(EndpointConnectionState.Disconnected);

    public IReadOnlyList<RuntimeInstrument> Instruments => _instruments;

    public RuntimeInstrument? FindInstrument(InstrumentId id)
    {
        ArgumentNullException.ThrowIfNull(id);

        return _instruments.FirstOrDefault(
            instrument => instrument.Descriptor.Id == id);
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

    public void SubscribeConnectionStatus(
    IEndpointConnectionStatusObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);

        if (!_connectionStatusObservers.Contains(observer))
        {
            _connectionStatusObservers.Add(observer);
        }
    }

    public void UnsubscribeConnectionStatus(
        IEndpointConnectionStatusObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);

        _connectionStatusObservers.Remove(observer);
    }

    public void UpdateConnectionStatus(
        EndpointConnectionStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        if (ConnectionStatus == status)
        {
            return;
        }

        var previousStatus = ConnectionStatus;
        ConnectionStatus = status;

        var change = new EndpointConnectionStatusChanged(
            this,
            previousStatus,
            status);

        foreach (var observer in _connectionStatusObservers.ToArray())
        {
            observer.OnEndpointConnectionStatusChanged(change);
        }
    }
}