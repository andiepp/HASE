namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Identifies a source from which the attachment lifecycle can obtain
/// the complete descriptor for an endpoint.
/// </summary>
/// <remarks>
/// Implementations define source-specific configuration. Descriptor
/// resolution behavior will be introduced separately.
/// </remarks>
public interface IEndpointDescriptorSource
{
}