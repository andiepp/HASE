using Hase.Core.Domain.Properties;

namespace Hase.Scpi.Kel103;

public sealed record Kel103InputControlMapping
{
    private Kel103InputControlMapping(
        DescriptorPath commandPath,
        bool inputEnabled,
        bool requiresConfirmation,
        string command)
    {
        CommandPath = commandPath;
        InputEnabled = inputEnabled;
        RequiresConfirmation = requiresConfirmation;
        Command = command;
    }

    public static Kel103InputControlMapping Activate { get; } = new(
        DescriptorPath.Parse("Input.Activate"),
        true,
        false,
        ":INPut ON");

    public static Kel103InputControlMapping Deactivate { get; } = new(
        DescriptorPath.Parse("Input.Deactivate"),
        false,
        false,
        ":INPut OFF");

    public static Kel103InputControlMapping ShortCircuitActivate { get; } = new(
        DescriptorPath.Parse("ShortCircuit.Activate"),
        true,
        true,
        ":INPut ON");

    public static IReadOnlyList<Kel103InputControlMapping> All { get; } =
        Array.AsReadOnly<Kel103InputControlMapping>(
            [Activate, Deactivate, ShortCircuitActivate]);

    public DescriptorPath CommandPath { get; }

    public bool InputEnabled { get; }

    public bool RequiresConfirmation { get; }

    public string Command { get; }
}
