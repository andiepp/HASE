using Hase.Core.Domain.Properties;

namespace Hase.Core.Domain.Events;

/// <summary>
/// Describes one event that can be emitted by an instrument.
/// An event represents something that happened.
/// </summary>
public sealed record EventDescriptor
{
    public EventDescriptor(
        DescriptorPath path,
        string displayName)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException(
                "Display name must not be empty.",
                nameof(displayName));
        }

        DisplayName = displayName.Trim();
    }

    /// <summary>
    /// Hierarchical path of the event within the instrument.
    /// Example: DDS.PLL.LockLost
    /// </summary>
    public DescriptorPath Path { get; }

    /// <summary>
    /// Human readable event name.
    /// Example: "PLL Lock Lost"
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Optional description shown to users.
    /// </summary>
    public string? Description { get; init; }
}