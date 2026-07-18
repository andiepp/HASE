using Hase.Core.Domain.Data;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Properties;
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
                connectionManager,
                supervisor);

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
                PhysicalRuntimeEndpointProbeLoop.RunAsync(
                    coordinator,
                    runtimeEndpoint,
                    ProbeInterval,
                    ProbeTimeout,
                    initialCorrelationId: 10_000,
                    cancellationTokenSource.Token);

            await Task.WhenAll(
                supervisionTask,
                probeTask);
        }
        catch (OperationCanceledException)
            when (cancellationTokenSource.IsCancellationRequested)
        {
            WriteStopped();
        }
        catch (IOException)
            when (cancellationTokenSource.IsCancellationRequested)
        {
            WriteStopped();
        }
        finally
        {
            Console.CancelKeyPress -=
                cancelHandler;

            runtimeEndpoint.UnsubscribeConnectionStatus(
                statusObserver);
        }
    }

    private static void WriteStopped()
    {
        Console.WriteLine();

        Console.WriteLine(
            "Connection supervision stopped.");
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

        private readonly RuntimeEndpointConnectionSupervisor _supervisor;

        private readonly HashSet<ITransportExchangeTraceSource>
            _traceSources =
            new(
                ReferenceEqualityComparer.Instance);

        private readonly object _syncRoot =
            new();

        private bool _disposed;

        public ConsoleConnectionStatusObserver(
            RuntimeEndpoint runtimeEndpoint,
            TransportConnectionManager connectionManager,
            RuntimeEndpointConnectionSupervisor supervisor)
        {
            _runtimeEndpoint =
                runtimeEndpoint
                ?? throw new ArgumentNullException(
                    nameof(runtimeEndpoint));

            _connectionManager =
                connectionManager
                ?? throw new ArgumentNullException(
                    nameof(connectionManager));

            _supervisor =
                supervisor
                ?? throw new ArgumentNullException(
                    nameof(supervisor));
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

            WriteDiagnostics(
                _supervisor.GetDiagnostics());

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

        private static void WriteDiagnostics(
            RuntimeEndpointConnectionDiagnostics diagnostics)
        {
            TransportConnectionHealthSnapshot health =
                diagnostics.TransportHealth;

            RuntimeEndpointConnectionStatistics connectionStatistics =
                diagnostics.ConnectionStatistics;

            TransportExchangeStatistics exchangeStatistics =
                diagnostics.ExchangeStatistics;

            Console.WriteLine(
                "  Aggregate diagnostics:");

            Console.WriteLine(
                $"    Transport    : "
                + $"connection {health.HasConnection}, "
                + $"state {health.State?.ToString() ?? "<none>"}, "
                + $"replacements {health.ReplacementCount}");

            Console.WriteLine(
                $"    Connection   : "
                + $"initial attempts "
                + $"{connectionStatistics.InitialConnectionAttemptCount}, "
                + $"initial failures "
                + $"{connectionStatistics.InitialConnectionFailureCount}");

            Console.WriteLine(
                $"    Recovery     : "
                + $"attempts "
                + $"{connectionStatistics.ReconnectAttemptCount}, "
                + $"failures "
                + $"{connectionStatistics.ReconnectFailureCount}, "
                + $"successful "
                + $"{connectionStatistics.SuccessfulRecoveryCount}");

            Console.WriteLine(
                $"    Exchanges    : "
                + $"completed "
                + $"{exchangeStatistics.CompletedExchangeCount}, "
                + $"successful "
                + $"{exchangeStatistics.SuccessfulExchangeCount}, "
                + $"failed "
                + $"{exchangeStatistics.FailedExchangeCount}, "
                + $"cancelled "
                + $"{exchangeStatistics.CancelledExchangeCount}");

            Console.WriteLine(
                $"    Bytes        : "
                + $"request "
                + $"{exchangeStatistics.TotalRequestByteCount}, "
                + $"response "
                + $"{exchangeStatistics.TotalResponseByteCount}");

            Console.WriteLine(
                $"    Total time   : "
                + $"{exchangeStatistics.TotalDuration.TotalMilliseconds:0.000} ms");

            if (connectionStatistics.LastRecoveryCompletedAtUtc.HasValue)
            {
                Console.WriteLine(
                    $"    Last recovery: "
                    + $"{connectionStatistics.LastRecoveryCompletedAtUtc:O}, "
                    + $"{connectionStatistics.LastRecoveryDuration}"
                    + $"");
            }
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