namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Selects the runtime host's descriptor repository as the source of the
/// complete endpoint descriptor.
/// </summary>
/// <remarks>
/// The endpoint attachment lifecycle obtains the authoritative versioned
/// descriptor reference from the endpoint bootstrap response. This source
/// selects where that reference is resolved; it does not supply or override
/// the reference itself.
/// </remarks>
public sealed class HostRepositoryDescriptorSource
    : IEndpointDescriptorSource
{
    private HostRepositoryDescriptorSource()
    {
    }

    /// <summary>
    /// Gets the shared host-repository descriptor-source instance.
    /// </summary>
    public static HostRepositoryDescriptorSource Instance
    {
        get;
    } =
        new();
}