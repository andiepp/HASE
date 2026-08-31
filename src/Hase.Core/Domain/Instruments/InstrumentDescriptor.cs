using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Events;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Core.Domain.Instruments;

public sealed record InstrumentDescriptor
{
    public InstrumentDescriptor(
        InstrumentId id,
        string name,
        InstrumentKind kind)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));

        Name = RequireText(name, nameof(name));

        Kind = kind ?? throw new ArgumentNullException(nameof(kind));
    }

    public InstrumentId Id { get; }

    public string Name { get; }

    public InstrumentKind Kind { get; }

    public InstrumentMetadata Metadata { get; init; } = new();

    public InstrumentInterface Interface { get; init; } = new();

    /// <summary>
    /// Gets the optional declaration of how this instrument may be presented
    /// as a whole, independently of its individual Properties.
    /// </summary>
    public InstrumentPresentation? Presentation { get; init; }

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Value must not be empty.",
                parameterName);
        }

        return value.Trim();
    }
}