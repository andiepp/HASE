using Hase.Core.Domain.Identity;
using Hase.Protocol;

namespace Hase.ProtocolExplorer.Scenarios;

internal sealed class DiscoverResponseScenario
    : ProtocolScenarioBase
{
    public override string Name =>
        "discover-response";

    protected override ProtocolMessage CreateMessage()
    {
        return new DiscoverResponse(
            new CorrelationId(10),
            new EndpointId("Lab-PC"),
            [
                new InstrumentId("DDS"),
                new InstrumentId("PowerSupply"),
                new InstrumentId("SpectrumAnalyzer")
            ]);
    }
}