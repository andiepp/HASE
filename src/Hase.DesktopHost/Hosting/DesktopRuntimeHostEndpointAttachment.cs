namespace Hase.DesktopHost.Hosting;

/// <summary>
/// One endpoint an endpoint provider contributes to a runtime host, together
/// with the attachment it performs.
/// </summary>
/// <remarks>
/// An attachment describes what would be attached and how; constructing one
/// connects, verifies, or attaches nothing. The host decides when, and in
/// which order, the contributed attachments run.
/// </remarks>
public sealed class DesktopRuntimeHostEndpointAttachment
{
    /// <summary>
    /// Initializes one contributed endpoint attachment.
    /// </summary>
    /// <param name="endpointId">
    /// The authoritative endpoint identity this attachment expects.
    /// </param>
    /// <param name="endpointKind">
    /// The endpoint kind reported in host diagnostics.
    /// </param>
    /// <param name="attachAsync">
    /// The attachment performed against the host attachment inventory.
    /// </param>
    public DesktopRuntimeHostEndpointAttachment(
        string endpointId,
        string endpointKind,
        Func<CancellationToken, Task> attachAsync)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointId);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointKind);
        ArgumentNullException.ThrowIfNull(attachAsync);

        EndpointId = endpointId.Trim();
        EndpointKind = endpointKind.Trim();
        AttachAsync = attachAsync;
    }

    /// <summary>
    /// Gets the authoritative endpoint identity this attachment expects.
    /// </summary>
    public string EndpointId { get; }

    /// <summary>
    /// Gets the endpoint kind reported in host diagnostics.
    /// </summary>
    public string EndpointKind { get; }

    /// <summary>
    /// Gets the attachment performed against the host attachment inventory.
    /// </summary>
    public Func<CancellationToken, Task> AttachAsync { get; }

    public override string ToString() =>
        $"{EndpointKind} endpoint attachment for '{EndpointId}'";
}
