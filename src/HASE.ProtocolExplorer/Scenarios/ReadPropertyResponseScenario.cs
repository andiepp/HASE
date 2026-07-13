using Hase.Core.Domain.Properties;
using Hase.Protocol;

namespace Hase.ProtocolExplorer.Scenarios;

internal sealed class ReadPropertyResponseScenario
    : ProtocolScenarioBase
{
    public override string Name =>
        "read-response";

    protected override ProtocolMessage CreateMessage()
    {
        var propertyValue =
            new PropertyValue(
                1_000_000.0,
                new DateTimeOffset(
                    2026,
                    1,
                    1,
                    12,
                    0,
                    0,
                    TimeSpan.Zero),
                PropertyQuality.Good);

        return new ReadPropertyResponse(
            new CorrelationId(11),
            ProtocolResult.Success,
            propertyValue);
    }
}