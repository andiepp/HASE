using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;

namespace Hase.CompactProtocol;

/// <summary>
/// Reports authoritative endpoint identity and the exact descriptor reference
/// declared by a compact endpoint.
/// </summary>
internal sealed record CompactBootstrapResponse
{
    public CompactBootstrapResponse(
        byte correlationId,
        EndpointId endpointId,
        DescriptorReference descriptorReference)
    {
        if (correlationId == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(correlationId),
                correlationId,
                "A compact bootstrap response must use a nonzero "
                + "correlation identifier.");
        }

        CorrelationId =
            correlationId;

        EndpointId =
            endpointId
            ?? throw new ArgumentNullException(
                nameof(endpointId));

        DescriptorReference =
            descriptorReference
            ?? throw new ArgumentNullException(
                nameof(descriptorReference));
    }

    public byte CorrelationId
    {
        get;
    }

    public EndpointId EndpointId
    {
        get;
    }

    public DescriptorReference DescriptorReference
    {
        get;
    }
}