using Hase.Core.Domain.Data;

namespace Hase.Core.Domain.Commands;

/// <summary>
/// Describes the one required typed argument accepted by a command.
/// </summary>
public sealed record CommandArgumentDescriptor
{
    public CommandArgumentDescriptor(
        string displayName,
        DataDescriptor data)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException(
                "Display name must not be empty.",
                nameof(displayName));
        }

        DisplayName = displayName.Trim();
        Data = data ?? throw new ArgumentNullException(nameof(data));
    }

    /// <summary>
    /// Human-readable argument name shown to users.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Optional argument description shown to users.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Describes the required argument value.
    /// </summary>
    public DataDescriptor Data { get; }
}
