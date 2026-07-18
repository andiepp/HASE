using Hase.Core.Domain.Events;

namespace Hase.Runtime.Runtime;

public sealed class RuntimeEvent : IRuntimeNode
{
    private readonly object _observerSyncRoot =
        new();

    private readonly List<IRuntimeEventObserver> _observers =
        [];

    public RuntimeEvent(
        RuntimeInstrument instrument,
        EventDescriptor descriptor)
    {
        Instrument =
            instrument
            ?? throw new ArgumentNullException(
                nameof(instrument));

        Descriptor =
            descriptor
            ?? throw new ArgumentNullException(
                nameof(descriptor));
    }

    public EventDescriptor Descriptor
    {
        get;
    }

    public RuntimeInstrument Instrument
    {
        get;
    }

    public string DisplayName =>
        Descriptor.DisplayName;

    public IRuntimeNode Parent =>
        Instrument;

    public IReadOnlyList<IRuntimeNode> Children =>
        Array.Empty<IRuntimeNode>();

    /// <summary>
    /// Subscribes an observer to occurrences of this runtime event.
    /// </summary>
    public void Subscribe(
        IRuntimeEventObserver observer)
    {
        ArgumentNullException.ThrowIfNull(
            observer);

        lock (_observerSyncRoot)
        {
            if (!_observers.Any(
                    current =>
                        ReferenceEquals(
                            current,
                            observer)))
            {
                _observers.Add(
                    observer);
            }
        }
    }

    /// <summary>
    /// Removes an event-occurrence observer.
    /// </summary>
    public void Unsubscribe(
        IRuntimeEventObserver observer)
    {
        ArgumentNullException.ThrowIfNull(
            observer);

        lock (_observerSyncRoot)
        {
            _observers.RemoveAll(
                current =>
                    ReferenceEquals(
                        current,
                        observer));
        }
    }

    /// <summary>
    /// Publishes one occurrence to the currently subscribed observers.
    /// </summary>
    public void PublishOccurrence(
        DateTimeOffset timestampUtc,
        object? value)
    {
        var occurrence =
            new RuntimeEventOccurrence(
                this,
                timestampUtc,
                value);

        IRuntimeEventObserver[] observers;

        lock (_observerSyncRoot)
        {
            observers =
                _observers.ToArray();
        }

        foreach (IRuntimeEventObserver observer
                 in observers)
        {
            try
            {
                observer.OnRuntimeEventOccurred(
                    occurrence);
            }
            catch
            {
                // Runtime event observers are observational. One observer
                // must not block delivery to later observers.
            }
        }
    }
}