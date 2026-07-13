using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Protocol;

namespace Hase.ProtocolExplorer.Scenarios;

internal sealed class EventNotificationScenario
    : ProtocolScenarioBase
{
    public override string Name =>
        "event";

    protected override ProtocolMessage
        CreateMessage()
    {
        return new EventNotification(
            new InstrumentId("DDS"),
            DescriptorPath.Parse("DDS.FrequencyChanged"),
            DateTimeOffset.Parse("2026-01-01T12:00:00Z"),
            1_000_000.0);
    }
}