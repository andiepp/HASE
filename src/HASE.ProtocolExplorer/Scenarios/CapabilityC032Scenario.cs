namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Defines the Protocol Explorer entry point for authenticated physical
/// northbound gRPC validation.
/// </summary>
internal sealed class CapabilityC032Scenario
{
    /// <summary>
    /// Gets the scenario command name.
    /// </summary>
    public string Name =>
        "c032";

    /// <summary>
    /// Parses the strict C-032 command-line shape.
    /// </summary>
    internal static CapabilityC032Arguments ParseArguments(
        IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(
            arguments);

        if (arguments.Count != 1)
        {
            throw new ArgumentException(
                "Capability C-032 requires exactly one ESP32 host name "
                + "or IP address.",
                nameof(arguments));
        }

        return new CapabilityC032Arguments(
            arguments[0]);
    }
}
