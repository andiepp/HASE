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

    public IReadOnlyList<PropertyDescriptor> Properties { get; init; }
        = [];

    public IReadOnlyList<CommandDescriptor> Commands { get; init; }
        = [];

    public IReadOnlyList<EventDescriptor> Events { get; init; }
        = [];

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