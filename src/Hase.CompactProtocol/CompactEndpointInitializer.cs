using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;

namespace Hase.CompactProtocol;

/// <summary>
/// Initializes one compact endpoint over an established Compact Serial Protocol
/// connection by bootstrapping its authoritative identity and descriptor
/// reference, then resolving and materializing the referenced descriptor.
/// </summary>
internal sealed class CompactEndpointInitializer
{
    private readonly CompactEndpointBootstrapper _bootstrapper;
    private readonly CompactEndpointDescriptorResolver _descriptorResolver;

    public CompactEndpointInitializer(
        ICompactSerialProtocolConnection connection,
        IEndpointDescriptorRepository descriptorRepository)
    {
        _bootstrapper =
            new CompactEndpointBootstrapper(
                connection);

        _descriptorResolver =
            new CompactEndpointDescriptorResolver(
                descriptorRepository);
    }

    /// <summary>
    /// Bootstraps the endpoint, validates an optional expected endpoint identity,
    /// and returns the materialized endpoint descriptor.
    /// </summary>
    public async Task<EndpointDescriptor> InitializeAsync(
        EndpointId? expectedEndpointId,
        CancellationToken cancellationToken = default)
    {
        CompactBootstrapResponse bootstrapResponse =
            await _bootstrapper.BootstrapAsync(
                expectedEndpointId,
                cancellationToken);

        return await _descriptorResolver.ResolveAsync(
            bootstrapResponse,
            cancellationToken);
    }
}
