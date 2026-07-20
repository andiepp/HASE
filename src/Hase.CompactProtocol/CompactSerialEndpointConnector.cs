using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Transport.Serial;

namespace Hase.CompactProtocol;

/// <summary>
/// Opens a configured serial byte stream, establishes a Compact Serial Protocol
/// connection, initializes the endpoint, and transfers ownership of the
/// established connection on success.
/// </summary>
internal sealed class CompactSerialEndpointConnector
{
    private readonly ISerialByteStreamFactory _serialByteStreamFactory;
    private readonly IEndpointDescriptorRepository _descriptorRepository;

    public CompactSerialEndpointConnector(
        ISerialByteStreamFactory serialByteStreamFactory,
        IEndpointDescriptorRepository descriptorRepository)
    {
        _serialByteStreamFactory =
            serialByteStreamFactory
            ?? throw new ArgumentNullException(
                nameof(serialByteStreamFactory));

        _descriptorRepository =
            descriptorRepository
            ?? throw new ArgumentNullException(
                nameof(descriptorRepository));
    }

    /// <summary>
    /// Opens and initializes one manually configured compact serial endpoint.
    /// </summary>
    public async Task<CompactEndpointConnection> ConnectAsync(
        SerialTransportOptions transportOptions,
        EndpointId? expectedEndpointId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            transportOptions);

        ISerialByteStream stream =
            await _serialByteStreamFactory.OpenAsync(
                transportOptions,
                cancellationToken);

        var connection =
            new CompactSerialProtocolConnection(
                stream);

        try
        {
            var initializer =
                new CompactEndpointInitializer(
                    connection,
                    _descriptorRepository);

            EndpointDescriptor descriptor =
                await initializer.InitializeAsync(
                    expectedEndpointId,
                    cancellationToken);

            return new CompactEndpointConnection(
                descriptor,
                connection);
        }
        catch
        {
            await connection.DisposeAsync();

            throw;
        }
    }
}