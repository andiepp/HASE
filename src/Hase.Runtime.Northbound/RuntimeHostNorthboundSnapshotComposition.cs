using Hase.Runtime.Transport.Attachment;

namespace Hase.Runtime.Northbound;

/// <summary>
/// Represents the resolved identity, snapshot services, Property service, and
/// Command service composed for one runtime host.
/// </summary>
/// <remarks>
/// This composition projects a host-owned attachment inventory. It does not
/// own or dispose the inventory or any endpoint lifecycle resources.
/// </remarks>
public sealed class RuntimeHostNorthboundSnapshotComposition
{
    private RuntimeHostNorthboundSnapshotComposition(
        RuntimeHostIdentityResolution identityResolution,
        IRuntimeHostInventorySnapshotProvider inventorySnapshotProvider,
        IRuntimeHostSnapshotProvider snapshotProvider,
        IRuntimeHostPropertyService propertyService,
        IRuntimeHostCommandService commandService)
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
    }

    /// <summary>
    /// Gets the authoritative runtime-host identity resolution.
    /// </summary>
    public RuntimeHostIdentityResolution IdentityResolution
    {
        get;
    }

    /// <summary>
    /// Gets the northbound projection of the host-owned attachment inventory.
    /// </summary>
    public IRuntimeHostInventorySnapshotProvider InventorySnapshotProvider
    {
        get;
    }

    /// <summary>
    /// Gets the complete runtime-host snapshot provider.
    /// </summary>
    public IRuntimeHostSnapshotProvider SnapshotProvider
    {
        get;
    }

    /// <summary>
    /// Gets the generation-scoped cached and authoritative Property service.
    /// </summary>
    public IRuntimeHostPropertyService PropertyService
    {
        get;
    }

    /// <summary>
    /// Gets the generation-scoped normalized Command service.
    /// </summary>
    public IRuntimeHostCommandService CommandService
    {
        get;
    }

    /// <summary>
    /// Resolves runtime-host identity from explicit configuration or the
    /// supplied file path and composes northbound snapshot, Property, and
    /// Command services over the host-owned attachment inventory.
    /// </summary>
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

        var attachmentProjection =
            new RuntimeHostAttachmentProjection(
                attachmentInventory);

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

        return new RuntimeHostNorthboundSnapshotComposition(
            identityResolution,
            inventorySnapshotProvider,
            snapshotProvider,
            propertyService,
            commandService);
    }
}