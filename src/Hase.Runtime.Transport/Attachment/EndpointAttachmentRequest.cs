namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Represents an explicit request to attach one endpoint to a
/// HASE runtime host.
/// </summary>
/// <remarks>
/// Constructing a request does not connect, verify, attach, replace,
/// or mutate a runtime endpoint.
/// </remarks>
public sealed class EndpointAttachmentRequest
{
    /// <summary>
    /// Initializes an explicit endpoint attachment request.
    /// </summary>
    public EndpointAttachmentRequest(
        IEndpointConnectionDefinition connectionDefinition,
        IEndpointDescriptorSource descriptorSource)
    {
        ConnectionDefinition =
            connectionDefinition
            ?? throw new ArgumentNullException(
                nameof(connectionDefinition));

        DescriptorSource =
            descriptorSource
            ?? throw new ArgumentNullException(
                nameof(descriptorSource));
    }

    /// <summary>
    /// Gets the transport-specific endpoint connection definition.
    /// </summary>
    public IEndpointConnectionDefinition ConnectionDefinition
    {
        get;
    }

    /// <summary>
    /// Gets the selected source of the complete endpoint descriptor.
    /// </summary>
    public IEndpointDescriptorSource DescriptorSource
    {
        get;
    }
}