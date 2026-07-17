using Hase.Core.Domain.Data;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Properties;
using Hase.Protocol;
using Hase.ProtocolExplorer.Transport;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport;
using Hase.Transport;
using Hase.Transport.Tcp;

namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Runs a supervised connection to the physical ESP32/BME280 endpoint.
/// </summary>
internal sealed class CapabilityC007Scenario
    : IParameterizedScenario
{
    private const int TcpPort =
        5000;

    private const int MaximumPayloadLength =
        4096;

    private static readonly TimeSpan ConnectionTimeout =
        TimeSpan.FromSeconds(
            3);

    private static readonly TimeSpan ProbeInterval =
        TimeSpan.FromSeconds(
            1);

    private static readonly TimeSpan ProbeTimeout =
        TimeSpan.FromSeconds(
            3);

    public string Name =>
        "c007";

    public void Execute(
        IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(
            arguments);

        ExecuteAsync(
                arguments)
            .GetAwaiter()
            .GetResult();
    }

    private static async Task ExecuteAsync(
        IReadOnlyList<string> arguments)
    {
        if (arguments.Count != 1)
        {
            throw new ArgumentException(
                "Capability C-007 requires exactly one argument: "
                + "the ESP32 host name or IP address.",
                nameof(arguments));
        }

        string host =
            arguments[0];

        WriteHeader(
            host);

        EndpointDescriptor descriptor =
            PhysicalEnvironmentEndpointDescriptorFactory.Create();

        var runtimeContext =
            new RuntimeContext();

        RuntimeEndpoint runtimeEndpoint =
            runtimeContext.AddEndpoint(
                descriptor);

        var options =
            new TcpTransportOptions(
                host,
                TcpPort,
                ConnectionTimeout);

        ITransportFactory transportFactory =
            new TcpTransportFactory(
                options,
                MaximumPayloadLength);

        await using var connectionManager =
            new TransportConnectionManager(
                transportFactory);

        var synchronizer =
            new ProtocolRuntimeEndpointSynchronizer(
                new EndpointDescriptorCompatibilityValidator());

        await using var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                connectionManager,
                runtimeEndpoint,
                synchronizer);

        var supervisor =
            new RuntimeEndpointConnectionSupervisor(
                coordinator,
                new DefaultRuntimeEndpointReconnectPolicy());

        using var statusObserver =
            new ConsoleConnectionStatusObserver(
                runtimeEndpoint,
                connectionManager);

        runtimeEndpoint.SubscribeConnectionStatus(
            statusObserver);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        ConsoleCancelEventHandler cancelHandler =
            (
                sender,
                eventArgs) =>
            {
                eventArgs.Cancel =
                    true;

                cancellationTokenSource.Cancel();
            };

        Console.CancelKeyPress +=
            cancelHandler;

        try
        {
            Console.WriteLine(
                "Starting endpoint connection supervision.");

            Console.WriteLine(
                "The ESP32 may be switched on before or after "
                + "starting this scenario.");

            Console.WriteLine();

            Task supervisionTask =
                supervisor.RunAsync(
                    cancellationTokenSource.Token);

            Task probeTask =
                RunProbeAsync(
                    connectionManager,
                    runtimeEndpoint,
                    cancellationTokenSource.Token);

            await Task.WhenAll(
                supervisionTask,
                probeTask);
        }
        catch (OperationCanceledException)
            when (cancellationTokenSource.IsCancellationRequested)
        {
            Console.WriteLine();

            Console.WriteLine(
                "Connection supervision stopped.");
        }
        finally
        {
            Console.CancelKeyPress -=
                cancelHandler;

            runtimeEndpoint.UnsubscribeConnectionStatus(
                statusObserver);
        }
    }

    private static async Task RunProbeAsync(
        TransportConnectionManager connectionManager,
        RuntimeEndpoint runtimeEndpoint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            connectionManager);

        ArgumentNullException.ThrowIfNull(
            runtimeEndpoint);

        uint correlationIdValue =
            10_000;

        while (true)
        {
            await Task.Delay(
                ProbeInterval,
                cancellationToken);

            if (runtimeEndpoint.ConnectionStatus.State
                != EndpointConnectionState.Ready)
            {
                continue;
            }

            ITransportConnection? connection =
                connectionManager.CurrentConnection;

            if (connection is null
                || connection.State
                    != TransportConnectionState.Connected)
            {
                continue;
            }

            correlationIdValue++;

            if (correlationIdValue == 0)
            {
                correlationIdValue =
                    1;
            }

            var request =
                new ReadPropertyRequest(
                    new CorrelationId(
                        correlationIdValue),
                    PhysicalEnvironmentEndpointDescriptorFactory
                        .InstrumentId,
                    PhysicalEnvironmentEndpointDescriptorFactory
                        .TemperaturePropertyId);

            var client =
                new ProtocolClient(
                    connection);

            using var probeCancellationTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);

            probeCancellationTokenSource.CancelAfter(
                ProbeTimeout);

            try
            {
                ProtocolExchangeResult exchange =
                    await client.SendAsync(
                        request,
                        probeCancellationTokenSource.Token);

                ReadPropertyResponse response =
                    exchange.ResponseMessage
                        as ReadPropertyResponse
                    ?? throw new InvalidDataException(
                        "The connectivity probe did not receive a "
                        + "ReadPropertyResponse.");

                if (!response.Result.IsSuccess)
                {
                    Console.WriteLine(
                        $"[{DateTimeOffset.UtcNow:O}] Probe rejected: "
                        + $"{response.Result.Code} "
                        + $"{response.Result.Message}");

                    continue;
                }

                PropertyValue value =
                    response.PropertyValue
                    ?? throw new InvalidDataException(
                        "The successful connectivity probe did not "
                        + "contain a property value.");

                RuntimeProperty runtimeProperty =
                    FindTemperatureProperty(
                        runtimeEndpoint);

                runtimeProperty.UpdateValue(
                    value);

                string unitSymbol =
                    runtimeProperty.Descriptor.Data
                        is NumericDataDescriptor numericData
                            ? numericData.NativeUnit.Symbol
                            : string.Empty;

                Console.WriteLine(
                    $"[{DateTimeOffset.UtcNow:O}] Probe succeeded: "
                    + $"{value.Value} {unitSymbol}, "
                    + $"{value.TimestampUtc:O}, "
                    + $"{value.Quality}");
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine(
                    $"[{DateTimeOffset.UtcNow:O}] Probe timed out "
                    + $"after {ProbeTimeout.TotalSeconds:0} seconds.");
            }
            catch (Exception exception)
            {
                Console.WriteLine(
                    $"[{DateTimeOffset.UtcNow:O}] Probe failed: "
                    + $"{exception.GetType().Name}: "
                    + $"{exception.Message}");
            }
        }
    }

    private static RuntimeProperty FindTemperatureProperty(
        RuntimeEndpoint runtimeEndpoint)
    {
        RuntimeInstrument instrument =
            runtimeEndpoint.FindInstrument(
                PhysicalEnvironmentEndpointDescriptorFactory
                    .InstrumentId)
            ?? throw new InvalidOperationException(
                "The physical environment instrument was not found.");

        return instrument.FindProperty(
                   PhysicalEnvironmentEndpointDescriptorFactory
                       .TemperaturePropertyId)
               ?? throw new InvalidOperationException(
                   "The physical temperature property was not found.");
    }

    private static void WriteHeader(
        string host)
    {
        const string title =
            "Capability C-007";

        Console.WriteLine(
            title);

        Console.WriteLine(
            new string(
                '=',
                title.Length));

        Console.WriteLine();

        Console.WriteLine(
            "Supervise the physical ESP32/BME280 endpoint through "
            + "HASE Protocol Version 1 over framed TCP.");

        Console.WriteLine();

        Console.WriteLine(
            $"Host               : {host}");

        Console.WriteLine(
            $"Port               : {TcpPort}");

        Console.WriteLine(
            $"Connection timeout : "
            + $"{ConnectionTimeout.TotalSeconds:0} seconds");

        Console.WriteLine(
            $"Probe interval     : "
            + $"{ProbeInterval.TotalSeconds:0} second");

        Console.WriteLine(
            $"Probe timeout      : "
            + $"{ProbeTimeout.TotalSeconds:0} seconds");

        Console.WriteLine();

        Console.WriteLine(
            "Press Ctrl+C to stop.");

        Console.WriteLine();
    }

    private sealed class ConsoleConnectionStatusObserver
        : IEndpointConnectionStatusObserver,
          ITransportExchangeTraceObserver,
          IDisposable
    {
        private readonly RuntimeEndpoint _runtimeEndpoint;
        private readonly TransportConnectionManager _connectionManager;

        private readonly HashSet<ITransportExchangeTraceSource>
            _traceSources =
            new(
                ReferenceEqualityComparer.Instance);

        private readonly object _syncRoot =
            new();

        private bool _disposed;

        public ConsoleConnectionStatusObserver(
            RuntimeEndpoint runtimeEndpoint,
            TransportConnectionManager connectionManager)
        {
            _runtimeEndpoint =
                runtimeEndpoint
                ?? throw new ArgumentNullException(
                    nameof(runtimeEndpoint));

            _connectionManager =
                connectionManager
                ?? throw new ArgumentNullException(
                    nameof(connectionManager));
        }

        public void OnEndpointConnectionStatusChanged(
            EndpointConnectionStatusChanged change)
        {
            ArgumentNullException.ThrowIfNull(
                change);

            if (!ReferenceEquals(
                change.Endpoint,
                _runtimeEndpoint))
            {
                return;
            }

            EnsureCurrentTraceSubscription();

            string timestamp =
                (change.CurrentStatus.ChangedAtUtc
                 ?? DateTimeOffset.UtcNow)
                .ToString(
                    "O");

            Console.WriteLine(
                $"[{timestamp}] "
                + $"{change.PreviousStatus.State} -> "
                + $"{change.CurrentStatus.State}");

            if (!string.IsNullOrWhiteSpace(
                    change.CurrentStatus.Detail))
            {
                Console.WriteLine(
                    $"  {change.CurrentStatus.Detail}");
            }

            if (change.CurrentStatus.State
                is EndpointConnectionState.Ready
                or EndpointConnectionState.Faulted
                or EndpointConnectionState.Reconnecting)
            {
                WriteCachedProperties(
                    _runtimeEndpoint);
            }
        }

        public void OnTransportExchangeCompleted(
            TransportExchangeTrace trace)
        {
            ArgumentNullException.ThrowIfNull(
                trace);

            Console.WriteLine(
                $"[{trace.CompletedAtUtc:O}] "
                + $"Transport exchange #{trace.SequenceNumber}: "
                + $"{trace.Outcome}");

            Console.WriteLine(
                $"  Duration : "
                + $"{trace.Duration.TotalMilliseconds:0.000} ms");

            Console.WriteLine(
                $"  Bytes    : request {trace.RequestByteCount}, "
                + $"response {trace.ResponseByteCount}");

            Console.WriteLine(
                $"  State    : {trace.ConnectionState}");

            if (trace.ExceptionType is not null)
            {
                Console.WriteLine(
                    $"  Exception: {trace.ExceptionType}");

                if (trace.ExceptionMessage is not null)
                {
                    Console.WriteLine(
                        $"  Message  : {trace.ExceptionMessage}");
                }
            }

            Console.WriteLine();
        }

        public void Dispose()
        {
            ITransportExchangeTraceSource[] traceSources;

            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed =
                    true;

                traceSources =
                    _traceSources.ToArray();

                _traceSources.Clear();
            }

            foreach (ITransportExchangeTraceSource traceSource
                     in traceSources)
            {
                traceSource.UnsubscribeTrace(
                    this);
            }
        }

        private void EnsureCurrentTraceSubscription()
        {
            if (_connectionManager.CurrentConnection
                is not ITransportExchangeTraceSource traceSource)
            {
                return;
            }

            lock (_syncRoot)
            {
                if (_disposed
                    || !_traceSources.Add(
                        traceSource))
                {
                    return;
                }
            }

            traceSource.SubscribeTrace(
                this);
        }

        private static void WriteCachedProperties(
            RuntimeEndpoint runtimeEndpoint)
        {
            Console.WriteLine(
                "  Current property cache:");

            foreach (RuntimeInstrument instrument
                     in runtimeEndpoint.Instruments)
            {
                foreach (RuntimeProperty property
                         in instrument.Properties)
                {
                    if (property.CurrentValue is null)
                    {
                        Console.WriteLine(
                            $"    {property.Descriptor.DisplayName}: "
                            + "<no value>");

                        continue;
                    }

                    string unitSymbol =
                        property.Descriptor.Data
                            is NumericDataDescriptor numericData
                                ? numericData.NativeUnit.Symbol
                                : string.Empty;

                    Console.WriteLine(
                        $"    {property.Descriptor.DisplayName}: "
                        + $"{property.CurrentValue.Value} "
                        + $"{unitSymbol}, "
                        + $"{property.CurrentValue.TimestampUtc:O}, "
                        + $"{property.CurrentValue.Quality}");
                }
            }

            Console.WriteLine();
        }
    }
}