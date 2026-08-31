namespace Hase.Client.Wpf.Services;

/// <summary>
/// Performs normalized Property and Command operations against one published
/// instrument of one endpoint attachment.
/// </summary>
/// <remarks>
/// This is the only device access a hosted instrument panel is given. A panel
/// never opens a transport, never speaks a device protocol, and never binds an
/// attachment other than the one it was opened for.
/// </remarks>
public interface IRuntimeHostInstrumentOperations
{
    /// <summary>
    /// Gets the attachment these operations are bound to.
    /// </summary>
    RemoteEndpointAttachmentKey Attachment { get; }

    Task<RemotePropertyOperationResult> ReadAsync(
        string propertyId,
        CancellationToken cancellationToken = default);

    Task<RemotePropertyOperationResult> WriteAsync(
        string propertyId,
        RemoteValue requestedValue,
        CancellationToken cancellationToken = default);

    Task<RemoteCommandOperationResult> ExecuteAsync(
        string commandPath,
        RemoteValue? argument = null,
        CancellationToken cancellationToken = default);
}
