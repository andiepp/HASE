using Hase.Runtime.Runtime;

namespace Hase.Runtime.Northbound;

internal sealed class RuntimeHostPropertyValueObservationAdapter
    : IPropertyValueObserver,
      IDisposable
{
    private readonly RuntimeHostPublishedAttachment _attachment;

    private readonly Action<
        RuntimeHostPublishedAttachment,
        PropertyValueChanged> _publish;

    private int _isRetired;

    public RuntimeHostPropertyValueObservationAdapter(
        RuntimeHostPublishedAttachment attachment,
        Action<
            RuntimeHostPublishedAttachment,
            PropertyValueChanged> publish)
    {
        _attachment =
            attachment
            ?? throw new ArgumentNullException(
                nameof(attachment));

        _publish =
            publish
            ?? throw new ArgumentNullException(
                nameof(publish));

        attachment.Entry.RuntimeEndpoint.Subscribe(
            this);
    }

    public void OnPropertyValueChanged(
        PropertyValueChanged change)
    {
        if (Volatile.Read(
                ref _isRetired)
            != 0)
        {
            return;
        }

        if (!ReferenceEquals(
                change.Property.Instrument.Endpoint,
                _attachment.Entry.RuntimeEndpoint))
        {
            return;
        }

        _publish(
            _attachment,
            change);
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

        _attachment.Entry.RuntimeEndpoint.Unsubscribe(
            this);
    }
}
