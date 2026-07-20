using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Protocol;

namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Bootstraps a native HASE endpoint through Protocol Version 1.
/// </summary>
public sealed class ProtocolNativeEndpointBootstrapper
    : INativeEndpointBootstrapper
{
    private static int _nextCorrelationId;

    /// <inheritdoc />
    public async Task<NativeEndpointBootstrapResult> BootstrapAsync(
        IRuntimeProtocolConnection connection,
        EndpointId? expectedEndpointId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            connection);

        cancellationToken.ThrowIfCancellationRequested();

        var discoverRequest =
            new DiscoverRequest(
                CreateCorrelationId());

        ProtocolMessage discoverResponseMessage =
            await connection.SendAsync(
                discoverRequest,
                cancellationToken);

        if (discoverResponseMessage
            is not DiscoverResponse discoverResponse)
        {
            throw new InvalidDataException(
                "Endpoint bootstrap expected a "
                + $"{nameof(DiscoverResponse)} but received "
                + $"'{discoverResponseMessage.GetType().Name}'.");
        }

        EndpointId authoritativeEndpointId =
            discoverResponse.EndpointId
            ?? throw new InvalidDataException(
                "The discovery response did not contain an "
                + "authoritative endpoint identity.");

        if (expectedEndpointId is not null
            && authoritativeEndpointId
                != expectedEndpointId)
        {
            throw new InvalidDataException(
                $"The discovered endpoint identity "
                + $"'{authoritativeEndpointId.Value}' does not match "
                + $"the expected endpoint identity "
                + $"'{expectedEndpointId.Value}'.");
        }

        var descriptorRequest =
            new ReadEndpointDescriptorRequest(
                CreateCorrelationId(),
                authoritativeEndpointId);

        ProtocolMessage descriptorResponseMessage =
            await connection.SendAsync(
                descriptorRequest,
                cancellationToken);

        if (descriptorResponseMessage
            is not ReadEndpointDescriptorResponse descriptorResponse)
        {
            throw new InvalidDataException(
                "Endpoint bootstrap expected a "
                + $"{nameof(ReadEndpointDescriptorResponse)} but received "
                + $"'{descriptorResponseMessage.GetType().Name}'.");
        }

        if (!descriptorResponse.Result.IsSuccess)
        {
            throw new InvalidDataException(
                "The endpoint returned descriptor result "
                + $"'{descriptorResponse.Result.Code}': "
                + $"{descriptorResponse.Result.Message ?? "(no message)"}.");
        }

        EndpointDescriptor descriptor =
            descriptorResponse.Descriptor
            ?? throw new InvalidDataException(
                "The successful endpoint-descriptor response did not "
                + "contain a descriptor.");

        if (descriptor.Id
            != authoritativeEndpointId)
        {
            throw new InvalidDataException(
                $"The descriptor endpoint identity "
                + $"'{descriptor.Id.Value}' does not match the "
                + $"authoritative endpoint identity "
                + $"'{authoritativeEndpointId.Value}'.");
        }

        return new NativeEndpointBootstrapResult(
            authoritativeEndpointId,
            descriptor);
    }

    private static CorrelationId CreateCorrelationId()
    {
        uint value =
            unchecked(
                (uint)Interlocked.Increment(
                    ref _nextCorrelationId));

        if (value == CorrelationId.None.Value)
        {
            value =
                unchecked(
                    (uint)Interlocked.Increment(
                        ref _nextCorrelationId));
        }

        return new CorrelationId(
            value);
    }
}