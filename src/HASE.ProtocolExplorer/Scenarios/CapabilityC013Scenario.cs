using Hase.Core.Domain.Endpoints;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport;
using Hase.Transport;
using Hase.Transport.Tcp;

namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Verifies that physical button presses occurring without a connected client
/// are neither queued nor replayed after a new connection is established.
/// </summary>
internal sealed class CapabilityC013Scenario
    : IParameterizedScenario
{
    private const int TcpPort =
        5000;

    private const int MaximumPayloadLength =
        4096;

    private static readonly TimeSpan ConnectionTimeout =
        TimeSpan.FromSeconds(
            3);

    private static readonly TimeSpan UserActionTimeout =
        TimeSpan.FromMinutes(
            2);

    private static readonly TimeSpan ReplayObservationDuration =
        TimeSpan.FromSeconds(
            2);

    private static readonly TimeSpan MaximumTimestampDifference =
        TimeSpan.FromMinutes(
            2);

    public string Name =>
        "c013";

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
                "Capability C-013 requires exactly one argument: "
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

        RuntimeEvent runtimeEvent =
            GetButtonPressedRuntimeEvent(
                runtimeEndpoint);

        var eventObserver =
            new RecordingRuntimeEventObserver();

        runtimeEvent.Subscribe(
            eventObserver);

        try
        {
            WriteStepHeader(
                "1. Verify a notification while connected");

            await RunInitialConnectedPhaseAsync(
                host,
                runtimeEndpoint,
                runtimeEvent,
                eventObserver);

            WriteStepHeader(
                "2. Press the button while disconnected");

            Console.WriteLine(
                "The Protocol Explorer TCP connection is now closed.");

            Console.WriteLine();

            Console.WriteLine(
                "Press and release the GPIO17 button once.");

            Console.WriteLine(
                "After releasing it, press Enter here to reconnect.");

            Console.WriteLine();

            string? confirmation =
                Console.ReadLine();

            if (confirmation is null)
            {
                throw new InvalidOperationException(
                    "Console input ended before the disconnected press "
                    + "was confirmed.");
            }

            if (eventObserver.OccurrenceCount != 1)
            {
                throw new InvalidDataException(
                    "A runtime occurrence was received while the "
                    + "Protocol Explorer was disconnected.");
            }

            Console.WriteLine();

            Console.WriteLine(
                "Disconnected press completed.");

            Console.WriteLine();

            WriteStepHeader(
                "3. Verify no replay and restore connected routing");

            await RunReplacementConnectedPhaseAsync(
                host,
                runtimeEndpoint,
                runtimeEvent,
                eventObserver);

            WriteSuccess(
                eventObserver);
        }
        finally
        {
            runtimeEvent.Unsubscribe(
                eventObserver);
        }
    }

    private static async Task RunInitialConnectedPhaseAsync(
        string host,
        RuntimeEndpoint runtimeEndpoint,
        RuntimeEvent runtimeEvent,
        RecordingRuntimeEventObserver eventObserver)
    {
        ITransportFactory transportFactory =
            CreateTransportFactory(
                host);

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

        await coordinator.ConnectAsync();

        ValidateReady(
            runtimeEndpoint);

        Console.WriteLine(
            "Runtime endpoint ready.");

        Console.WriteLine();

        Console.WriteLine(
            "Press and release the GPIO17 button once.");

        Console.WriteLine();

        RuntimeEventOccurrence occurrence =
            await eventObserver.FirstOccurrence.WaitAsync(
                UserActionTimeout);

        ValidateOccurrence(
            occurrence,
            runtimeEvent,
            "initial connected");

        if (eventObserver.OccurrenceCount != 1)
        {
            throw new InvalidDataException(
                "The first physical press did not produce exactly one "
                + "runtime event occurrence.");
        }

        WriteOccurrence(
            occurrence,
            eventObserver.OccurrenceCount);

        Console.WriteLine(
            "Closing the first coordinator and TCP connection...");

        Console.WriteLine();
    }

    private static async Task RunReplacementConnectedPhaseAsync(
        string host,
        RuntimeEndpoint runtimeEndpoint,
        RuntimeEvent runtimeEvent,
        RecordingRuntimeEventObserver eventObserver)
    {
        ITransportFactory transportFactory =
            CreateTransportFactory(
                host);

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

        Console.WriteLine(
            "Establishing a new coordinator and TCP connection...");

        await coordinator.ConnectAsync();

        ValidateReady(
            runtimeEndpoint);

        Console.WriteLine(
            "Runtime endpoint ready again.");

        Console.WriteLine();

        Console.WriteLine(
            $"Observing for replay for "
            + $"{ReplayObservationDuration.TotalSeconds:0} seconds...");

        await Task.Delay(
            ReplayObservationDuration);

        if (eventObserver.OccurrenceCount != 1)
        {
            throw new InvalidDataException(
                "The button press performed while disconnected was "
                + "replayed after reconnection.");
        }

        Console.WriteLine(
            "No queued or replayed occurrence was received.");

        Console.WriteLine();

        Console.WriteLine(
            "Press and release the GPIO17 button once more while "
            + "connected.");

        Console.WriteLine();

        RuntimeEventOccurrence occurrence =
            await eventObserver.SecondOccurrence.WaitAsync(
                UserActionTimeout);

        ValidateOccurrence(
            occurrence,
            runtimeEvent,
            "replacement connected");

        if (eventObserver.OccurrenceCount != 2)
        {
            throw new InvalidDataException(
                "The final connected press did not produce exactly one "
                + "additional runtime event occurrence.");
        }

        WriteOccurrence(
            occurrence,
            eventObserver.OccurrenceCount);
    }

    private static ITransportFactory CreateTransportFactory(
        string host)
    {
        var options =
            new TcpTransportOptions(
                host,
                TcpPort,
                ConnectionTimeout);

        return new TcpTransportFactory(
            options,
            MaximumPayloadLength);
    }

    private static RuntimeEvent GetButtonPressedRuntimeEvent(
        RuntimeEndpoint runtimeEndpoint)
    {
        RuntimeInstrument runtimeInstrument =
            runtimeEndpoint.FindInstrument(
                PhysicalEnvironmentEndpointDescriptorFactory
                    .ControllerInstrumentId)
            ?? throw new InvalidOperationException(
                "The controller runtime instrument was not found.");

        return runtimeInstrument.FindEvent(
            PhysicalEnvironmentEndpointDescriptorFactory
                .ButtonPressedEventPath)
            ?? throw new InvalidOperationException(
                "The Button Pressed runtime event was not found.");
    }

    private static void ValidateReady(
        RuntimeEndpoint runtimeEndpoint)
    {
        if (runtimeEndpoint.ConnectionStatus.State
            != EndpointConnectionState.Ready)
        {
            throw new InvalidOperationException(
                "The runtime endpoint did not enter the Ready state.");
        }
    }

    private static void ValidateOccurrence(
        RuntimeEventOccurrence occurrence,
        RuntimeEvent expectedEvent,
        string occurrenceName)
    {
        if (!ReferenceEquals(
                occurrence.Event,
                expectedEvent))
        {
            throw new InvalidDataException(
                $"The {occurrenceName} occurrence references the wrong "
                + "runtime event.");
        }

        if (occurrence.Value is not null)
        {
            throw new InvalidDataException(
                $"The {occurrenceName} occurrence has a non-null "
                + "event value.");
        }

        if (occurrence.TimestampUtc.Offset
            != TimeSpan.Zero)
        {
            throw new InvalidDataException(
                $"The {occurrenceName} occurrence timestamp is not UTC.");
        }

        TimeSpan timestampDifference =
            (DateTimeOffset.UtcNow
             - occurrence.TimestampUtc)
            .Duration();

        if (timestampDifference
            > MaximumTimestampDifference)
        {
            throw new InvalidDataException(
                $"The {occurrenceName} occurrence timestamp differs "
                + "from current UTC by "
                + $"{timestampDifference.TotalSeconds:0.000} seconds.");
        }
    }

    private static void WriteHeader(
        string host)
    {
        const string title =
            "Capability C-013";

        Console.WriteLine(
            title);

        Console.WriteLine(
            new string(
                '=',
                title.Length));

        Console.WriteLine();

        Console.WriteLine(
            "Verify that GPIO17 button presses occurring without a "
            + "connected HASE client are not queued or replayed.");

        Console.WriteLine();

        Console.WriteLine(
            $"Host          : {host}");

        Console.WriteLine(
            $"Port          : {TcpPort}");

        Console.WriteLine(
            "Instrument ID : "
            + PhysicalEnvironmentEndpointDescriptorFactory
                .ControllerInstrumentId);

        Console.WriteLine(
            "Event path    : "
            + PhysicalEnvironmentEndpointDescriptorFactory
                .ButtonPressedEventPath);

        Console.WriteLine(
            "Offline queue : None");

        Console.WriteLine(
            "Replay        : None");

        Console.WriteLine();
    }

    private static void WriteStepHeader(
        string step)
    {
        Console.WriteLine(
            step);

        Console.WriteLine(
            new string(
                '-',
                step.Length));

        Console.WriteLine();
    }

    private static void WriteOccurrence(
        RuntimeEventOccurrence occurrence,
        int occurrenceCount)
    {
        Console.WriteLine(
            $"Runtime event   : {occurrence.Event.DisplayName}");

        Console.WriteLine(
            $"Timestamp UTC   : {occurrence.TimestampUtc:O}");

        Console.WriteLine(
            "Value           : <null>");

        Console.WriteLine(
            $"Occurrence count: {occurrenceCount}");

        Console.WriteLine();
    }

    private static void WriteSuccess(
        RecordingRuntimeEventObserver eventObserver)
    {
        const string title =
            "Capability Result";

        Console.WriteLine(
            title);

        Console.WriteLine(
            new string(
                '-',
                title.Length));

        Console.WriteLine();

        Console.WriteLine(
            "Result                  : Success");

        Console.WriteLine(
            "Initial connected press : Verified");

        Console.WriteLine(
            "Disconnected press      : Discarded");

        Console.WriteLine(
            "Offline queue           : None");

        Console.WriteLine(
            "Replay after reconnect  : None");

        Console.WriteLine(
            "New connected press     : Verified");

        Console.WriteLine(
            $"Runtime occurrences     : "
            + $"{eventObserver.OccurrenceCount}");

        Console.WriteLine();
    }

    private sealed class RecordingRuntimeEventObserver
        : IRuntimeEventObserver
    {
        private readonly TaskCompletionSource<RuntimeEventOccurrence>
            _firstOccurrence =
                new(
                    TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<RuntimeEventOccurrence>
            _secondOccurrence =
                new(
                    TaskCreationOptions.RunContinuationsAsynchronously);

        private int _occurrenceCount;

        public Task<RuntimeEventOccurrence> FirstOccurrence =>
            _firstOccurrence.Task;

        public Task<RuntimeEventOccurrence> SecondOccurrence =>
            _secondOccurrence.Task;

        public int OccurrenceCount =>
            Volatile.Read(
                ref _occurrenceCount);

        public void OnRuntimeEventOccurred(
            RuntimeEventOccurrence occurrence)
        {
            ArgumentNullException.ThrowIfNull(
                occurrence);

            int occurrenceCount =
                Interlocked.Increment(
                    ref _occurrenceCount);

            if (occurrenceCount == 1)
            {
                _firstOccurrence.TrySetResult(
                    occurrence);
            }
            else if (occurrenceCount == 2)
            {
                _secondOccurrence.TrySetResult(
                    occurrence);
            }
        }
    }
}