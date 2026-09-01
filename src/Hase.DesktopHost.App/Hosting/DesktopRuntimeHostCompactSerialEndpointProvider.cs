using System.IO;
using Hase.CompactProtocol;
using Hase.Core.Domain.Identity;
using Hase.DesktopHost.Configuration;
using Hase.DesktopHost.Hosting;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Hase.Runtime.Transport.Discovery;
using Hase.Transport.Discovery;

namespace Hase.DesktopHost.App.Hosting;

/// <summary>
/// Supplies the compact-protocol endpoints this host reaches over USB serial.
/// </summary>
/// <remarks>
/// The kind carries no instrument knowledge: it discovers the serial port by
/// the configured VID/PID, verifies the endpoint identity, and resolves the
/// descriptor from the host compact definition repository.
/// </remarks>
public sealed class DesktopRuntimeHostCompactSerialEndpointProvider
    : IDesktopRuntimeHostEndpointProvider
{
    /// <summary>
    /// The identifier this provider is registered under.
    /// </summary>
    public const string Id = "compact-serial";

    /// <summary>
    /// The endpoint kind reported in host diagnostics.
    /// </summary>
    public const string EndpointKind = "CompactSerial";

    /// <inheritdoc />
    public string ProviderId => Id;

    /// <inheritdoc />
    public bool Supports(IEndpointConnectionDefinition connectionDefinition)
    {
        ArgumentNullException.ThrowIfNull(connectionDefinition);

        return connectionDefinition is SerialEndpointConnectionDefinition;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The runtime attachment host routes compact serial definitions itself,
    /// so this provider contributes no service of its own.
    /// </remarks>
    public IEndpointAttachmentService? CreateAttachmentService(
        RuntimeContext runtimeContext)
    {
        ArgumentNullException.ThrowIfNull(runtimeContext);

        return null;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DesktopRuntimeHostEndpointAttachment>>
        ResolveAttachmentsAsync(
            DesktopRuntimeHostEndpointProviderContext context,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var attachments = new List<DesktopRuntimeHostEndpointAttachment>();

        foreach (DesktopRuntimeHostCompactSerialEndpointProfile endpoint
            in context.EndpointComposition.CompactSerialEndpoints)
        {
            attachments.Add(
                new DesktopRuntimeHostEndpointAttachment(
                    endpoint.ExpectedEndpointId,
                    EndpointKind,
                    token => AttachAsync(
                        context.AttachmentInventory,
                        context.CompactDefinitionRepository,
                        endpoint,
                        token)));
        }

        return Task.FromResult<
            IReadOnlyList<DesktopRuntimeHostEndpointAttachment>>(attachments);
    }

    private static async Task AttachAsync(
        IRuntimeEndpointAttachmentInventory attachmentInventory,
        ICompactEndpointDefinitionRepository definitionRepository,
        DesktopRuntimeHostCompactSerialEndpointProfile endpoint,
        CancellationToken cancellationToken)
    {
        var descriptorRepository =
            new CompactEndpointDescriptorRepositoryAdapter(
                definitionRepository);
        var candidateFilter =
            new UsbSerialEndpointMetadataFilter(
                vendorId: endpoint.VendorId,
                productId: endpoint.ProductId);

        UsbSerialEndpointDiscoveryService discoveryService =
            WindowsUsbSerialEndpointDiscovery.Create(
                descriptorRepository,
                candidateFilter);
        var discoveryOptions =
            new UsbSerialEndpointDiscoveryOptions(
                endpoint.BaudRate,
                endpoint.VerificationTimeout);

        UsbSerialEndpointDiscoveryResult discoveryResult =
            await discoveryService.DiscoverAsync(
                discoveryOptions,
                cancellationToken);

        if (discoveryResult.VerifiedEndpoints.Count == 0)
        {
            throw new DesktopRuntimeHostEndpointUnavailableException(
                "NoVerifiedCandidate");
        }

        if (discoveryResult.VerifiedEndpoints.Count > 1)
        {
            throw new InvalidOperationException(
                "The desktop runtime host requires exactly one "
                + "authoritatively verified compact endpoint after "
                + "VID/PID filtering.");
        }

        VerifiedUsbSerialEndpoint selectedEndpoint =
            discoveryResult.VerifiedEndpoints[0];

        if (!selectedEndpoint.EndpointId.Equals(
                new EndpointId(
                    endpoint.ExpectedEndpointId)))
        {
            throw new InvalidDataException(
                "The verified compact endpoint identity does not match the configured expected identity.");
        }

        SerialEndpointConnectionDefinition connectionDefinition =
            SerialEndpointConnectionDefinition.FromVerifiedEndpoint(
                selectedEndpoint,
                discoveryOptions);
        var request =
            new EndpointAttachmentRequest(
                connectionDefinition,
                HostRepositoryDescriptorSource.Instance);

        await attachmentInventory.AttachAsync(
            request,
            cancellationToken);
    }
}
