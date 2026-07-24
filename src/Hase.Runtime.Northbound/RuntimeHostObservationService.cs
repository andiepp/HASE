using Hase.Runtime.Transport.Attachment;
using Hase.Runtime.Connections;

namespace Hase.Runtime.Northbound;

/// <summary>
/// Provides race-free initial state and bounded normalized attachment
/// observations over one shared attachment projection.
/// </summary>
public sealed class RuntimeHostObservationService
    : IRuntimeHostObservationService,
      IRuntimeHostAttachmentProjectionObserver,
      IAsyncDisposable
{
    private readonly RuntimeHostId _runtimeHostId;

    private readonly RuntimeHostAttachmentProjection _attachmentProjection;

    private readonly IDisposable _projectionRegistration;

    private readonly object _syncRoot =
        new();

    private readonly List<BufferedRuntimeHostObservationSubscription>
        _subscriptions =
            [];

    private readonly Dictionary<
        RuntimeEndpointAttachmentInventoryEntry,
        RuntimeHostConnectionStatusObservationAdapter>
        _connectionStatusAdapters =
            new(
                ReferenceEqualityComparer.Instance);

    private bool _isDisposed;

    internal RuntimeHostObservationService(
        RuntimeHostId runtimeHostId,
        RuntimeHostAttachmentProjection attachmentProjection)
    {
        _runtimeHostId =
            runtimeHostId
            ?? throw new ArgumentNullException(
                nameof(runtimeHostId));

        _attachmentProjection =
            attachmentProjection
            ?? throw new ArgumentNullException(
                nameof(attachmentProjection));

        _projectionRegistration =
            attachmentProjection.Subscribe(
                this);
    }

    /// <inheritdoc />
    public Task<RuntimeHostObservationSubscription> OpenSubscriptionAsync(
        RuntimeHostObservationSubscriptionOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            options);

        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(
                _isDisposed,
                this);

            RuntimeHostAttachmentProjectionSnapshot projectionSnapshot =
                _attachmentProjection.Capture();

            foreach (
                RuntimeHostPublishedAttachment attachment
                in projectionSnapshot.Attachments)
            {
                EnsureConnectionStatusAdapter(
                    attachment);
            }

            var endpointSnapshots =
                projectionSnapshot.Attachments.Select(
                    CreateEndpointSnapshot);

            var initialSnapshot =
                new PublishedRuntimeHostSnapshot(
                    _runtimeHostId,
                    RuntimeHostApiVersion.Current,
                    endpointSnapshots);

            var subscription =
                new BufferedRuntimeHostObservationSubscription(
                    initialSnapshot,
                    new RuntimeHostObservationSequence(
                        0),
                    projectionSnapshot.ChangeOrder,
                    options.BufferCapacity,
                    RemoveSubscription);

            _subscriptions.Add(
                subscription);

            return Task.FromResult<RuntimeHostObservationSubscription>(
                subscription);
        }
    }

    /// <inheritdoc />
    void IRuntimeHostAttachmentProjectionObserver
        .OnAttachmentProjectionChanged(
            RuntimeHostAttachmentProjectionChange change)
    {
        lock (_syncRoot)
        {
            if (_isDisposed)
            {
                return;
            }

            if (change.Kind
                == RuntimeHostAttachmentProjectionChangeKind.Published)
            {
                EnsureConnectionStatusAdapter(
                    change.Attachment);
            }
            else if (change.Kind
                     == RuntimeHostAttachmentProjectionChangeKind.Ended)
            {
                RetireConnectionStatusAdapter(
                    change.Attachment.Entry);
            }

            foreach (
                BufferedRuntimeHostObservationSubscription subscription
                in _subscriptions.ToArray())
            {
                if (change.Order
                    <= subscription.ProjectionBoundaryOrder)
                {
                    continue;
                }

                subscription.TryEnqueue(
                    sequence =>
                        CreateObservation(
                            sequence,
                            change));
            }
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        BufferedRuntimeHostObservationSubscription[] subscriptions;
        RuntimeHostConnectionStatusObservationAdapter[] adapters;

        lock (_syncRoot)
        {
            if (_isDisposed)
            {
                return ValueTask.CompletedTask;
            }

            _isDisposed =
                true;

            subscriptions =
                _subscriptions.ToArray();

            _subscriptions.Clear();

            adapters =
                _connectionStatusAdapters.Values.ToArray();

            _connectionStatusAdapters.Clear();
        }

        _projectionRegistration.Dispose();

        foreach (
            RuntimeHostConnectionStatusObservationAdapter adapter
            in adapters)
        {
            adapter.Dispose();
        }

        foreach (
            BufferedRuntimeHostObservationSubscription subscription
            in subscriptions)
        {
            subscription.End();
        }

        return ValueTask.CompletedTask;
    }

    private static PublishedRuntimeEndpointSnapshot CreateEndpointSnapshot(
        RuntimeHostPublishedAttachment attachment)
    {
        return new PublishedRuntimeEndpointSnapshot(
            attachment.Generation,
            attachment.Entry.RuntimeEndpoint.Descriptor,
            attachment.Entry.RuntimeEndpoint.ConnectionStatus);
    }

    private static RuntimeHostObservation CreateObservation(
        RuntimeHostObservationSequence sequence,
        RuntimeHostAttachmentProjectionChange change)
    {
        RuntimeHostPublishedAttachment attachment =
            change.Attachment;

        RuntimeHostObservationPayload payload =
            change.Kind switch
            {
                RuntimeHostAttachmentProjectionChangeKind.Published =>
                    new RuntimeHostAttachmentPublishedObservationPayload(
                        CreateEndpointSnapshot(
                            attachment)),

                RuntimeHostAttachmentProjectionChangeKind.Ended =>
                    new RuntimeHostAttachmentEndedObservationPayload(
                        change.EndedAtUtc!.Value),

                _ =>
                    throw new InvalidOperationException(
                        "Unsupported attachment projection change.")
            };

        return new RuntimeHostObservation(
            sequence,
            attachment.Entry.EndpointId,
            attachment.Generation,
            payload);
    }

    private void RemoveSubscription(
        BufferedRuntimeHostObservationSubscription subscription)
    {
        lock (_syncRoot)
        {
            _subscriptions.Remove(
                subscription);
        }
    }

    private void EnsureConnectionStatusAdapter(
        RuntimeHostPublishedAttachment attachment)
    {
        if (_connectionStatusAdapters.ContainsKey(
                attachment.Entry))
        {
            return;
        }

        _connectionStatusAdapters.Add(
            attachment.Entry,
            new RuntimeHostConnectionStatusObservationAdapter(
                attachment,
                PublishConnectionStatusChange));
    }

    private void RetireConnectionStatusAdapter(
        RuntimeEndpointAttachmentInventoryEntry entry)
    {
        if (_connectionStatusAdapters.Remove(
                entry,
                out RuntimeHostConnectionStatusObservationAdapter? adapter))
        {
            adapter.Dispose();
        }
    }

    private void PublishConnectionStatusChange(
        RuntimeHostPublishedAttachment attachment,
        EndpointConnectionStatusChanged change)
    {
        lock (_syncRoot)
        {
            if (_isDisposed
                || !_connectionStatusAdapters.ContainsKey(
                    attachment.Entry))
            {
                return;
            }

            foreach (
                BufferedRuntimeHostObservationSubscription subscription
                in _subscriptions.ToArray())
            {
                subscription.TryEnqueue(
                    sequence =>
                        new RuntimeHostObservation(
                            sequence,
                            attachment.Entry.EndpointId,
                            attachment.Generation,
                            new RuntimeHostConnectionStatusChangedObservationPayload(
                                change.PreviousStatus,
                                change.CurrentStatus)));
            }
        }
    }
}
