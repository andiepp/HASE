using Hase.Core.Domain.Descriptors;

namespace Hase.CompactProtocol;

/// <summary>
/// Contains one exact host-side compact endpoint definition together with the
/// wire mappings required to operate its properties.
/// </summary>
public sealed class CompactEndpointDefinition
{
    /// <summary>
    /// Initializes one compact endpoint definition.
    /// </summary>
    /// <param name="descriptorReference">
    /// Exact versioned reference reported by authoritative compact bootstrap.
    /// </param>
    /// <param name="descriptorDefinition">
    /// Complete transport-independent endpoint descriptor definition.
    /// </param>
    /// <param name="propertyMappings">
    /// Compact wire-property mappings associated with this exact descriptor
    /// version.
    /// </param>
    public CompactEndpointDefinition(
        DescriptorReference descriptorReference,
        EndpointDescriptorDefinition descriptorDefinition,
        IEnumerable<CompactPropertyMapping> propertyMappings)
    {
        DescriptorReference =
            descriptorReference
            ?? throw new ArgumentNullException(
                nameof(descriptorReference));

        DescriptorDefinition =
            descriptorDefinition
            ?? throw new ArgumentNullException(
                nameof(descriptorDefinition));

        ArgumentNullException.ThrowIfNull(
            propertyMappings);

        CompactPropertyMapping[] mappingArray =
            propertyMappings.ToArray();

        if (mappingArray.Any(
                static mapping =>
                    mapping is null))
        {
            throw new ArgumentException(
                "The compact property mapping collection must not contain "
                + "null values.",
                nameof(propertyMappings));
        }

        _ = new CompactPropertyMap(
            descriptorDefinition,
            mappingArray);

        PropertyMappings =
            mappingArray;
    }

    /// <summary>
    /// Gets the exact versioned descriptor reference identifying this
    /// definition.
    /// </summary>
    public DescriptorReference DescriptorReference
    {
        get;
    }

    /// <summary>
    /// Gets the complete transport-independent descriptor definition.
    /// </summary>
    public EndpointDescriptorDefinition DescriptorDefinition
    {
        get;
    }

    /// <summary>
    /// Gets the validated compact wire-property mappings in declaration order.
    /// </summary>
    public IReadOnlyList<CompactPropertyMapping> PropertyMappings
    {
        get;
    }

    internal CompactPropertyMap CreatePropertyMap()
    {
        return new CompactPropertyMap(
            DescriptorDefinition,
            PropertyMappings);
    }
}