using Hase.Core.Domain.Properties;

namespace Hase.Scpi.Kel103;

public sealed record Kel103InputControlMapping
{
    private Kel103InputControlMapping(
        DescriptorPath commandPath,
        bool inputEnabled,
        string command)
    {
        CommandPath = commandPath;
        InputEnabled = inputEnabled;
        Command = command;
    }

    public static Kel103InputControlMapping Activate { get; } = new(
        DescriptorPath.Parse("Input.Activate"),
        true,
        ":INPut ON");

    public static Kel103InputControlMapping Deactivate { get; } = new(
        DescriptorPath.Parse("Input.Deactivate"),
        false,
        ":INPut OFF");

    public static IReadOnlyList<Kel103InputControlMapping> All { get; } =
        Array.AsReadOnly<Kel103InputControlMapping>([Activate, Deactivate]);

    public DescriptorPath CommandPath { get; }

    public bool InputEnabled { get; }

    public string Command { get; }
}
