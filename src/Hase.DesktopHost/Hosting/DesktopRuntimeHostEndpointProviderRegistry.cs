using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;

namespace Hase.DesktopHost.Hosting;

/// <summary>
/// Holds the endpoint providers composed into one Desktop Runtime Host.
/// </summary>
/// <remarks>
/// The host library registers no provider of its own. An application composes
/// the providers it ships, and an endpoint family becomes attachable only
/// through a provider registered here. A host composed without providers
/// behaves exactly as one that has no provider concept at all.
/// </remarks>
public sealed class DesktopRuntimeHostEndpointProviderRegistry
{
    private readonly IReadOnlyList<IDesktopRuntimeHostEndpointProvider>
        providers;

    /// <summary>
    /// Registers the composed providers in the order they are supplied. That
    /// order is the order in which their endpoints are attached.
    /// </summary>
    public DesktopRuntimeHostEndpointProviderRegistry(
        IEnumerable<IDesktopRuntimeHostEndpointProvider>? providers = null)
    {
        var registered = new List<IDesktopRuntimeHostEndpointProvider>();
        var providerIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (IDesktopRuntimeHostEndpointProvider provider
            in providers ?? [])
        {
            ArgumentNullException.ThrowIfNull(provider, nameof(providers));

            if (string.IsNullOrWhiteSpace(provider.ProviderId))
            {
                throw new ArgumentException(
                    "An endpoint provider identifier must not be empty.",
                    nameof(providers));
            }

            if (!providerIds.Add(provider.ProviderId.Trim()))
            {
                throw new ArgumentException(
                    "Only one endpoint provider may be registered for each "
                    + "provider identifier.",
                    nameof(providers));
            }

            registered.Add(provider);
        }

        this.providers = registered;
        RegisteredProviderIds = providerIds;
    }

    /// <summary>
    /// Gets the provider identifiers this host composes.
    /// </summary>
    public IReadOnlySet<string> RegisteredProviderIds { get; }

    /// <summary>
    /// Resolves the provider registered under the supplied identifier.
    /// </summary>
    public bool TryResolve(
        string providerId,
        out IDesktopRuntimeHostEndpointProvider provider)
    {
        if (!string.IsNullOrWhiteSpace(providerId))
        {
            string trimmed = providerId.Trim();

            foreach (IDesktopRuntimeHostEndpointProvider registered
                in providers)
            {
                if (StringComparer.Ordinal.Equals(
                        registered.ProviderId.Trim(),
                        trimmed))
                {
                    provider = registered;
                    return true;
                }
            }
        }

        provider = null!;
        return false;
    }

    /// <summary>
    /// Resolves the configured endpoints of every registered provider, in
    /// registration order.
    /// </summary>
    /// <remarks>
    /// This runs before the host owns an attachment inventory, so a
    /// provider's preflight fails before any runtime resource exists.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// A provider resolved no list, or two providers contributed the same
    /// endpoint identity.
    /// </exception>
    public async Task<DesktopRuntimeHostEndpointResolution> ResolveAsync(
        DesktopRuntimeHostEndpointProviderContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var attachments = new List<DesktopRuntimeHostEndpointAttachment>();
        var endpointIds = new HashSet<string>(StringComparer.Ordinal);
        var contributingProviderIds =
            new HashSet<string>(StringComparer.Ordinal);

        foreach (IDesktopRuntimeHostEndpointProvider provider in providers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<DesktopRuntimeHostEndpointAttachment> resolved =
                await provider.ResolveAttachmentsAsync(
                    context,
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Endpoint provider '{provider.ProviderId}' resolved no "
                    + "attachment list.");

            foreach (DesktopRuntimeHostEndpointAttachment attachment
                in resolved)
            {
                if (attachment is null)
                {
                    throw new InvalidOperationException(
                        $"Endpoint provider '{provider.ProviderId}' resolved "
                        + "a null attachment.");
                }

                if (!endpointIds.Add(attachment.EndpointId))
                {
                    throw new InvalidOperationException(
                        $"Endpoint identity '{attachment.EndpointId}' is "
                        + "contributed by more than one endpoint provider.");
                }

                attachments.Add(attachment);
            }

            if (resolved.Count > 0)
            {
                contributingProviderIds.Add(provider.ProviderId.Trim());
            }
        }

        return new DesktopRuntimeHostEndpointResolution(
            attachments,
            contributingProviderIds);
    }

    /// <summary>
    /// Creates the attachment service routing every connection definition
    /// contributed by the providers that resolved at least one endpoint.
    /// </summary>
    /// <remarks>
    /// A provider that resolved nothing is never asked for a service, so a
    /// family configured nowhere constructs nothing. When no contributing
    /// provider supplies a service, the returned router rejects every request
    /// exactly as an unrouted connection definition is rejected.
    /// </remarks>
    public IEndpointAttachmentService CreateAttachmentService(
        RuntimeContext runtimeContext,
        DesktopRuntimeHostEndpointResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(runtimeContext);
        ArgumentNullException.ThrowIfNull(resolution);

        var routes =
            new List<DesktopRuntimeHostEndpointProviderAttachmentRoute>();

        foreach (IDesktopRuntimeHostEndpointProvider provider in providers)
        {
            if (!resolution.ContributingProviderIds.Contains(
                    provider.ProviderId.Trim()))
            {
                continue;
            }

            if (provider.CreateAttachmentService(runtimeContext)
                is IEndpointAttachmentService service)
            {
                routes.Add(
                    new DesktopRuntimeHostEndpointProviderAttachmentRoute(
                        provider,
                        service));
            }
        }

        return new DesktopRuntimeHostEndpointProviderAttachmentRouter(routes);
    }
}

/// <summary>
/// One contributing provider and the attachment service it created.
/// </summary>
internal sealed record DesktopRuntimeHostEndpointProviderAttachmentRoute(
    IDesktopRuntimeHostEndpointProvider Provider,
    IEndpointAttachmentService Service);

/// <summary>
/// Routes attachment requests to the contributing provider that supports the
/// requested connection definition.
/// </summary>
internal sealed class DesktopRuntimeHostEndpointProviderAttachmentRouter
    : IEndpointAttachmentService
{
    private readonly
        IReadOnlyList<DesktopRuntimeHostEndpointProviderAttachmentRoute>
            routes;

    public DesktopRuntimeHostEndpointProviderAttachmentRouter(
        IReadOnlyList<DesktopRuntimeHostEndpointProviderAttachmentRoute> routes)
    {
        this.routes = routes ?? throw new ArgumentNullException(nameof(routes));
    }

    /// <inheritdoc />
    public Task<IEndpointAttachmentSession> AttachAsync(
        EndpointAttachmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        foreach (DesktopRuntimeHostEndpointProviderAttachmentRoute route
            in routes)
        {
            if (route.Provider.Supports(request.ConnectionDefinition))
            {
                return route.Service.AttachAsync(request, cancellationToken);
            }
        }

        throw new NotSupportedException(
            "No endpoint provider is registered for the requested "
            + "connection definition.");
    }
}
