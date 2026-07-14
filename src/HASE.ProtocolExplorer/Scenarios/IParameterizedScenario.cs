namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Represents a Protocol Explorer scenario that accepts
/// runtime command-line arguments.
/// </summary>
internal interface IParameterizedScenario
    : IScenario
{
    /// <summary>
    /// Executes the scenario using the supplied arguments.
    /// </summary>
    void Execute(
        IReadOnlyList<string> arguments);

    /// <summary>
    /// Executes the scenario without arguments.
    /// </summary>
    void IScenario.Execute()
    {
        Execute(
            Array.Empty<string>());
    }
}