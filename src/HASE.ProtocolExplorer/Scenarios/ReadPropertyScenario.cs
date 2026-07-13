using Hase.Core.Domain.Identity;
using Hase.Protocol;

namespace Hase.ProtocolExplorer.Scenarios;

internal sealed class ReadPropertyScenario
    : ProtocolScenarioBase
{
    public override string Name =>
        "read";

    protected override ProtocolMessage
        CreateMessage()
    {
        return new ReadPropertyRequest(
            new CorrelationId(1),
            new InstrumentId("DDS"),
            new PropertyId("DDS.Frequency"));
    }
}