using Hase.Core.Domain.Data;
using Hase.Core.Domain.Identity;

namespace Hase.Core.Domain.Properties;

/// <summary>
/// Describes one engineering property of an instrument.
/// A PropertyDescriptor is immutable and contains only metadata.
/// It does not contain the current property value.
/// </summary>
public sealed record PropertyDescriptor
{
    public PropertyDescriptor(
        PropertyId id,
        PropertyPath path,
        string displayName,
        DataDescriptor data)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Path = path ?? throw new ArgumentNullException(nameof(path));

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
    /// Unique identifier of the property.
    /// </summary>
    public PropertyId Id { get; }

    /// <summary>
    /// Hierarchical path of the property within the instrument.
    /// Example: DDS.Profile1.Frequency
    /// </summary>
    public PropertyPath Path { get; }

    /// <summary>
    /// Human readable name.
    /// Example: "Frequency"
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Optional description shown to users.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Describes the engineering data exposed by this property.
    /// </summary>
    public DataDescriptor Data { get; }

    /// <summary>
    /// Defines whether the property can be read and/or written.
    /// </summary>
    public PropertyAccessMode AccessMode { get; init; } = PropertyAccessMode.ReadWrite;
}