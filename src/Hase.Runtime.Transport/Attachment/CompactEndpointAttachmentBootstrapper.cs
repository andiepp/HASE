using Hase.CompactProtocol;
using Hase.Core.Domain.Descriptors;
using Hase.Transport.Serial;

namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Bootstraps compact serial endpoints through temporary Compact Serial
/// Protocol connections for explicit runtime-host attachment.
/// </summary>
public sealed class CompactEndpointAttachmentBootstrapper
    : ICompactEndpointAttachmentBootstrapper
{
    private readonly ICompactEndpointConnectionFactory _connectionFactory;

    /// <summary>
    /// Initializes a production compact endpoint attachment bootstrapper from
    /// the compact endpoint-definition repository shared by bootstrap and
    /// operational attachment.
    /// </summary>
    /// <param name="serialByteStreamFactory">
    /// Factory used to open temporary serial byte streams.
    /// </param>
    /// <param name="compactDefinitionRepository">
    /// Runtime-host repository containing complete compact endpoint
    /// definitions and their operational wire mappings.
    /// </param>
    public CompactEndpointAttachmentBootstrapper(
        ISerialByteStreamFactory serialByteStreamFactory,
        ICompactEndpointDefinitionRepository
            compactDefinitionRepository)
        : this(
            serialByteStreamFactory,
            new CompactEndpointDescriptorRepositoryAdapter(
                compactDefinitionRepository
                ?? throw new ArgumentNullException(
                    nameof(compactDefinitionRepository))))
    {
    }

    /// <summary>
    /// Initializes a production compact endpoint attachment bootstrapper from
    /// a transport-independent endpoint descriptor repository.
    /// </summary>
    /// <param name="serialByteStreamFactory">
    /// Factory used to open temporary serial byte streams.
    /// </param>
    /// <param name="descriptorRepository">
    /// Runtime-host repository used to resolve the exact descriptor reference
    /// returned by authoritative compact bootstrap.
    /// </param>
    public CompactEndpointAttachmentBootstrapper(
        ISerialByteStreamFactory serialByteStreamFactory,
        IEndpointDescriptorRepository descriptorRepository)
        : this(
            new CompactSerialEndpointConnector(
                serialByteStreamFactory
                ?? throw new ArgumentNullException(
                    nameof(serialByteStreamFactory)),
                descriptorRepository
                ?? throw new ArgumentNullException(
                    nameof(descriptorRepository))))
    {
    }

    internal CompactEndpointAttachmentBootstrapper(
        ICompactEndpointConnectionFactory connectionFactory)
    {
        _connectionFactory =
            connectionFactory
            ?? throw new ArgumentNullException(
                nameof(connectionFactory));
    }

    /// <inheritdoc />
    public async Task<CompactEndpointAttachmentBootstrapResult>
        BootstrapAsync(
            SerialEndpointConnectionDefinition connectionDefinition,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            connectionDefinition);

        cancellationToken.ThrowIfCancellationRequested();

        await using CompactEndpointConnection connection =
            await _connectionFactory.ConnectAsync(
                connectionDefinition.TransportOptions,
                connectionDefinition.ExpectedEndpointId,
                cancellationToken);

        CompactEndpointInitializationResult initializationResult =
            connection.InitializationResult
            ?? throw new InvalidOperationException(
                "The temporary compact endpoint connection did not preserve "
                + "its authoritative initialization result.");

        return new CompactEndpointAttachmentBootstrapResult(
            initializationResult.EndpointId,
            initializationResult.DescriptorReference,
            initializationResult.DescriptorDefinition,
            initializationResult.Descriptor);
    }
}