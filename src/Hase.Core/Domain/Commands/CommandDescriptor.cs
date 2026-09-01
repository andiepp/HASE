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

    public CommandDescriptor(
        DescriptorPath path,
        string displayName,
        CommandArgumentDescriptor argument)
        : this(
            path,
            displayName)
    {
        Argument =
            argument
            ?? throw new ArgumentNullException(nameof(argument));
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

    /// <summary>
    /// Describes the one required argument, or null for a parameterless command.
    /// </summary>
    public CommandArgumentDescriptor? Argument { get; }

    /// <summary>
    /// Optional declaration of how this command relates to the other commands
    /// of its instrument.
    /// </summary>
    public CommandPresentation? Presentation { get; init; }

    /// <summary>
    /// Indicates whether this command's argument must be confirmed explicitly
    /// before it may be executed.
    /// </summary>
    /// <remarks>
    /// An instrument declares this for a command whose effect is severe
    /// enough that a default-valued argument must not be accepted. It states
    /// that a confirmation is required, not how to ask for one.
    /// </remarks>
    public bool RequiresExplicitConfirmation { get; init; }
}
