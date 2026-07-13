using Hase.Protocol;
using Hase.ProtocolExplorer.Formatting;
using Hase.ProtocolExplorer.Generators;
using Hase.ProtocolExplorer.Tracing.Model;

namespace Hase.ProtocolExplorer.Scenarios;

internal abstract class ProtocolScenarioBase
    : IScenario
{
    private readonly ProtocolTraceGenerator
        _traceGenerator =
        new();

    private readonly ConsoleTraceFormatter
        _consoleFormatter =
        new();

    public abstract string Name
    {
        get;
    }

    protected abstract ProtocolMessage CreateMessage();

    public void Execute()
    {
        ProtocolMessage message =
            CreateMessage();

        TraceDocument trace =
            _traceGenerator.Generate(
                message);

        _consoleFormatter.Write(
            trace);
    }
}