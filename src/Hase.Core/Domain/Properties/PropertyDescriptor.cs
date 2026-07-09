using Hase.Core.Domain.Data;
using Hase.Core.Domain.Identity;

namespace Hase.Core.Domain.Properties;

public sealed record PropertyDescriptor
{
    public PropertyDescriptor(
        PropertyId id,
        string path,
        string displayName,
        DataDescriptor data)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Path = RequireText(path, nameof(path));
        DisplayName = RequireText(displayName, nameof(displayName));
        Data = data ?? throw new ArgumentNullException(nameof(data));
    }

    public PropertyId Id { get; }

    public string Path { get; }

    public string DisplayName { get; }

    public string? Description { get; init; }

    public DataDescriptor Data { get; }

    public PropertyAccessMode AccessMode { get; init; } = PropertyAccessMode.ReadWrite;

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be empty.", parameterName);
        }

        return value.Trim();
    }
}