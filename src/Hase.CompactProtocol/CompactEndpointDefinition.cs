using Hase.Core.Domain.Descriptors;

namespace Hase.CompactProtocol;

/// <summary>
/// Contains one exact host-side compact endpoint definition together with the
/// wire mappings required to operate its Properties, Events, and Commands.
/// </summary>
public sealed class CompactEndpointDefinition
{
    /// <summary>
    /// Initializes one compact endpoint definition without Event or Command
    /// mappings.
    /// </summary>
    public CompactEndpointDefinition(
        DescriptorReference descriptorReference,
        EndpointDescriptorDefinition descriptorDefinition,
        IEnumerable<CompactPropertyMapping> propertyMappings)
        : this(
            descriptorReference,
            descriptorDefinition,
            propertyMappings,
            eventMappings: [],
            commandMappings: [])
    {
    }

    /// <summary>
    /// Initializes one compact endpoint definition without Command mappings.
    /// </summary>
    public CompactEndpointDefinition(
        DescriptorReference descriptorReference,
        EndpointDescriptorDefinition descriptorDefinition,
        IEnumerable<CompactPropertyMapping> propertyMappings,
        IEnumerable<CompactEventMapping> eventMappings)
        : this(
            descriptorReference,
            descriptorDefinition,
            propertyMappings,
            eventMappings,
            commandMappings: [])
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
    /// Compact wire-Property mappings associated with this exact descriptor
    /// version.
    /// </param>
    /// <param name="eventMappings">
    /// Compact wire-Event mappings associated with this exact descriptor
    /// version.
    /// </param>
    /// <param name="commandMappings">
    /// Compact wire-Command mappings associated with this exact descriptor
    /// version.
    /// </param>
    public CompactEndpointDefinition(
        DescriptorReference descriptorReference,
        EndpointDescriptorDefinition descriptorDefinition,
        IEnumerable<CompactPropertyMapping> propertyMappings,
        IEnumerable<CompactEventMapping> eventMappings,
        IEnumerable<CompactCommandMapping> commandMappings)
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

        ArgumentNullException.ThrowIfNull(
            commandMappings);

        CompactPropertyMapping[] propertyMappingArray =
            propertyMappings.ToArray();

        if (propertyMappingArray.Any(
                static mapping =>
                    mapping is null))
        {
            throw new ArgumentException(
                "The compact Property mapping collection must not contain "
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
                "The compact Event mapping collection must not contain "
                + "null values.",
                nameof(eventMappings));
        }

        CompactCommandMapping[] commandMappingArray =
            commandMappings.ToArray();

        if (commandMappingArray.Any(
                static mapping =>
                    mapping is null))
        {
            throw new ArgumentException(
                "The compact Command mapping collection must not contain "
                + "null values.",
                nameof(commandMappings));
        }

        _ = new CompactPropertyMap(
            descriptorDefinition,
            propertyMappingArray);

        _ = new CompactEventMap(
            descriptorDefinition,
            eventMappingArray);

        _ = new CompactCommandMap(
            descriptorDefinition,
            commandMappingArray);

        PropertyMappings =
            propertyMappingArray;

        EventMappings =
            eventMappingArray;

        CommandMappings =
            commandMappingArray;
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
    /// Gets the validated compact wire-Property mappings in declaration order.
    /// </summary>
    public IReadOnlyList<CompactPropertyMapping> PropertyMappings
    {
        get;
    }

    /// <summary>
    /// Gets the validated compact wire-Event mappings in declaration order.
    /// </summary>
    public IReadOnlyList<CompactEventMapping> EventMappings
    {
        get;
    }

    /// <summary>
    /// Gets the validated compact wire-Command mappings in declaration order.
    /// </summary>
    public IReadOnlyList<CompactCommandMapping> CommandMappings
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

    internal CompactCommandMap CreateCommandMap()
    {
        return new CompactCommandMap(
            DescriptorDefinition,
            CommandMappings);
    }
}