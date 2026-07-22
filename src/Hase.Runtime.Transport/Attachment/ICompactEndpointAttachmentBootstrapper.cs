namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Bootstraps a compact serial endpoint through a temporary connection for
/// explicit runtime-host attachment.
/// </summary>
public interface ICompactEndpointAttachmentBootstrapper
{
    /// <summary>
    /// Opens a temporary serial connection, obtains authoritative compact
    /// identity and descriptor-reference information, resolves the exact
    /// descriptor definition, and closes the temporary connection.
    /// </summary>
    /// <param name="connectionDefinition">
    /// Serial target and optional expected authoritative endpoint identity.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels bootstrap and requires cleanup of the temporary connection.
    /// </param>
    Task<CompactEndpointAttachmentBootstrapResult> BootstrapAsync(
        SerialEndpointConnectionDefinition connectionDefinition,
        CancellationToken cancellationToken = default);
}
