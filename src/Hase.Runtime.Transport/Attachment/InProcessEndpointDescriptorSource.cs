namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Selects the complete descriptor carried by an in-process endpoint
/// connection definition.
/// </summary>
public sealed class InProcessEndpointDescriptorSource
    : IEndpointDescriptorSource
{
    private InProcessEndpointDescriptorSource()
    {
    }

    public static InProcessEndpointDescriptorSource Instance
    {
        get;
    } =
        new();
}
