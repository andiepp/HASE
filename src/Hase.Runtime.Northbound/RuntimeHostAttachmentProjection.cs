using Hase.Core.Domain.Identity;
using Hase.Runtime.Transport.Attachment;

namespace Hase.Runtime.Northbound;

/// <summary>
/// Maintains one shared projection of current attachment entries and their
/// authoritative northbound generations.
/// </summary>
internal sealed class RuntimeHostAttachmentProjection
    : IRuntimeEndpointAttachmentInventoryObserver,
      IDisposable
{
    private readonly IRuntimeEndpointAttachmentInventory
        _attachmentInventory;

    private readonly Dictionary<
        RuntimeEndpointAttachmentInventoryEntry,
        RuntimeEndpointAttachmentGeneration>
        _generations =
            new(
                ReferenceEqualityComparer.Instance);

    private readonly object _syncRoot =
        new();

    private readonly List<IRuntimeHostAttachmentProjectionObserver>
        _observers =
            [];

    private readonly List<object> _initializationChanges =
        [];

    private readonly IDisposable? _inventoryObservationRegistration;

    private readonly bool _isObserved;

    private bool _isInitializing;

    private bool _isDisposed;

    private long _nextChangeOrder;

    public RuntimeHostAttachmentProjection(
        IRuntimeEndpointAttachmentInventory attachmentInventory)
    {
        _attachmentInventory =
            attachmentInventory
            ?? throw new ArgumentNullException(
                nameof(attachmentInventory));
    }

    public RuntimeHostAttachmentProjection(
        IRuntimeEndpointAttachmentInventory attachmentInventory,
        IRuntimeEndpointAttachmentInventoryObservationSource
            inventoryObservationSource)
    {
        _attachmentInventory =
            attachmentInventory
            ?? throw new ArgumentNullException(
                nameof(attachmentInventory));

        ArgumentNullException.ThrowIfNull(
            inventoryObservationSource);

        _isObserved =
            true;

        _isInitializing =
            true;

        _inventoryObservationRegistration =
            inventoryObservationSource.Subscribe(
                this);

        IReadOnlyList<RuntimeEndpointAttachmentInventoryEntry> initialEntries;

        try
        {
            initialEntries =
                _attachmentInventory.List();
        }
        catch
        {
            _inventoryObservationRegistration.Dispose();
            throw;
        }

        lock (_syncRoot)
        {
            SynchronizeEntriesWithoutNotifications(
                initialEntries);

            foreach (object change in _initializationChanges)
            {
                ApplyInventoryChange(
                    change);
            }

            _initializationChanges.Clear();

            _isInitializing =
                false;
        }
    }

    /// <summary>
    /// Lists current published attachments with stable per-entry generations.
    /// </summary>
    public IReadOnlyList<RuntimeHostPublishedAttachment> List()
    {
        if (!_isObserved)
        {
            IReadOnlyList<RuntimeEndpointAttachmentInventoryEntry> entries =
                _attachmentInventory.List();

            lock (_syncRoot)
            {
                ThrowIfDisposed();

                SynchronizeEntriesWithoutNotifications(
                    entries);
            }
        }

        lock (_syncRoot)
        {
            ThrowIfDisposed();

            return _generations
                .Select(
                    pair =>
                        new RuntimeHostPublishedAttachment(
                            pair.Key,
                            pair.Value))
                .ToArray();
        }
    }

    /// <summary>
    /// Finds one current published attachment by authoritative endpoint
    /// identity.
    /// </summary>
    public RuntimeHostPublishedAttachment? Find(
        EndpointId endpointId)
    {
        ArgumentNullException.ThrowIfNull(
            endpointId);

        return List().FirstOrDefault(
            attachment =>
                attachment.Entry.EndpointId
                == endpointId);
    }

    /// <summary>
    /// Atomically captures current attachments and their internal change-order
    /// boundary.
    /// </summary>
    public RuntimeHostAttachmentProjectionSnapshot Capture()
    {
        if (!_isObserved)
        {
            _ =
                List();
        }

        lock (_syncRoot)
        {
            ThrowIfDisposed();

            return new RuntimeHostAttachmentProjectionSnapshot(
                _nextChangeOrder,
                _generations.Select(
                    pair =>
                        new RuntimeHostPublishedAttachment(
                            pair.Key,
                            pair.Value)));
        }
    }

    /// <summary>
    /// Registers an observer for later ordered projection changes.
    /// </summary>
    public IDisposable Subscribe(
        IRuntimeHostAttachmentProjectionObserver observer)
    {
        ArgumentNullException.ThrowIfNull(
            observer);

        lock (_syncRoot)
        {
            ThrowIfDisposed();

            _observers.Add(
                observer);
        }

        return new ObserverRegistration(
            this,
            observer);
    }

    /// <inheritdoc />
    public void OnAttachmentPublished(
        RuntimeEndpointAttachmentPublished publication)
    {
        ArgumentNullException.ThrowIfNull(
            publication);

        ProcessInventoryChange(
            publication);
    }

    /// <inheritdoc />
    public void OnAttachmentEnded(
        RuntimeEndpointAttachmentEnded ending)
    {
        ArgumentNullException.ThrowIfNull(
            ending);

        ProcessInventoryChange(
            ending);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        IDisposable? inventoryRegistration;

        lock (_syncRoot)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed =
                true;

            _observers.Clear();
            _initializationChanges.Clear();

            inventoryRegistration =
                _inventoryObservationRegistration;
        }

        inventoryRegistration?.Dispose();
    }

    private void ProcessInventoryChange(
        object change)
    {
        RuntimeHostAttachmentProjectionChange? projectionChange;

        lock (_syncRoot)
        {
            if (_isDisposed)
            {
                return;
            }

            if (_isInitializing)
            {
                _initializationChanges.Add(
                    change);

                return;
            }

            projectionChange =
                ApplyInventoryChange(
                    change);
        }

        if (projectionChange is not null)
        {
            PublishChange(
                projectionChange);
        }
    }

    private RuntimeHostAttachmentProjectionChange? ApplyInventoryChange(
        object change)
    {
        return change switch
        {
            RuntimeEndpointAttachmentPublished publication =>
                ApplyPublication(
                    publication),

            RuntimeEndpointAttachmentEnded ending =>
                ApplyEnding(
                    ending),

            _ =>
                throw new ArgumentException(
                    "Unsupported inventory change.",
                    nameof(change))
        };
    }

    private RuntimeHostAttachmentProjectionChange? ApplyPublication(
        RuntimeEndpointAttachmentPublished publication)
    {
        RuntimeEndpointAttachmentInventoryEntry entry =
            publication.Entry;

        if (_generations.ContainsKey(
                entry))
        {
            return null;
        }

        RuntimeEndpointAttachmentGeneration generation =
            RuntimeEndpointAttachmentGeneration.CreateNew();

        _generations.Add(
            entry,
            generation);

        return CreateChange(
            RuntimeHostAttachmentProjectionChangeKind.Published,
            new RuntimeHostPublishedAttachment(
                entry,
                generation));
    }

    private RuntimeHostAttachmentProjectionChange? ApplyEnding(
        RuntimeEndpointAttachmentEnded ending)
    {
        if (!_generations.Remove(
                ending.Entry,
                out RuntimeEndpointAttachmentGeneration? generation))
        {
            return null;
        }

        return CreateChange(
            RuntimeHostAttachmentProjectionChangeKind.Ended,
            new RuntimeHostPublishedAttachment(
                ending.Entry,
                generation),
            ending.EndedAtUtc);
    }

    private RuntimeHostAttachmentProjectionChange CreateChange(
        RuntimeHostAttachmentProjectionChangeKind kind,
        RuntimeHostPublishedAttachment attachment,
        DateTimeOffset? endedAtUtc = null)
    {
        _nextChangeOrder =
            checked(
                _nextChangeOrder + 1);

        return new RuntimeHostAttachmentProjectionChange(
            _nextChangeOrder,
            kind,
            attachment,
            endedAtUtc);
    }

    private void SynchronizeEntriesWithoutNotifications(
        IReadOnlyList<RuntimeEndpointAttachmentInventoryEntry> entries)
    {
        var currentEntries =
            new HashSet<RuntimeEndpointAttachmentInventoryEntry>(
                entries,
                ReferenceEqualityComparer.Instance);

        foreach (
            RuntimeEndpointAttachmentInventoryEntry endedEntry
            in _generations.Keys
                .Where(
                    entry =>
                        !currentEntries.Contains(
                            entry))
                .ToArray())
        {
            _generations.Remove(
                endedEntry);
        }

        foreach (
            RuntimeEndpointAttachmentInventoryEntry entry
            in entries)
        {
            if (!_generations.ContainsKey(
                    entry))
            {
                _generations.Add(
                    entry,
                    RuntimeEndpointAttachmentGeneration.CreateNew());
            }
        }
    }

    private void PublishChange(
        RuntimeHostAttachmentProjectionChange change)
    {
        IRuntimeHostAttachmentProjectionObserver[] observers;

        lock (_syncRoot)
        {
            if (_isDisposed)
            {
                return;
            }

            observers =
                _observers.ToArray();
        }

        foreach (
            IRuntimeHostAttachmentProjectionObserver observer
            in observers)
        {
            try
            {
                observer.OnAttachmentProjectionChanged(
                    change);
            }
            catch
            {
                // Projection observers are observational. One observer must
                // not fail inventory routing or later observers.
            }
        }
    }

    private void Unsubscribe(
        IRuntimeHostAttachmentProjectionObserver observer)
    {
        lock (_syncRoot)
        {
            _observers.Remove(
                observer);
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _isDisposed,
            this);
    }

    private sealed class ObserverRegistration
        : IDisposable
    {
        private RuntimeHostAttachmentProjection? _projection;

        private IRuntimeHostAttachmentProjectionObserver? _observer;

        public ObserverRegistration(
            RuntimeHostAttachmentProjection projection,
            IRuntimeHostAttachmentProjectionObserver observer)
        {
            _projection =
                projection;

            _observer =
                observer;
        }

        public void Dispose()
        {
            RuntimeHostAttachmentProjection? projection =
                Interlocked.Exchange(
                    ref _projection,
                    null);

            IRuntimeHostAttachmentProjectionObserver? observer =
                Interlocked.Exchange(
                    ref _observer,
                    null);

            if (projection is null
                || observer is null)
            {
                return;
            }

            projection.Unsubscribe(
                observer);
        }
    }
}
