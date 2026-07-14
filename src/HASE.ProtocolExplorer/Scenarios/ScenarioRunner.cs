using Hase.ProtocolExplorer.Scenarios;

namespace Hase.ProtocolExplorer;

internal sealed class ScenarioRunner
{
    private readonly Dictionary<string, IScenario>
        _scenarios;

    public ScenarioRunner(
        Generators.ProtocolTraceGenerator traceGenerator,
        IEnumerable<IScenario> scenarios)
    {
        ArgumentNullException.ThrowIfNull(
            traceGenerator);

        ArgumentNullException.ThrowIfNull(
            scenarios);

        _scenarios =
            scenarios.ToDictionary(
                scenario => scenario.Name,
                StringComparer.OrdinalIgnoreCase);
    }

    public bool TryRun(
        string scenarioName)
    {
        return TryRun(
            scenarioName,
            Array.Empty<string>());
    }

    public bool TryRun(
        string scenarioName,
        IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(
            scenarioName);

        ArgumentNullException.ThrowIfNull(
            arguments);

        if (!_scenarios.TryGetValue(
                scenarioName,
                out IScenario? scenario))
        {
            return false;
        }

        if (scenario is IParameterizedScenario
            parameterizedScenario)
        {
            parameterizedScenario.Execute(
                arguments);
        }
        else
        {
            scenario.Execute();
        }

        return true;
    }
}