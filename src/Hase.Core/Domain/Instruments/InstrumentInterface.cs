using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Events;
using Hase.Core.Domain.Properties;

namespace Hase.Core.Domain.Instruments;

/// <summary>
/// Describes the engineering interface exposed by an instrument.
/// </summary>
public sealed record InstrumentInterface
{
    public InstrumentInterface(
        IEnumerable<PropertyDescriptor>? properties = null,
        IEnumerable<CommandDescriptor>? commands = null,
        IEnumerable<EventDescriptor>? events = null)
    {
        Properties = (properties ?? Enumerable.Empty<PropertyDescriptor>())
            .ToArray();

        Commands = (commands ?? Enumerable.Empty<CommandDescriptor>())
            .ToArray();

        Events = (events ?? Enumerable.Empty<EventDescriptor>())
            .ToArray();
    }

    public IReadOnlyList<PropertyDescriptor> Properties { get; }

    public IReadOnlyList<CommandDescriptor> Commands { get; }

    public IReadOnlyList<EventDescriptor> Events { get; }

    public PropertyDescriptor? FindProperty(DescriptorPath path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return Properties.FirstOrDefault(p => p.Path == path);
    }

    public CommandDescriptor? FindCommand(DescriptorPath path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return Commands.FirstOrDefault(c => c.Path == path);
    }

    public EventDescriptor? FindEvent(DescriptorPath path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return Events.FirstOrDefault(e => e.Path == path);
    }
}