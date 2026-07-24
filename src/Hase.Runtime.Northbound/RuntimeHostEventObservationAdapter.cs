using Hase.Runtime.Runtime;

namespace Hase.Runtime.Northbound;

internal sealed class RuntimeHostEventObservationAdapter
    : IRuntimeEventObserver,
      IDisposable
{
    private readonly RuntimeHostPublishedAttachment _attachment;

    private readonly Action<
        RuntimeHostPublishedAttachment,
        RuntimeEventOccurrence> _publish;

    private readonly RuntimeEvent[] _runtimeEvents;

    private int _isRetired;

    public RuntimeHostEventObservationAdapter(
        RuntimeHostPublishedAttachment attachment,
        Action<
            RuntimeHostPublishedAttachment,
            RuntimeEventOccurrence> publish)
    {
        _attachment =
            attachment
            ?? throw new ArgumentNullException(
                nameof(attachment));

        _publish =
            publish
            ?? throw new ArgumentNullException(
                nameof(publish));

        _runtimeEvents =
            attachment.Entry.RuntimeEndpoint.Instruments
                .SelectMany(
                    instrument =>
                        instrument.Events)
                .ToArray();

        foreach (
            RuntimeEvent runtimeEvent
            in _runtimeEvents)
        {
            runtimeEvent.Subscribe(
                this);
        }
    }

    public void OnRuntimeEventOccurred(
        RuntimeEventOccurrence occurrence)
    {
        if (Volatile.Read(
                ref _isRetired)
            != 0)
        {
            return;
        }

        if (!ReferenceEquals(
                occurrence.Event.Instrument.Endpoint,
                _attachment.Entry.RuntimeEndpoint))
        {
            return;
        }

        _publish(
            _attachment,
            occurrence);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(
                ref _isRetired,
                1)
            != 0)
        {
            return;
        }

        foreach (
            RuntimeEvent runtimeEvent
            in _runtimeEvents)
        {
            runtimeEvent.Unsubscribe(
                this);
        }
    }
}
