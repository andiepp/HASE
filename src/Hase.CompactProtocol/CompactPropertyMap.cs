using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Instruments;

namespace Hase.CompactProtocol;

/// <summary>
/// Contains the validated compact property mappings associated with one
/// predefined endpoint descriptor definition.
/// </summary>
internal sealed class CompactPropertyMap
{
    private readonly IReadOnlyDictionary<byte, CompactPropertyMapping>
        _mappingsByCompactPropertyId;

    public CompactPropertyMap(
        EndpointDescriptorDefinition descriptorDefinition,
        IEnumerable<CompactPropertyMapping> mappings)
    {
        DescriptorDefinition =
            descriptorDefinition
            ?? throw new ArgumentNullException(
                nameof(descriptorDefinition));

        ArgumentNullException.ThrowIfNull(
            mappings);

        CompactPropertyMapping[] mappingArray =
            mappings.ToArray();

        if (mappingArray.Any(
            mapping => mapping is null))
        {
            throw new ArgumentException(
                "The compact property mapping collection must not contain "
                + "null values.",
                nameof(mappings));
        }

        ValidateUniqueCompactPropertyIds(
            mappingArray);

        ValidateUniquePropertyTargets(
            mappingArray);

        foreach (CompactPropertyMapping mapping in mappingArray)
        {
            ValidateTargetProperty(
                descriptorDefinition,
                mapping);
        }

        Mappings =
            mappingArray;

        _mappingsByCompactPropertyId =
            mappingArray.ToDictionary(
                mapping => mapping.CompactPropertyId);
    }

    public EndpointDescriptorDefinition DescriptorDefinition
    {
        get;
    }

    public IReadOnlyList<CompactPropertyMapping> Mappings
    {
        get;
    }

    public CompactPropertyMapping? Find(
        byte compactPropertyId)
    {
        if (compactPropertyId == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(compactPropertyId),
                compactPropertyId,
                "A compact property identifier must be nonzero.");
        }

        _mappingsByCompactPropertyId.TryGetValue(
            compactPropertyId,
            out CompactPropertyMapping? mapping);

        return mapping;
    }

    private static void ValidateUniqueCompactPropertyIds(
        IEnumerable<CompactPropertyMapping> mappings)
    {
        byte? duplicate =
            mappings
                .GroupBy(
                    mapping => mapping.CompactPropertyId)
                .Where(
                    group => group.Count() > 1)
                .Select(
                    group => (byte?)group.Key)
                .FirstOrDefault();

        if (duplicate.HasValue)
        {
            throw new ArgumentException(
                $"Compact property identifier 0x{duplicate.Value:X2} is "
                + "mapped more than once.",
                nameof(mappings));
        }
    }

    private static void ValidateUniquePropertyTargets(
        IEnumerable<CompactPropertyMapping> mappings)
    {
        CompactPropertyMapping? duplicate =
            mappings
                .GroupBy(
                    mapping => new
                    {
                        mapping.InstrumentId,
                        mapping.PropertyId
                    })
                .Where(
                    group => group.Count() > 1)
                .Select(
                    group => group.First())
                .FirstOrDefault();

        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Property '{duplicate.PropertyId.Value}' of instrument "
                + $"'{duplicate.InstrumentId.Value}' is mapped more than "
                + "once.",
                nameof(mappings));
        }
    }

    private static void ValidateTargetProperty(
        EndpointDescriptorDefinition descriptorDefinition,
        CompactPropertyMapping mapping)
    {
        InstrumentDescriptor? instrument =
            descriptorDefinition.Instruments.FirstOrDefault(
                candidate => candidate.Id == mapping.InstrumentId);

        if (instrument is null)
        {
            throw new ArgumentException(
                $"Compact property identifier "
                + $"0x{mapping.CompactPropertyId:X2} refers to unknown "
                + $"instrument '{mapping.InstrumentId.Value}'.",
                nameof(mapping));
        }

        bool propertyExists =
            instrument.Interface.Properties.Any(
                property => property.Id == mapping.PropertyId);

        if (!propertyExists)
        {
            throw new ArgumentException(
                $"Compact property identifier "
                + $"0x{mapping.CompactPropertyId:X2} refers to unknown "
                + $"property '{mapping.PropertyId.Value}' of instrument "
                + $"'{mapping.InstrumentId.Value}'.",
                nameof(mapping));
        }
    }
}