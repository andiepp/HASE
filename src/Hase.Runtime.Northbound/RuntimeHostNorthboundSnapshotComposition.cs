using Hase.Runtime.Transport.Attachment;

namespace Hase.Runtime.Northbound;

/// <summary>
/// Represents the resolved identity and normalized northbound services
/// composed for one runtime host.
/// </summary>
/// <remarks>
/// This composition projects a host-owned attachment inventory. It owns its
/// observation subscriptions and projection registrations, but it does not own
/// or dispose the inventory or any endpoint lifecycle resources.
/// </remarks>
public sealed class RuntimeHostNorthboundSnapshotComposition
    : IAsyncDisposable
{
    private readonly RuntimeHostAttachmentProjection
        _attachmentProjection;

    private readonly RuntimeHostObservationService
        _observationService;

    private readonly object _disposeSyncRoot =
        new();

    private Task? _disposeTask;

    private RuntimeHostNorthboundSnapshotComposition(
        RuntimeHostIdentityResolution identityResolution,
        IRuntimeHostInventorySnapshotProvider inventorySnapshotProvider,
        IRuntimeHostSnapshotProvider snapshotProvider,
        IRuntimeHostPropertyService propertyService,
        IRuntimeHostCommandService commandService,
        RuntimeHostAttachmentProjection attachmentProjection,
        RuntimeHostObservationService observationService)
    {
        IdentityResolution =
            identityResolution;

        InventorySnapshotProvider =
            inventorySnapshotProvider;

        SnapshotProvider =
            snapshotProvider;

        PropertyService =
            propertyService;

        CommandService =
            commandService;

        _attachmentProjection =
            attachmentProjection;

        _observationService =
            observationService;
    }

    public RuntimeHostIdentityResolution IdentityResolution
    {
        get;
    }

    public IRuntimeHostInventorySnapshotProvider InventorySnapshotProvider
    {
        get;
    }

    public IRuntimeHostSnapshotProvider SnapshotProvider
    {
        get;
    }

    public IRuntimeHostPropertyService PropertyService
    {
        get;
    }

    public IRuntimeHostCommandService CommandService
    {
        get;
    }

    /// <summary>
    /// Gets the normalized live-observation service.
    /// </summary>
    public IRuntimeHostObservationService ObservationService =>
        _observationService;

    public static async Task<RuntimeHostNorthboundSnapshotComposition>
        CreateFileBackedAsync(
            IRuntimeEndpointAttachmentInventory attachmentInventory,
            string identityFilePath,
            RuntimeHostId? configuredRuntimeHostId = null,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            attachmentInventory);

        var identityStore =
            new FileRuntimeHostIdentityStore(
                identityFilePath);

        var identityResolver =
            new RuntimeHostIdentityResolver(
                identityStore,
                new GuidRuntimeHostIdGenerator());

        RuntimeHostIdentityResolution identityResolution =
            await identityResolver
                .ResolveAsync(
                    configuredRuntimeHostId,
                    cancellationToken)
                .ConfigureAwait(
                    false);

        RuntimeHostAttachmentProjection attachmentProjection =
            attachmentInventory
                is IRuntimeEndpointAttachmentInventoryObservationSource
                    observationSource
                ? new RuntimeHostAttachmentProjection(
                    attachmentInventory,
                    observationSource)
                : new RuntimeHostAttachmentProjection(
                    attachmentInventory);

        try
        {
            var inventorySnapshotProvider =
                RuntimeHostInventorySnapshotProvider.CreateShared(
                    attachmentProjection);

            var snapshotProvider =
                new RuntimeHostSnapshotProvider(
                    identityResolution.RuntimeHostId,
                    inventorySnapshotProvider);

            var propertyService =
                new RuntimeHostPropertyService(
                    attachmentProjection);

            var commandService =
                new RuntimeHostCommandService(
                    attachmentProjection);

            var observationService =
                new RuntimeHostObservationService(
                    identityResolution.RuntimeHostId,
                    attachmentProjection);

            return new RuntimeHostNorthboundSnapshotComposition(
                identityResolution,
                inventorySnapshotProvider,
                snapshotProvider,
                propertyService,
                commandService,
                attachmentProjection,
                observationService);
        }
        catch
        {
            attachmentProjection.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        lock (_disposeSyncRoot)
        {
            _disposeTask ??=
                DisposeCoreAsync();

            return new ValueTask(
                _disposeTask);
        }
    }

    private async Task DisposeCoreAsync()
    {
        await _observationService
            .DisposeAsync()
            .ConfigureAwait(
                false);

        _attachmentProjection.Dispose();
    }
}