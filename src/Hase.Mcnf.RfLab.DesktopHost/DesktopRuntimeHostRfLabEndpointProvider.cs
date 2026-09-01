using System.IO;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;
using Hase.DesktopHost.Configuration;
using Hase.DesktopHost.Hosting;
using Hase.Mcnf.RfLab;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Hase.Transport.Serial;
using Hase.Mcnf.RfLab.Hosting;

namespace Hase.Mcnf.RfLab.DesktopHost;

/// <summary>
/// Supplies the RF-Lab MCNF nodes this host reaches over serial.
/// </summary>
/// <remarks>
/// The provider owns everything the family needs: the definition preflight
/// that validates a configured endpoint, the connection definition it
/// attaches with, and the attachment service that routes it. A Runtime Host
/// composes this provider; it names no RF-Lab type of its own.
/// </remarks>
public sealed class DesktopRuntimeHostRfLabEndpointProvider
    : IDesktopRuntimeHostEndpointProvider
{
    /// <summary>
    /// The identifier this provider is registered under.
    /// </summary>
    public const string Id = "rf-lab-serial";

    /// <summary>
    /// The endpoint kind reported in host diagnostics.
    /// </summary>
    public const string EndpointKind = "RfLabSerial";

    private readonly Func<RuntimeContext, IEndpointAttachmentService>
        attachmentServiceFactory;

    private readonly IEndpointDescriptorRepository definitionRepository;

    /// <summary>
    /// Composes the provider over production serial transport and the exact
    /// RF-Lab definition repository.
    /// </summary>
    public DesktopRuntimeHostRfLabEndpointProvider()
        : this(
            CreateProductionAttachmentService,
            new RfLabDefinitionRepository())
    {
    }

    /// <summary>
    /// Composes the provider over an explicitly supplied attachment service.
    /// </summary>
    public DesktopRuntimeHostRfLabEndpointProvider(
        Func<RuntimeContext, IEndpointAttachmentService>
            attachmentServiceFactory)
        : this(
            attachmentServiceFactory,
            new RfLabDefinitionRepository())
    {
    }

    /// <summary>
    /// Composes the provider over an explicitly supplied attachment service
    /// and definition repository.
    /// </summary>
    public DesktopRuntimeHostRfLabEndpointProvider(
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
            is DesktopRuntimeHostRfLabConnectionDefinition;
    }

    /// <inheritdoc />
    public IEndpointAttachmentService? CreateAttachmentService(
        RuntimeContext runtimeContext)
    {
        ArgumentNullException.ThrowIfNull(runtimeContext);

        return attachmentServiceFactory(runtimeContext)
            ?? throw new InvalidOperationException(
                "The RF-Lab attachment-service factory returned null.");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DesktopRuntimeHostEndpointAttachment>>
        ResolveAttachmentsAsync(
            DesktopRuntimeHostEndpointProviderContext context,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<DesktopRuntimeHostRfLabSerialEndpointProfile> profiles =
            context.EndpointComposition
                .ForProvider(Id)
                .Select(entry =>
                    new DesktopRuntimeHostRfLabSerialEndpointProfile(
                        entry.ExpectedEndpointId,
                        entry.RequireString("definitionId"),
                        entry.RequireUInt16("definitionVersion"),
                        entry.RequireString("serialPort"),
                        entry.RequireInt32("baudRate")))
                .ToArray();
        IReadOnlyList<DesktopRuntimeHostRfLabEndpointPlan> plans =
            await DesktopRuntimeHostRfLabDefinitionPreflight.ResolveAllAsync(
                profiles,
                definitionRepository,
                cancellationToken);

        if (plans.Count != profiles.Count)
        {
            throw new InvalidDataException(
                "The RF-Lab endpoint plans do not match the configured "
                + "endpoint count.");
        }

        var attachments = new List<DesktopRuntimeHostEndpointAttachment>();

        for (int index = 0; index < plans.Count; index++)
        {
            DesktopRuntimeHostRfLabSerialEndpointProfile endpoint =
                profiles[index];
            DesktopRuntimeHostRfLabEndpointPlan plan = plans[index];

            if (new EndpointId(endpoint.ExpectedEndpointId)
                != plan.ExpectedEndpointId)
            {
                throw new InvalidDataException(
                    "An RF-Lab endpoint profile does not match its preflight "
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
        new DesktopRuntimeHostRfLabAttachmentService(
            new DesktopRuntimeHostRfLabAttachmentFactory(
                runtimeContext,
                new SystemIoPortsSerialByteStreamFactory()));

    private static async Task AttachAsync(
        IRuntimeEndpointAttachmentInventory attachmentInventory,
        DesktopRuntimeHostRfLabSerialEndpointProfile endpoint,
        DesktopRuntimeHostRfLabEndpointPlan plan,
        CancellationToken cancellationToken)
    {
        // The RF-Lab node communicates only with asserted DTR and RTS lines.
        var request = new EndpointAttachmentRequest(
            new DesktopRuntimeHostRfLabConnectionDefinition(
                plan.ExpectedEndpointId,
                plan.Definition,
                new SerialTransportOptions(
                    endpoint.SerialPort,
                    endpoint.BaudRate,
                    dataBits: 8,
                    SerialParity.None,
                    SerialStopBits.One,
                    SerialHandshake.None,
                    assertDataTerminalReady: true,
                    assertRequestToSend: true)),
            HostRepositoryDescriptorSource.Instance);

        await attachmentInventory.AttachAsync(
            request,
            cancellationToken);
    }
}
