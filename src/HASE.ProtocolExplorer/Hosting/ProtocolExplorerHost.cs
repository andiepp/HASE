using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Protocol;
using Hase.ProtocolExplorer.Generators;
using Hase.ProtocolExplorer.Transport;
using Hase.Runtime.Execution;
using Hase.Runtime.Protocol;
using Hase.Runtime.Runtime;
using Hase.Simulation.Environment;
using Hase.Transport;
using Hase.Transport.Loopback;

namespace Hase.ProtocolExplorer.Hosting;

internal sealed class ProtocolExplorerHost
{
    private readonly BinaryProtocolPayloadCodec
        _payloadCodec =
        new();

    private readonly ProtocolEnvelopeByteCodec
        _envelopeByteCodec =
        new();

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

        TransportConnection =
            new LoopbackTransportConnection(
                ExchangeAsync);

        Client =
            new ProtocolClient(
                TransportConnection);
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

    public ITransportConnection TransportConnection
    {
        get;
    }

    public ProtocolClient Client
    {
        get;
    }

    private async Task<byte[]> ExchangeAsync(
        byte[] requestFrame,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            requestFrame);

        cancellationToken.ThrowIfCancellationRequested();

        ProtocolEnvelope requestEnvelope =
            _envelopeByteCodec.Decode(
                requestFrame);

        ProtocolMessage requestMessage =
            _payloadCodec.Decode(
                requestEnvelope);

        ProtocolMessage responseMessage =
            await DispatchAsync(
                requestMessage,
                cancellationToken);

        ProtocolEnvelope responseEnvelope =
            _payloadCodec.Encode(
                responseMessage);

        return _envelopeByteCodec.Encode(
            responseEnvelope);
    }

    private async Task<ProtocolMessage> DispatchAsync(
        ProtocolMessage request,
        CancellationToken cancellationToken)
    {
        return request switch
        {
            DiscoverRequest discoverRequest =>
                await Dispatcher.DispatchAsync(
                    discoverRequest,
                    cancellationToken),

            ReadEndpointDescriptorRequest descriptorRequest =>
                await Dispatcher.DispatchAsync(
                    descriptorRequest,
                    cancellationToken),

            ReadPropertyRequest readPropertyRequest =>
                await Dispatcher.DispatchAsync(
                    readPropertyRequest,
                    cancellationToken),

            WritePropertyRequest writePropertyRequest =>
                await Dispatcher.DispatchAsync(
                    writePropertyRequest,
                    cancellationToken),

            ExecuteCommandRequest executeCommandRequest =>
                await Dispatcher.DispatchAsync(
                    executeCommandRequest,
                    cancellationToken),

            _ =>
                throw new NotSupportedException(
                    $"Runtime dispatch does not support protocol " +
                    $"message type '{request.MessageType}' with role " +
                    $"'{request.Role}'.")
        };
    }
}