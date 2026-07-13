using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Protocol;

namespace Hase.ProtocolExplorer.Scenarios;

internal sealed class ReadEndpointDescriptorResponseScenario
    : ProtocolScenarioBase
{
    public override string Name =>
        "descriptor-response";

    protected override ProtocolMessage CreateMessage()
    {
        var descriptor =
            new EndpointDescriptor(
                new EndpointId(
                    "Endpoint1"));

        return new ReadEndpointDescriptorResponse(
            new CorrelationId(14),
            ProtocolResult.Success,
            descriptor);
    }
}