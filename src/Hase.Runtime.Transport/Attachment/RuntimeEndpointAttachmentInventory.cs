using System.Runtime.ExceptionServices;
using Hase.Core.Domain.Identity;

namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Maintains the runtime host's active endpoint attachment inventory.
/// </summary>
/// <remarks>
/// Complete inventory operations are serialized. An operation that enters
/// first completes before the next operation observes or mutates inventory
/// state.
/// </remarks>
public sealed class RuntimeEndpointAttachmentInventory
    : IRuntimeEndpointAttachmentInventory
{
    private readonly IEndpointAttachmentService
        _attachmentService;

    private readonly Dictionary<
        EndpointId,
        RuntimeEndpointAttachmentInventoryEntry>
        _entries =
            [];

    private readonly SemaphoreSlim _operationGate =
        new(
            1,
            1);

    private bool _isDisposed;

    /// <summary>
    /// Initializes a runtime endpoint attachment inventory.
    /// </summary>
    public RuntimeEndpointAttachmentInventory(
        IEndpointAttachmentService attachmentService)
    {
        _attachmentService =
            attachmentService
            ?? throw new ArgumentNullException(
                nameof(attachmentService));
    }

    /// <inheritdoc />
    public async Task<RuntimeEndpointAttachmentInventoryEntry> AttachAsync(
        EndpointAttachmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        await _operationGate.WaitAsync(
            cancellationToken);

        try
        {
            ThrowIfDisposed();

            IEndpointAttachmentSession attachmentSession =
                await _attachmentService.AttachAsync(
                    request,
                    cancellationToken);

            RuntimeEndpointAttachmentInventoryEntry entry;

            try
            {
                entry =
                    new RuntimeEndpointAttachmentInventoryEntry(
                        attachmentSession);

                if (_entries.ContainsKey(
                        entry.EndpointId))
                {
                    throw new InvalidOperationException(
                        "An endpoint attachment with authoritative identity "
                        + $"'{entry.EndpointId}' is already present.");
                }

                _entries.Add(
                    entry.EndpointId,
                    entry);

                return entry;
            }
            catch (Exception attachmentFailure)
            {
                try
                {
                    await attachmentSession.DisposeAsync();
                }
                catch (Exception cleanupFailure)
                {
                    throw new AggregateException(
                        "Endpoint attachment failed and its incomplete "
                        + "session cleanup also failed.",
                        attachmentFailure,
                        cleanupFailure);
                }

                throw;
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <inheritdoc />
    public RuntimeEndpointAttachmentInventoryEntry? Find(
        EndpointId endpointId)
    {
        ArgumentNullException.ThrowIfNull(
            endpointId);

        _operationGate.Wait();

        try
        {
            ThrowIfDisposed();

            _entries.TryGetValue(
                endpointId,
                out RuntimeEndpointAttachmentInventoryEntry? entry);

            return entry;
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<RuntimeEndpointAttachmentInventoryEntry> List()
    {
        _operationGate.Wait();

        try
        {
            ThrowIfDisposed();

            return _entries.Values.ToArray();
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> DetachAsync(
        EndpointId endpointId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            endpointId);

        await _operationGate.WaitAsync(
            cancellationToken);

        try
        {
            ThrowIfDisposed();

            if (!_entries.Remove(
                    endpointId,
                    out RuntimeEndpointAttachmentInventoryEntry? entry))
            {
                return false;
            }

            await entry.AttachmentSession.ShutdownAsync(
                cancellationToken);

            return true;
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _operationGate.WaitAsync();

        try
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed =
                true;

            RuntimeEndpointAttachmentInventoryEntry[] entries =
                _entries.Values.ToArray();

            _entries.Clear();

            List<Exception>? failures =
                null;

            foreach (
                RuntimeEndpointAttachmentInventoryEntry entry
                in entries)
            {
                try
                {
                    await entry.AttachmentSession.DisposeAsync();
                }
                catch (Exception exception)
                {
                    failures ??=
                        [];

                    failures.Add(
                        exception);
                }
            }

            if (failures is null)
            {
                return;
            }

            if (failures.Count == 1)
            {
                ExceptionDispatchInfo
                    .Capture(
                        failures[0])
                    .Throw();
            }

            throw new AggregateException(
                "Multiple endpoint attachments failed during inventory "
                + "disposal.",
                failures);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _isDisposed,
            this);
    }
}