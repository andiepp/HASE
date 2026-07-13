using Hase.Protocol;

namespace Hase.ProtocolExplorer.Scenarios;

internal sealed class DiscoverScenario
    : ProtocolScenarioBase
{
    public override string Name =>
        "discover";

    protected override ProtocolMessage
        CreateMessage()
    {
        return new DiscoverRequest(
            CorrelationId.None);
    }
}