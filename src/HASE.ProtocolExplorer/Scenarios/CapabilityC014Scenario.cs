using Hase.Core.Domain.Endpoints;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport;
using Hase.Transport;
using Hase.Transport.Tcp;

namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Verifies physical runtime-event routing before and after automatic recovery
/// from an ESP32 reset.
/// </summary>
internal sealed class CapabilityC014Scenario
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

    private static readonly TimeSpan UserActionTimeout =
        TimeSpan.FromMinutes(
            2);

    private static readonly TimeSpan RecoveryTimeout =
        TimeSpan.FromMinutes(
            3);

    private static readonly TimeSpan MaximumTimestampDifference =
        TimeSpan.FromMinutes(
            2);

    public string Name =>
        "c014";

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
                "Capability C-014 requires exactly one argument: "
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

        var statusObserver =
            new RecordingConnectionStatusObserver();

        runtimeEvent.Subscribe(
            eventObserver);

        runtimeEndpoint.SubscribeConnectionStatus(
            statusObserver);

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

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task supervisionTask =
            supervisor.RunAsync(
                cancellationTokenSource.Token);

        Task probeTask =
            PhysicalRuntimeEndpointProbeLoop.RunAsync(
                coordinator,
                runtimeEndpoint,
                ProbeInterval,
                ProbeTimeout,
                initialCorrelationId: 14_000,
                cancellationTokenSource.Token);

        try
        {
            WriteStepHeader(
                "1. Verify event routing through the initial session");

            Console.WriteLine(
                "Waiting for the initial runtime endpoint connection...");

            await statusObserver.FirstReady.WaitAsync(
                RecoveryTimeout);

            Console.WriteLine(
                "Runtime endpoint ready.");

            Console.WriteLine();

            Console.WriteLine(
                "Press and release the GPIO17 button once.");

            Console.WriteLine();

            RuntimeEventOccurrence firstOccurrence =
                await eventObserver.FirstOccurrence.WaitAsync(
                    UserActionTimeout);

            ValidateOccurrence(
                firstOccurrence,
                runtimeEvent,
                "initial");

            if (eventObserver.OccurrenceCount != 1)
            {
                throw new InvalidDataException(
                    "The initial physical press did not produce exactly "
                    + "one runtime event occurrence.");
            }

            WriteOccurrence(
                firstOccurrence,
                eventObserver.OccurrenceCount);

            WriteStepHeader(
                "2. Reset the ESP32 and wait for automatic recovery");

            Console.WriteLine(
                "Reset the ESP32 now.");

            Console.WriteLine(
                "Do not restart the Protocol Explorer.");

            Console.WriteLine();

            await statusObserver.FirstFault.WaitAsync(
                RecoveryTimeout);

            Console.WriteLine(
                "Protocol health-probe timeout detected.");

            Console.WriteLine(
                "Transport connection marked as faulted.");

            Console.WriteLine();

            await statusObserver.SecondReady.WaitAsync(
                RecoveryTimeout);

            if (!statusObserver.SawReconnecting)
            {
                throw new InvalidDataException(
                    "The runtime endpoint returned to Ready without an "
                    + "observed Reconnecting state.");
            }

            if (connectionManager.ReplacementCount < 1)
            {
                throw new InvalidDataException(
                    "Automatic recovery did not replace the failed "
                    + "transport connection.");
            }

            Console.WriteLine(
                "Automatic recovery completed.");

            Console.WriteLine(
                $"Transport replacements: "
                + $"{connectionManager.ReplacementCount}");

            Console.WriteLine();

            WriteStepHeader(
                "3. Verify event routing through the replacement session");

            Console.WriteLine(
                "Press and release the GPIO17 button once more.");

            Console.WriteLine();

            RuntimeEventOccurrence secondOccurrence =
                await eventObserver.SecondOccurrence.WaitAsync(
                    UserActionTimeout);

            ValidateOccurrence(
                secondOccurrence,
                runtimeEvent,
                "replacement");

            if (eventObserver.OccurrenceCount != 2)
            {
                throw new InvalidDataException(
                    "The post-recovery press did not produce exactly one "
                    + "additional runtime event occurrence.");
            }

            WriteOccurrence(
                secondOccurrence,
                eventObserver.OccurrenceCount);

            WriteSuccess(
                firstOccurrence,
                secondOccurrence,
                connectionManager.ReplacementCount);
        }
        finally
        {
            cancellationTokenSource.Cancel();

            await ObserveCancellationAsync(
                supervisionTask,
                cancellationTokenSource.Token);

            await ObserveCancellationAsync(
                probeTask,
                cancellationTokenSource.Token);

            runtimeEvent.Unsubscribe(
                eventObserver);

            runtimeEndpoint.UnsubscribeConnectionStatus(
                statusObserver);
        }
    }

    private static async Task ObserveCancellationAsync(
        Task task,
        CancellationToken cancellationToken)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
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
                $"The {occurrenceName} occurrence has a non-null value.");
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
            "Capability C-014";

        Console.WriteLine(
            title);

        Console.WriteLine(
            new string(
                '=',
                title.Length));

        Console.WriteLine();

        Console.WriteLine(
            "Verify physical GPIO17 runtime-event routing before and "
            + "after automatic recovery from an ESP32 reset.");

        Console.WriteLine();

        Console.WriteLine(
            $"Host          : {host}");

        Console.WriteLine(
            $"Port          : {TcpPort}");

        Console.WriteLine(
            $"Probe interval: {ProbeInterval}");

        Console.WriteLine(
            $"Probe timeout : {ProbeTimeout}");

        Console.WriteLine(
            "Instrument ID : "
            + PhysicalEnvironmentEndpointDescriptorFactory
                .ControllerInstrumentId);

        Console.WriteLine(
            "Event path    : "
            + PhysicalEnvironmentEndpointDescriptorFactory
                .ButtonPressedEventPath);

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
        RuntimeEventOccurrence firstOccurrence,
        RuntimeEventOccurrence secondOccurrence,
        long replacementCount)
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
            "Result                    : Success");

        Console.WriteLine(
            "Initial event routing     : Verified");

        Console.WriteLine(
            "Active liveness probing   : Verified");

        Console.WriteLine(
            "Transport fault detection : Verified");

        Console.WriteLine(
            "Automatic recovery        : Verified");

        Console.WriteLine(
            "Duplex replacement        : Verified");

        Console.WriteLine(
            "Router migration          : Verified");

        Console.WriteLine(
            "Post-recovery routing      : Verified");

        Console.WriteLine(
            "Observer resubscription    : Not required");

        Console.WriteLine(
            $"Transport replacements    : {replacementCount}");

        Console.WriteLine(
            "Runtime occurrences       : 2");

        Console.WriteLine(
            $"Initial timestamp         : "
            + $"{firstOccurrence.TimestampUtc:O}");

        Console.WriteLine(
            $"Recovered timestamp       : "
            + $"{secondOccurrence.TimestampUtc:O}");

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

    private sealed class RecordingConnectionStatusObserver
        : IEndpointConnectionStatusObserver
    {
        private readonly TaskCompletionSource _firstReady =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource _firstFault =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource _secondReady =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        private int _readyCount;

        private int _sawReconnecting;

        public Task FirstReady =>
            _firstReady.Task;

        public Task FirstFault =>
            _firstFault.Task;

        public Task SecondReady =>
            _secondReady.Task;

        public bool SawReconnecting =>
            Volatile.Read(
                ref _sawReconnecting)
            != 0;

        public void OnEndpointConnectionStatusChanged(
            EndpointConnectionStatusChanged change)
        {
            ArgumentNullException.ThrowIfNull(
                change);

            switch (change.CurrentStatus.State)
            {
                case EndpointConnectionState.Ready:
                    {
                        int readyCount =
                            Interlocked.Increment(
                                ref _readyCount);

                        if (readyCount == 1)
                        {
                            _firstReady.TrySetResult();
                        }
                        else if (readyCount == 2)
                        {
                            _secondReady.TrySetResult();
                        }

                        break;
                    }

                case EndpointConnectionState.Faulted:
                    {
                        if (Volatile.Read(
                                ref _readyCount)
                            >= 1)
                        {
                            _firstFault.TrySetResult();
                        }

                        break;
                    }

                case EndpointConnectionState.Reconnecting:
                    {
                        if (Volatile.Read(
                                ref _readyCount)
                            >= 1)
                        {
                            Interlocked.Exchange(
                                ref _sawReconnecting,
                                1);
                        }

                        break;
                    }
            }
        }
    }
}