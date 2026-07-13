using Hase.Protocol;

namespace Hase.ProtocolExplorer.Scenarios;

internal sealed class ExecuteCommandResponseScenario
    : ProtocolScenarioBase
{
    public override string Name =>
        "command-response";

    protected override ProtocolMessage CreateMessage()
    {
        return new ExecuteCommandResponse(
            new CorrelationId(13),
            ProtocolResult.Success,
            ReturnValue: "Completed");
    }
}