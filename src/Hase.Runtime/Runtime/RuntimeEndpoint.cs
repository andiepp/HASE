using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;

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
}