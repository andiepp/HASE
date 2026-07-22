using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Instruments;

namespace Hase.CompactProtocol;

/// <summary>
/// Contains the validated compact event mappings associated with one
/// predefined endpoint descriptor definition.
/// </summary>
internal sealed class CompactEventMap
{
    private readonly IReadOnlyDictionary<byte, CompactEventMapping>
        _mappingsByCompactEventId;

    public CompactEventMap(
        EndpointDescriptorDefinition descriptorDefinition,
        IEnumerable<CompactEventMapping> mappings)
    {
        DescriptorDefinition =
            descriptorDefinition
            ?? throw new ArgumentNullException(
                nameof(descriptorDefinition));

        ArgumentNullException.ThrowIfNull(
            mappings);

        CompactEventMapping[] mappingArray =
            mappings.ToArray();

        if (mappingArray.Any(
            mapping => mapping is null))
        {
            throw new ArgumentException(
                "The compact event mapping collection must not contain "
                + "null values.",
                nameof(mappings));
        }

        ValidateUniqueCompactEventIds(
            mappingArray);

        ValidateUniqueEventTargets(
            mappingArray);

        foreach (CompactEventMapping mapping in mappingArray)
        {
            ValidateTargetEvent(
                descriptorDefinition,
                mapping);
        }

        Mappings =
            mappingArray;

        _mappingsByCompactEventId =
            mappingArray.ToDictionary(
                mapping => mapping.CompactEventId);
    }

    public EndpointDescriptorDefinition DescriptorDefinition
    {
        get;
    }

    public IReadOnlyList<CompactEventMapping> Mappings
    {
        get;
    }

    public CompactEventMapping? Find(
        byte compactEventId)
    {
        if (compactEventId == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(compactEventId),
                compactEventId,
                "A compact event identifier must be nonzero.");
        }

        _mappingsByCompactEventId.TryGetValue(
            compactEventId,
            out CompactEventMapping? mapping);

        return mapping;
    }

    private static void ValidateUniqueCompactEventIds(
        IEnumerable<CompactEventMapping> mappings)
    {
        byte? duplicate =
            mappings
                .GroupBy(
                    mapping => mapping.CompactEventId)
                .Where(
                    group => group.Count() > 1)
                .Select(
                    group => (byte?)group.Key)
                .FirstOrDefault();

        if (duplicate.HasValue)
        {
            throw new ArgumentException(
                $"Compact event identifier 0x{duplicate.Value:X2} is "
                + "mapped more than once.",
                nameof(mappings));
        }
    }

    private static void ValidateUniqueEventTargets(
        IEnumerable<CompactEventMapping> mappings)
    {
        CompactEventMapping? duplicate =
            mappings
                .GroupBy(
                    mapping => new
                    {
                        mapping.InstrumentId,
                        mapping.EventPath
                    })
                .Where(
                    group => group.Count() > 1)
                .Select(
                    group => group.First())
                .FirstOrDefault();

        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Event '{duplicate.EventPath}' of instrument "
                + $"'{duplicate.InstrumentId.Value}' is mapped more than "
                + "once.",
                nameof(mappings));
        }
    }

    private static void ValidateTargetEvent(
        EndpointDescriptorDefinition descriptorDefinition,
        CompactEventMapping mapping)
    {
        InstrumentDescriptor? instrument =
            descriptorDefinition.Instruments.FirstOrDefault(
                candidate => candidate.Id == mapping.InstrumentId);

        if (instrument is null)
        {
            throw new ArgumentException(
                $"Compact event identifier "
                + $"0x{mapping.CompactEventId:X2} refers to unknown "
                + $"instrument '{mapping.InstrumentId.Value}'.",
                nameof(mapping));
        }

        bool eventExists =
            instrument.Interface.Events.Any(
                eventDescriptor =>
                    eventDescriptor.Path == mapping.EventPath);

        if (!eventExists)
        {
            throw new ArgumentException(
                $"Compact event identifier "
                + $"0x{mapping.CompactEventId:X2} refers to unknown "
                + $"event '{mapping.EventPath}' of instrument "
                + $"'{mapping.InstrumentId.Value}'.",
                nameof(mapping));
        }
    }
}