using Hase.CompactProtocol;
using Hase.Runtime.Runtime;
using Hase.Transport.Serial;

namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Explicitly attaches compact serial endpoints to one HASE runtime context.
/// </summary>
public sealed class CompactSerialEndpointAttachmentService
    : IEndpointAttachmentService
{
    private readonly CompactEndpointAttachmentSuccessfulPath
        _successfulPath;

    private readonly Func<
        SerialEndpointConnectionDefinition,
        CancellationToken,
        Task<CompactEndpointAttachmentBootstrapResult>>
        _bootstrapAsync;

    private readonly Func<
        CompactEndpointAttachmentBootstrapResult,
        CancellationToken,
        Task<CompactEndpointDefinition>>
        _resolveOperationalDefinitionAsync;

    private readonly Func<
        SerialEndpointConnectionDefinition,
        CompactEndpointDefinition,
        RuntimeEndpoint,
        ICompactEndpointOperationalResources>
        _createOperationalResources;

    /// <summary>
    /// Initializes a production compact serial endpoint attachment service.
    /// </summary>
    public CompactSerialEndpointAttachmentService(
        RuntimeContext runtimeContext,
        ISerialByteStreamFactory serialByteStreamFactory,
        ICompactEndpointDefinitionRepository definitionRepository,
        IRuntimeEndpointReconnectPolicy reconnectPolicy,
        CompactEndpointHealthProbeOptions probeOptions)
    {
        ArgumentNullException.ThrowIfNull(
            runtimeContext);

        ArgumentNullException.ThrowIfNull(
            serialByteStreamFactory);

        ArgumentNullException.ThrowIfNull(
            definitionRepository);

        ArgumentNullException.ThrowIfNull(
            reconnectPolicy);

        ArgumentNullException.ThrowIfNull(
            probeOptions);

        var bootstrapper =
            new CompactEndpointAttachmentBootstrapper(
                serialByteStreamFactory,
                definitionRepository);

        var operationalDefinitionResolver =
            new CompactEndpointOperationalDefinitionResolver(
                definitionRepository);

        _successfulPath =
            new CompactEndpointAttachmentSuccessfulPath(
                runtimeContext);

        _bootstrapAsync =
            bootstrapper.BootstrapAsync;

        _resolveOperationalDefinitionAsync =
            operationalDefinitionResolver.ResolveAsync;

        _createOperationalResources =
            (
                connectionDefinition,
                definition,
                runtimeEndpoint) =>
                CompactEndpointOperationalResources.CreateSerial(
                    connectionDefinition,
                    definition,
                    runtimeEndpoint,
                    serialByteStreamFactory,
                    definitionRepository,
                    reconnectPolicy,
                    probeOptions);
    }

    internal CompactSerialEndpointAttachmentService(
        RuntimeContext runtimeContext,
        Func<
            SerialEndpointConnectionDefinition,
            CancellationToken,
            Task<CompactEndpointAttachmentBootstrapResult>>
            bootstrapAsync,
        Func<
            CompactEndpointAttachmentBootstrapResult,
            CancellationToken,
            Task<CompactEndpointDefinition>>
            resolveOperationalDefinitionAsync,
        Func<
            SerialEndpointConnectionDefinition,
            CompactEndpointDefinition,
            RuntimeEndpoint,
            ICompactEndpointOperationalResources>
            createOperationalResources)
    {
        ArgumentNullException.ThrowIfNull(
            runtimeContext);

        _bootstrapAsync =
            bootstrapAsync
            ?? throw new ArgumentNullException(
                nameof(bootstrapAsync));

        _resolveOperationalDefinitionAsync =
            resolveOperationalDefinitionAsync
            ?? throw new ArgumentNullException(
                nameof(resolveOperationalDefinitionAsync));

        _createOperationalResources =
            createOperationalResources
            ?? throw new ArgumentNullException(
                nameof(createOperationalResources));

        _successfulPath =
            new CompactEndpointAttachmentSuccessfulPath(
                runtimeContext);
    }

    /// <inheritdoc />
    public async Task<IEndpointAttachmentSession> AttachAsync(
        EndpointAttachmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        cancellationToken.ThrowIfCancellationRequested();

        if (request.DescriptorSource
            is not HostRepositoryDescriptorSource)
        {
            throw new NotSupportedException(
                "Compact serial attachment requires the host-repository "
                + "descriptor source.");
        }

        if (request.ConnectionDefinition
            is not SerialEndpointConnectionDefinition
                connectionDefinition)
        {
            throw new NotSupportedException(
                "Compact serial attachment requires a serial endpoint "
                + "connection definition.");
        }

        CompactEndpointAttachmentBootstrapResult bootstrapResult =
            await _bootstrapAsync(
                connectionDefinition,
                cancellationToken);

        CompactEndpointDefinition operationalDefinition =
            await _resolveOperationalDefinitionAsync(
                bootstrapResult,
                cancellationToken);

        return await _successfulPath.CompleteAsync(
            request,
            bootstrapResult,
            runtimeEndpoint =>
                _createOperationalResources(
                    connectionDefinition,
                    operationalDefinition,
                    runtimeEndpoint),
            cancellationToken);
    }
}