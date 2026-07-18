using Hase.Core.Domain.Endpoints;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport;
using Hase.Transport;
using Hase.Transport.Tcp;

namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Validates physical GPIO17 button event notifications through the runtime
/// endpoint coordinator.
/// </summary>
internal sealed class CapabilityC012Scenario
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

    private static readonly TimeSpan HoldVerificationDuration =
        TimeSpan.FromSeconds(
            1);

    private static readonly TimeSpan MaximumTimestampDifference =
        TimeSpan.FromMinutes(
            2);

    public string Name =>
        "c012";

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
                "Capability C-012 requires exactly one argument: "
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

        try
        {
            Console.WriteLine(
                "Establishing and synchronizing the runtime endpoint...");

            await coordinator.ConnectAsync();

            if (runtimeEndpoint.ConnectionStatus.State
                != Hase.Runtime.Connections.EndpointConnectionState.Ready)
            {
                throw new InvalidOperationException(
                    "The runtime endpoint did not enter the Ready state.");
            }

            Console.WriteLine(
                "Runtime endpoint ready.");

            Console.WriteLine();

            WriteStepHeader(
                "1. Verify the first press and absence of hold repeat");

            Console.WriteLine(
                "Press and hold the GPIO17 button for at least one second.");

            Console.WriteLine();

            RuntimeEventOccurrence firstOccurrence =
                await eventObserver.FirstOccurrence.WaitAsync(
                    UserActionTimeout);

            ValidateOccurrence(
                firstOccurrence,
                runtimeEvent,
                "first");

            await Task.Delay(
                HoldVerificationDuration);

            if (eventObserver.OccurrenceCount != 1)
            {
                throw new InvalidDataException(
                    "Holding the button produced more than one event "
                    + "notification.");
            }

            WriteOccurrence(
                firstOccurrence,
                eventObserver.OccurrenceCount);

            Console.WriteLine(
                "Hold repeat     : None");

            Console.WriteLine();

            WriteStepHeader(
                "2. Verify release rearming");

            Console.WriteLine(
                "Release the button, wait briefly, and press it again.");

            Console.WriteLine();

            RuntimeEventOccurrence secondOccurrence =
                await eventObserver.SecondOccurrence.WaitAsync(
                    UserActionTimeout);

            ValidateOccurrence(
                secondOccurrence,
                runtimeEvent,
                "second");

            if (eventObserver.OccurrenceCount != 2)
            {
                throw new InvalidDataException(
                    "Two physical presses did not produce exactly two "
                    + "runtime event occurrences.");
            }

            WriteOccurrence(
                secondOccurrence,
                eventObserver.OccurrenceCount);

            WriteSuccess(
                firstOccurrence,
                secondOccurrence);
        }
        finally
        {
            runtimeEvent.Unsubscribe(
                eventObserver);
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
                $"The {occurrenceName} button occurrence has a non-null "
                + "event value.");
        }

        if (occurrence.TimestampUtc.Offset
            != TimeSpan.Zero)
        {
            throw new InvalidDataException(
                $"The {occurrenceName} occurrence timestamp is not "
                + "expressed in UTC.");
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
            "Capability C-012";

        Console.WriteLine(
            title);

        Console.WriteLine(
            new string(
                '=',
                title.Length));

        Console.WriteLine();

        Console.WriteLine(
            "Validate physical GPIO17 active-low pushbutton "
            + "notifications through the runtime endpoint coordinator.");

        Console.WriteLine();

        Console.WriteLine(
            $"Host          : {host}");

        Console.WriteLine(
            $"Port          : {TcpPort}");

        Console.WriteLine(
            "GPIO          : 17");

        Console.WriteLine(
            "Input mode    : INPUT_PULLUP");

        Console.WriteLine(
            "Active level  : Low");

        Console.WriteLine(
            "Debounce      : 50 ms");

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
            $"Instrument ID   : "
            + $"{occurrence.Event.Instrument.Descriptor.Id}");

        Console.WriteLine(
            $"Event path      : "
            + $"{occurrence.Event.Descriptor.Path}");

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
        RuntimeEventOccurrence secondOccurrence)
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
            "Result             : Success");

        Console.WriteLine(
            "Runtime routing    : Verified");

        Console.WriteLine(
            "First press        : Verified");

        Console.WriteLine(
            "Hold repeat        : None");

        Console.WriteLine(
            "Release rearming   : Verified");

        Console.WriteLine(
            "Null event value   : Verified");

        Console.WriteLine(
            "UTC timestamps     : Verified");

        Console.WriteLine(
            "Occurrence count   : 2");

        Console.WriteLine(
            $"First timestamp    : {firstOccurrence.TimestampUtc:O}");

        Console.WriteLine(
            $"Second timestamp   : {secondOccurrence.TimestampUtc:O}");

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