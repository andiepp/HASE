using Hase.Protocol;
using Hase.ProtocolExplorer.Hosting;
using Hase.Simulation.Runtime.Environment;

namespace Hase.ProtocolExplorer.Scenarios;

internal sealed class CapabilityC001Scenario
    : CapabilityScenarioBase<WritePropertyResponse>
{
    private const double RequestedTargetTemperature =
        25.0;

    public CapabilityC001Scenario(
        ProtocolExplorerHost host)
        : base(host)
    {
    }

    public override string Name =>
        "c001";

    protected override string CapabilityTitle =>
        "Capability C-001";

    protected override string Description =>
        "Write simulated environment-controller target temperature " +
        "through the byte-level loopback transport.";

    protected override IReadOnlyList<string>
        RuntimeOperationLines =>
        [
            $"Write target temperature " +
            $"{FormatTemperature(RequestedTargetTemperature)}"
        ];

    protected override ProtocolMessage CreateRequest()
    {
        return new WritePropertyRequest(
            new CorrelationId(101),
            EnvironmentControllerDescriptorFactory
                .InstrumentId,
            EnvironmentControllerDescriptorFactory
                .TargetTemperaturePropertyId,
            RequestedTargetTemperature);
    }

    protected override void WriteCapabilityResult(
        WritePropertyResponse response)
    {
        WriteResultSection(
            $"Result : {response.Result.Code}");
    }
}