using Hase.Core.Domain.Identity;
using Hase.Protocol;

namespace Hase.ProtocolExplorer.Scenarios;

internal sealed class WritePropertyScenario
    : ProtocolScenarioBase
{
    public override string Name =>
        "write";

    protected override ProtocolMessage
        CreateMessage()
    {
        return new WritePropertyRequest(
            new CorrelationId(2),
            new InstrumentId("DDS"),
            new PropertyId("DDS.Frequency"),
            Value: 1_000_000.0);
    }
}