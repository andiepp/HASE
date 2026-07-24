using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.CompactProtocol;

/// <summary>
/// Maps one resource-constrained wire Command identifier to one Command in a
/// predefined host-side endpoint descriptor.
/// </summary>
public sealed record CompactCommandMapping
{
    /// <summary>
    /// Initializes one compact Command mapping.
    /// </summary>
    public CompactCommandMapping(
        byte compactCommandId,
        InstrumentId instrumentId,
        DescriptorPath commandPath)
    {
        if (compactCommandId == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(compactCommandId),
                compactCommandId,
                "A compact Command identifier must be nonzero.");
        }

        CompactCommandId =
            compactCommandId;

        InstrumentId =
            instrumentId
            ?? throw new ArgumentNullException(
                nameof(instrumentId));

        CommandPath =
            commandPath
            ?? throw new ArgumentNullException(
                nameof(commandPath));
    }

    /// <summary>
    /// Gets the nonzero compact wire-Command identifier.
    /// </summary>
    public byte CompactCommandId
    {
        get;
    }

    /// <summary>
    /// Gets the target runtime instrument identity.
    /// </summary>
    public InstrumentId InstrumentId
    {
        get;
    }

    /// <summary>
    /// Gets the target runtime Command path.
    /// </summary>
    public DescriptorPath CommandPath
    {
        get;
    }
}
