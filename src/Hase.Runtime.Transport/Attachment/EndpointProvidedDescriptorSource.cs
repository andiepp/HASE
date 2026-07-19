namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Selects the complete descriptor returned by the native HASE endpoint
/// through its operational protocol session.
/// </summary>
public sealed class EndpointProvidedDescriptorSource
    : IEndpointDescriptorSource
{
    private EndpointProvidedDescriptorSource()
    {
    }

    /// <summary>
    /// Gets the shared endpoint-provided descriptor-source instance.
    /// </summary>
    public static EndpointProvidedDescriptorSource Instance
    {
        get;
    } =
        new();
}