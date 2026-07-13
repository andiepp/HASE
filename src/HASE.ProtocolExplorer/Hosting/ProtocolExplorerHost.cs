using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.ProtocolExplorer.Generators;
using Hase.ProtocolExplorer.Transport;
using Hase.Runtime.Execution;
using Hase.Runtime.Protocol;
using Hase.Runtime.Runtime;
using Hase.Simulation.Environment;

namespace Hase.ProtocolExplorer.Hosting;

/// <summary>
/// Hosts the complete Protocol Explorer runtime.
/// </summary>
internal sealed class ProtocolExplorerHost
{
    public ProtocolExplorerHost()
    {
        TraceGenerator =
            new ProtocolTraceGenerator();

        Runtime =
            new RuntimeContext();

        ControllerState =
            new EnvironmentControllerState(
                EnvironmentControllerSimulation
                    .DefaultTargetTemperature);

        ControllerSimulation =
            new EnvironmentControllerSimulation(
                ControllerState);

        EndpointDescriptor endpointDescriptor =
            new(
                new EndpointId(
                    "simulation.endpoint"),
                [
                    Hase.Simulation.Runtime.Environment
                        .EnvironmentControllerDescriptorFactory
                        .CreateDescriptor()
                ]);

        Endpoint =
            Runtime.AddEndpoint(
                endpointDescriptor);

        ControllerInstrument =
            Endpoint.FindInstrument(
                Hase.Simulation.Runtime.Environment
                    .EnvironmentControllerDescriptorFactory
                    .InstrumentId)
            ?? throw new InvalidOperationException(
                "The simulated environment-controller instrument " +
                "was not created.");

        ControllerExecutor =
            new Hase.Simulation.Runtime.Environment
                .EnvironmentControllerInstrumentExecutor(
                    ControllerSimulation);

        ControllerInstrument.ConnectExecutor(
            ControllerExecutor);

        Dispatcher =
            new RuntimeProtocolDispatcher(
                Endpoint);

        Transport =
            new LoopbackProtocolTransport(
                Dispatcher);

        Client =
            new ProtocolClient(
                Transport);
    }

    public ProtocolTraceGenerator TraceGenerator
    {
        get;
    }

    public RuntimeContext Runtime
    {
        get;
    }

    public RuntimeEndpoint Endpoint
    {
        get;
    }

    public RuntimeInstrument ControllerInstrument
    {
        get;
    }

    public EnvironmentControllerState ControllerState
    {
        get;
    }

    public EnvironmentControllerSimulation ControllerSimulation
    {
        get;
    }

    public IInstrumentExecutor ControllerExecutor
    {
        get;
    }

    public IRuntimeProtocolDispatcher Dispatcher
    {
        get;
    }

    public IProtocolTransport Transport
    {
        get;
    }

    public ProtocolClient Client
    {
        get;
    }
}