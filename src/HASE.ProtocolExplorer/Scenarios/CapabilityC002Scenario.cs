using Hase.Protocol;
using Hase.ProtocolExplorer.Hosting;
using Hase.Simulation.Runtime.Environment;

namespace Hase.ProtocolExplorer.Scenarios;

internal sealed class CapabilityC002Scenario
    : CapabilityScenarioBase<ExecuteCommandResponse>
{
    private const double InitialTargetTemperature =
        30.0;

    public CapabilityC002Scenario(
        ProtocolExplorerHost host)
        : base(host)
    {
    }

    public override string Name =>
        "c002";

    protected override string CapabilityTitle =>
        "Capability C-002";

    protected override string Description =>
        "Reset the simulated environment-controller target " +
        "temperature through the byte-level loopback transport.";

    protected override IReadOnlyList<string>
        RuntimeOperationLines =>
        [
            "Reset target temperature to its default value."
        ];

    protected override void PrepareSimulation()
    {
        Host.ControllerSimulation
            .SetTargetTemperature(
                InitialTargetTemperature);
    }

    protected override ProtocolMessage CreateRequest()
    {
        return new ExecuteCommandRequest(
            new CorrelationId(102),
            EnvironmentControllerDescriptorFactory
                .InstrumentId,
            EnvironmentControllerDescriptorFactory
                .ResetTargetTemperatureCommandPath,
            Argument: null);
    }

    protected override void WriteCapabilityResult(
        ExecuteCommandResponse response)
    {
        WriteResultSection(
            $"Result       : {response.Result.Code}",
            $"Return Value : {FormatValue(response.ReturnValue)}");
    }
}