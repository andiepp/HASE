namespace Hase.DesktopHost.Hosting;

/// <summary>
/// What the registered endpoint providers resolved for one runtime host
/// start: the attachments to run, and which providers contributed them.
/// </summary>
/// <remarks>
/// The contributing set is what keeps a configured-nowhere family from
/// constructing anything: only a provider named here is asked for an
/// attachment service.
/// </remarks>
public sealed class DesktopRuntimeHostEndpointResolution
{
    /// <summary>
    /// The resolution of a host that composes no endpoint at all.
    /// </summary>
    public static readonly DesktopRuntimeHostEndpointResolution Empty =
        new([], new HashSet<string>(StringComparer.Ordinal));

    /// <summary>
    /// Initializes one resolution result.
    /// </summary>
    public DesktopRuntimeHostEndpointResolution(
        IReadOnlyList<DesktopRuntimeHostEndpointAttachment> attachments,
        IReadOnlySet<string> contributingProviderIds)
    {
        Attachments =
            attachments
            ?? throw new ArgumentNullException(nameof(attachments));
        ContributingProviderIds =
            contributingProviderIds
            ?? throw new ArgumentNullException(
                nameof(contributingProviderIds));
    }

    /// <summary>
    /// Gets the contributed attachments, in provider registration order.
    /// </summary>
    public IReadOnlyList<DesktopRuntimeHostEndpointAttachment> Attachments
    {
        get;
    }

    /// <summary>
    /// Gets the identifiers of the providers that contributed at least one
    /// endpoint.
    /// </summary>
    public IReadOnlySet<string> ContributingProviderIds { get; }

    public override string ToString() =>
        $"Endpoint resolution ({Attachments.Count} endpoints from "
        + $"{ContributingProviderIds.Count} providers)";
}
