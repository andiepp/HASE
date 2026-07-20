using Hase.Core.Domain.Identity;

namespace Hase.Core.Domain.Descriptors;

/// <summary>
/// Identifies one exact version of a complete descriptor stored in a HASE
/// descriptor repository.
/// </summary>
public sealed record DescriptorReference
{
    public DescriptorReference(
        DescriptorId id,
        ushort version)
    {
        Id =
            id
            ?? throw new ArgumentNullException(
                nameof(id));

        if (version == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(version),
                version,
                "A descriptor version must be greater than zero.");
        }

        Version =
            version;
    }

    /// <summary>
    /// Gets the stable descriptor identity.
    /// </summary>
    public DescriptorId Id
    {
        get;
    }

    /// <summary>
    /// Gets the exact positive descriptor version.
    /// </summary>
    public ushort Version
    {
        get;
    }
}