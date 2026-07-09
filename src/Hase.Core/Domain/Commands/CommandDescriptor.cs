using Hase.Core.Domain.Properties;

namespace Hase.Core.Domain.Commands;

/// <summary>
/// Describes one executable command exposed by an instrument.
/// A command represents behavior, not state.
/// </summary>
public sealed record CommandDescriptor
{
    public CommandDescriptor(
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
    /// Hierarchical path of the command within the instrument.
    /// Example: DDS.Sweep.Start
    /// </summary>
    public DescriptorPath Path { get; }

    /// <summary>
    /// Human readable command name.
    /// Example: "Start Sweep"
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Optional description shown to users.
    /// </summary>
    public string? Description { get; init; }
}