using System.IO;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;
using Hase.DesktopHost.Configuration;
using Hase.DesktopHost.Hosting;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Hase.Scpi.Kel103;
using Hase.Transport.Serial;
using Hase.Scpi.Kel103.Hosting;

namespace Hase.Scpi.Kel103.DesktopHost;

/// <summary>
/// Supplies the KEL-103 electronic loads this host reaches over serial.
/// </summary>
/// <remarks>
/// The provider owns everything the family needs: the definition preflight
/// that validates a configured endpoint, the connection definition it
/// attaches with, and the attachment service that routes it. A Runtime Host
/// composes this provider; it names no KEL-103 type of its own.
/// </remarks>
public sealed class DesktopRuntimeHostKel103EndpointProvider
    : IDesktopRuntimeHostEndpointProvider
{
    /// <summary>
    /// The identifier this provider is registered under.
    /// </summary>
    public const string Id = "kel-103-serial";

    /// <summary>
    /// The endpoint kind reported in host diagnostics.
    /// </summary>
    public const string EndpointKind = "Kel103Serial";

    private readonly Func<RuntimeContext, IEndpointAttachmentService>
        attachmentServiceFactory;

    private readonly IEndpointDescriptorRepository definitionRepository;

    /// <summary>
    /// Composes the provider over production serial transport and the exact
    /// KEL-103 definition repository.
    /// </summary>
    public DesktopRuntimeHostKel103EndpointProvider()
        : this(
            CreateProductionAttachmentService,
            new Kel103DefinitionRepository())
    {
    }

    /// <summary>
    /// Composes the provider over an explicitly supplied attachment service.
    /// </summary>
    public DesktopRuntimeHostKel103EndpointProvider(
        Func<RuntimeContext, IEndpointAttachmentService>
            attachmentServiceFactory)
        : this(
            attachmentServiceFactory,
            new Kel103DefinitionRepository())
    {
    }

    /// <summary>
    /// Composes the provider over an explicitly supplied attachment service
    /// and definition repository.
    /// </summary>
    public DesktopRuntimeHostKel103EndpointProvider(
        Func<RuntimeContext, IEndpointAttachmentService>
            attachmentServiceFactory,
        IEndpointDescriptorRepository definitionRepository)
    {
        this.attachmentServiceFactory =
            attachmentServiceFactory
            ?? throw new ArgumentNullException(
                nameof(attachmentServiceFactory));
        this.definitionRepository =
            definitionRepository
            ?? throw new ArgumentNullException(nameof(definitionRepository));
    }

    /// <inheritdoc />
    public string ProviderId => Id;

    /// <inheritdoc />
    public bool Supports(IEndpointConnectionDefinition connectionDefinition)
    {
        ArgumentNullException.ThrowIfNull(connectionDefinition);

        return connectionDefinition
            is DesktopRuntimeHostKel103ConnectionDefinition;
    }

    /// <inheritdoc />
    public IEndpointAttachmentService? CreateAttachmentService(
        RuntimeContext runtimeContext)
    {
        ArgumentNullException.ThrowIfNull(runtimeContext);

        return attachmentServiceFactory(runtimeContext)
            ?? throw new InvalidOperationException(
                "The KEL-103 attachment-service factory returned null.");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DesktopRuntimeHostEndpointAttachment>>
        ResolveAttachmentsAsync(
            DesktopRuntimeHostEndpointProviderContext context,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<DesktopRuntimeHostKel103SerialEndpointProfile> profiles =
            context.EndpointComposition.Kel103SerialEndpoints;
        IReadOnlyList<DesktopRuntimeHostKel103EndpointPlan> plans =
            await DesktopRuntimeHostKel103DefinitionPreflight.ResolveAllAsync(
                profiles,
                definitionRepository,
                cancellationToken);

        if (plans.Count != profiles.Count)
        {
            throw new InvalidDataException(
                "The KEL-103 endpoint plans do not match the configured "
                + "endpoint count.");
        }

        var attachments = new List<DesktopRuntimeHostEndpointAttachment>();

        for (int index = 0; index < plans.Count; index++)
        {
            DesktopRuntimeHostKel103SerialEndpointProfile endpoint =
                profiles[index];
            DesktopRuntimeHostKel103EndpointPlan plan = plans[index];

            if (new EndpointId(endpoint.ExpectedEndpointId)
                != plan.ExpectedEndpointId)
            {
                throw new InvalidDataException(
                    "A KEL-103 endpoint profile does not match its preflight "
                    + "plan.");
            }

            attachments.Add(
                new DesktopRuntimeHostEndpointAttachment(
                    endpoint.ExpectedEndpointId,
                    EndpointKind,
                    (inventory, token) => AttachAsync(
                        inventory,
                        endpoint,
                        plan,
                        token)));
        }

        return attachments;
    }

    private static IEndpointAttachmentService CreateProductionAttachmentService(
        RuntimeContext runtimeContext) =>
        new DesktopRuntimeHostKel103AttachmentService(
            new DesktopRuntimeHostKel103AttachmentFactory(
                runtimeContext,
                new SystemIoPortsSerialByteStreamFactory()));

    private static async Task AttachAsync(
        IRuntimeEndpointAttachmentInventory attachmentInventory,
        DesktopRuntimeHostKel103SerialEndpointProfile endpoint,
        DesktopRuntimeHostKel103EndpointPlan plan,
        CancellationToken cancellationToken)
    {
        var request = new EndpointAttachmentRequest(
            new DesktopRuntimeHostKel103ConnectionDefinition(
                plan.ExpectedEndpointId,
                plan.Definition,
                new SerialTransportOptions(
                    endpoint.SerialPort,
                    endpoint.BaudRate)),
            HostRepositoryDescriptorSource.Instance);

        await attachmentInventory.AttachAsync(
            request,
            cancellationToken);
    }
}
