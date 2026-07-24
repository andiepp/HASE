using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;

namespace Hase.CompactProtocol;

/// <summary>
/// Contains the validated compact Command mappings associated with one
/// predefined endpoint descriptor definition.
/// </summary>
internal sealed class CompactCommandMap
{
    private readonly IReadOnlyDictionary<byte, CompactCommandMapping>
        _mappingsByCompactCommandId;

    private readonly IReadOnlyDictionary<
        (InstrumentId InstrumentId, DescriptorPath CommandPath),
        CompactCommandMapping>
        _mappingsByCommandTarget;

    public CompactCommandMap(
        EndpointDescriptorDefinition descriptorDefinition,
        IEnumerable<CompactCommandMapping> mappings)
    {
        DescriptorDefinition =
            descriptorDefinition
            ?? throw new ArgumentNullException(
                nameof(descriptorDefinition));

        ArgumentNullException.ThrowIfNull(
            mappings);

        CompactCommandMapping[] mappingArray =
            mappings.ToArray();

        if (mappingArray.Any(
                mapping =>
                    mapping is null))
        {
            throw new ArgumentException(
                "The compact Command mapping collection must not contain "
                + "null values.",
                nameof(mappings));
        }

        ValidateUniqueCompactCommandIds(
            mappingArray);

        ValidateUniqueCommandTargets(
            mappingArray);

        foreach (
            CompactCommandMapping mapping
            in mappingArray)
        {
            ValidateTargetCommand(
                descriptorDefinition,
                mapping);
        }

        Mappings =
            mappingArray;

        _mappingsByCompactCommandId =
            mappingArray.ToDictionary(
                mapping =>
                    mapping.CompactCommandId);

        _mappingsByCommandTarget =
            mappingArray.ToDictionary(
                mapping => (
                    mapping.InstrumentId,
                    mapping.CommandPath));
    }

    public EndpointDescriptorDefinition DescriptorDefinition
    {
        get;
    }

    public IReadOnlyList<CompactCommandMapping> Mappings
    {
        get;
    }

    public CompactCommandMapping? Find(
        byte compactCommandId)
    {
        if (compactCommandId == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(compactCommandId),
                compactCommandId,
                "A compact Command identifier must be nonzero.");
        }

        _mappingsByCompactCommandId.TryGetValue(
            compactCommandId,
            out CompactCommandMapping? mapping);

        return mapping;
    }

    /// <summary>
    /// Finds the compact mapping for one logical Command target.
    /// </summary>
    public CompactCommandMapping? Find(
        InstrumentId instrumentId,
        DescriptorPath commandPath)
    {
        ArgumentNullException.ThrowIfNull(
            instrumentId);

        ArgumentNullException.ThrowIfNull(
            commandPath);

        _mappingsByCommandTarget.TryGetValue(
            (
                instrumentId,
                commandPath),
            out CompactCommandMapping? mapping);

        return mapping;
    }

    private static void ValidateUniqueCompactCommandIds(
        IEnumerable<CompactCommandMapping> mappings)
    {
        byte? duplicate =
            mappings
                .GroupBy(
                    mapping =>
                        mapping.CompactCommandId)
                .Where(
                    group =>
                        group.Count() > 1)
                .Select(
                    group =>
                        (byte?)group.Key)
                .FirstOrDefault();

        if (duplicate.HasValue)
        {
            throw new ArgumentException(
                $"Compact Command identifier 0x{duplicate.Value:X2} is "
                + "mapped more than once.",
                nameof(mappings));
        }
    }

    private static void ValidateUniqueCommandTargets(
        IEnumerable<CompactCommandMapping> mappings)
    {
        CompactCommandMapping? duplicate =
            mappings
                .GroupBy(
                    mapping => new
                    {
                        mapping.InstrumentId,
                        mapping.CommandPath
                    })
                .Where(
                    group =>
                        group.Count() > 1)
                .Select(
                    group =>
                        group.First())
                .FirstOrDefault();

        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Command '{duplicate.CommandPath}' of instrument "
                + $"'{duplicate.InstrumentId.Value}' is mapped more than "
                + "once.",
                nameof(mappings));
        }
    }

    private static void ValidateTargetCommand(
        EndpointDescriptorDefinition descriptorDefinition,
        CompactCommandMapping mapping)
    {
        InstrumentDescriptor? instrument =
            descriptorDefinition.Instruments.FirstOrDefault(
                candidate =>
                    candidate.Id
                    == mapping.InstrumentId);

        if (instrument is null)
        {
            throw new ArgumentException(
                $"Compact Command identifier "
                + $"0x{mapping.CompactCommandId:X2} refers to unknown "
                + $"instrument '{mapping.InstrumentId.Value}'.",
                nameof(mapping));
        }

        bool commandExists =
            instrument.Interface.Commands.Any(
                command =>
                    command.Path
                    == mapping.CommandPath);

        if (!commandExists)
        {
            throw new ArgumentException(
                $"Compact Command identifier "
                + $"0x{mapping.CompactCommandId:X2} refers to unknown "
                + $"Command '{mapping.CommandPath}' of instrument "
                + $"'{mapping.InstrumentId.Value}'.",
                nameof(mapping));
        }
    }
}