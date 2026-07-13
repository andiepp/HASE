using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Protocol;

namespace Hase.ProtocolExplorer.Scenarios;

internal sealed class ExecuteCommandScenario
    : ProtocolScenarioBase
{
    public override string Name =>
        "command";

    protected override ProtocolMessage
        CreateMessage()
    {
        return new ExecuteCommandRequest(
            new CorrelationId(3),
            new InstrumentId("DDS"),
            DescriptorPath.Parse("DDS.Reset"),
            "Factory");
    }
}