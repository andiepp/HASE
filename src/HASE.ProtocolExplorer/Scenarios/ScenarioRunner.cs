using Hase.ProtocolExplorer.Scenarios;

namespace Hase.ProtocolExplorer;

internal sealed class ScenarioRunner
{
    private readonly Dictionary<string, IScenario> _scenarios;

    public ScenarioRunner(
Generators.ProtocolTraceGenerator traceGenerator, IEnumerable<IScenario> scenarios)
    {
        ArgumentNullException.ThrowIfNull(scenarios);

        _scenarios =
            scenarios.ToDictionary(
                scenario => scenario.Name,
                StringComparer.OrdinalIgnoreCase);
    }

    public bool TryRun(
        string scenarioName)
    {
        ArgumentNullException.ThrowIfNull(scenarioName);

        if (!_scenarios.TryGetValue(
                scenarioName,
                out IScenario? scenario))
        {
            return false;
        }

        scenario.Execute();

        return true;
    }
}