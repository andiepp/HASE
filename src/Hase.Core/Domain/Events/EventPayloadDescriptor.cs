using Hase.Core.Domain.Data;

namespace Hase.Core.Domain.Events;

/// <summary>
/// Describes the one typed payload carried by an event occurrence.
/// </summary>
public sealed record EventPayloadDescriptor
{
    public EventPayloadDescriptor(
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
    /// Human-readable payload name shown to users.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Optional payload description shown to users.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Describes the event payload value.
    /// </summary>
    public DataDescriptor Data { get; }
}
