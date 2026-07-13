using Hase.Protocol;

namespace Hase.ProtocolExplorer.Scenarios;

internal sealed class WritePropertyResponseScenario
    : ProtocolScenarioBase
{
    public override string Name =>
        "write-response";

    protected override ProtocolMessage CreateMessage()
    {
        return new WritePropertyResponse(
            new CorrelationId(12),
            ProtocolResult.Success,
            PropertyValue: null);
    }
}