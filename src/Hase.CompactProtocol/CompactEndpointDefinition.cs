using Hase.Core.Domain.Descriptors;

namespace Hase.CompactProtocol;

/// <summary>
/// Contains one exact host-side compact endpoint definition together with the
/// wire mappings required to operate its properties and events.
/// </summary>
public sealed class CompactEndpointDefinition
{
    /// <summary>
    /// Initializes one compact endpoint definition without event mappings.
    /// </summary>
    public CompactEndpointDefinition(
        DescriptorReference descriptorReference,
        EndpointDescriptorDefinition descriptorDefinition,
        IEnumerable<CompactPropertyMapping> propertyMappings)
        : this(
            descriptorReference,
            descriptorDefinition,
            propertyMappings,
            eventMappings: [])
    {
    }

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
    /// <param name="eventMappings">
    /// Compact wire-event mappings associated with this exact descriptor
    /// version.
    /// </param>
    public CompactEndpointDefinition(
        DescriptorReference descriptorReference,
        EndpointDescriptorDefinition descriptorDefinition,
        IEnumerable<CompactPropertyMapping> propertyMappings,
        IEnumerable<CompactEventMapping> eventMappings)
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

        ArgumentNullException.ThrowIfNull(
            eventMappings);

        CompactPropertyMapping[] propertyMappingArray =
            propertyMappings.ToArray();

        if (propertyMappingArray.Any(
                static mapping =>
                    mapping is null))
        {
            throw new ArgumentException(
                "The compact property mapping collection must not contain "
                + "null values.",
                nameof(propertyMappings));
        }

        CompactEventMapping[] eventMappingArray =
            eventMappings.ToArray();

        if (eventMappingArray.Any(
                static mapping =>
                    mapping is null))
        {
            throw new ArgumentException(
                "The compact event mapping collection must not contain "
                + "null values.",
                nameof(eventMappings));
        }

        _ = new CompactPropertyMap(
            descriptorDefinition,
            propertyMappingArray);

        _ = new CompactEventMap(
            descriptorDefinition,
            eventMappingArray);

        PropertyMappings =
            propertyMappingArray;

        EventMappings =
            eventMappingArray;
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

    /// <summary>
    /// Gets the validated compact wire-event mappings in declaration order.
    /// </summary>
    public IReadOnlyList<CompactEventMapping> EventMappings
    {
        get;
    }

    internal CompactPropertyMap CreatePropertyMap()
    {
        return new CompactPropertyMap(
            DescriptorDefinition,
            PropertyMappings);
    }

    internal CompactEventMap CreateEventMap()
    {
        return new CompactEventMap(
            DescriptorDefinition,
            EventMappings);
    }
}