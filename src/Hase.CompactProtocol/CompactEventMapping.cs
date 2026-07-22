using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.CompactProtocol;

/// <summary>
/// Maps one resource-constrained wire event identifier to one event in a
/// predefined host-side endpoint descriptor.
/// </summary>
public sealed record CompactEventMapping
{
    /// <summary>
    /// Initializes one compact event mapping.
    /// </summary>
    public CompactEventMapping(
        byte compactEventId,
        InstrumentId instrumentId,
        DescriptorPath eventPath,
        CompactEventValueEncoding encoding)
    {
        if (compactEventId == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(compactEventId),
                compactEventId,
                "A compact event identifier must be nonzero.");
        }

        if (!Enum.IsDefined(
                encoding))
        {
            throw new ArgumentOutOfRangeException(
                nameof(encoding),
                encoding,
                "The compact event-value encoding is not defined.");
        }

        CompactEventId =
            compactEventId;

        InstrumentId =
            instrumentId
            ?? throw new ArgumentNullException(
                nameof(instrumentId));

        EventPath =
            eventPath
            ?? throw new ArgumentNullException(
                nameof(eventPath));

        Encoding =
            encoding;
    }

    /// <summary>
    /// Gets the nonzero compact wire-event identifier.
    /// </summary>
    public byte CompactEventId
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
    /// Gets the target runtime event path.
    /// </summary>
    public DescriptorPath EventPath
    {
        get;
    }

    /// <summary>
    /// Gets the compact event-value encoding.
    /// </summary>
    public CompactEventValueEncoding Encoding
    {
        get;
    }
}