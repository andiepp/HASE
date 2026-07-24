using Hase.Runtime.Connections;
using Hase.Runtime.Transport.Attachment;

namespace Hase.Runtime.Northbound;

internal sealed class RuntimeHostConnectionStatusObservationAdapter
    : IEndpointConnectionStatusObserver,
      IDisposable
{
    private readonly RuntimeHostPublishedAttachment _attachment;
    private readonly Action<
        RuntimeHostPublishedAttachment,
        EndpointConnectionStatusChanged> _publish;

    private int _isRetired;

    public RuntimeHostConnectionStatusObservationAdapter(
        RuntimeHostPublishedAttachment attachment,
        Action<
            RuntimeHostPublishedAttachment,
            EndpointConnectionStatusChanged> publish)
    {
        _attachment =
            attachment
            ?? throw new ArgumentNullException(
                nameof(attachment));

        _publish =
            publish
            ?? throw new ArgumentNullException(
                nameof(publish));

        attachment.Entry.RuntimeEndpoint.SubscribeConnectionStatus(
            this);
    }

    public void OnEndpointConnectionStatusChanged(
        EndpointConnectionStatusChanged change)
    {
        if (Volatile.Read(
                ref _isRetired)
            != 0)
        {
            return;
        }

        if (!ReferenceEquals(
                change.Endpoint,
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

        _attachment.Entry.RuntimeEndpoint.UnsubscribeConnectionStatus(
            this);
    }
}