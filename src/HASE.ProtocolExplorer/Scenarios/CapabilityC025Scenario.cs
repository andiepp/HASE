using Hase.CompactProtocol;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport;
using Hase.Runtime.Transport.Attachment;
using Hase.Runtime.Transport.Discovery;
using Hase.Transport.Discovery;
using System.Globalization;

namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Discovers, explicitly selects, attaches, and validates unsolicited compact
/// button-event notifications and automatic recovery from an Arduino Uno
/// hardware reset while the USB serial adapter remains connected.
/// </summary>
internal sealed class CapabilityC025Scenario
    : IParameterizedScenario
{
    private const int DefaultBaudRate =
        115200;

    private const int DefaultVerificationTimeoutSeconds =
        3;

    private const ushort ArduinoVendorId =
        0x2341;

    private const ushort ArduinoUnoProductId =
        0x0043;

    private static readonly TimeSpan UserActionTimeout =
        TimeSpan.FromMinutes(
            2);

    private static readonly TimeSpan FaultObservationTimeout =
        TimeSpan.FromSeconds(
            30);

    private static readonly TimeSpan RecoveryTimeout =
        TimeSpan.FromMinutes(
            2);

    private static readonly TimeSpan NoReplayObservationDuration =
        TimeSpan.FromSeconds(
            1);

    public string Name =>
        "c025";

    public void Execute(
        IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(
            arguments);

        (int baudRate, TimeSpan verificationTimeout) =
            ParseArguments(
                arguments);

        ExecuteAsync(
                baudRate,
                verificationTimeout)
            .GetAwaiter()
            .GetResult();
    }

    private static async Task ExecuteAsync(
        int baudRate,
        TimeSpan verificationTimeout)
    {
        CompactEndpointDefinition compactDefinition =
            PhysicalArduinoUnoCompactDescriptorFactory
                .CreateCompactDefinition();

        var definitionRepository =
            new InMemoryCompactEndpointDefinitionRepository(
                [
                    compactDefinition
                ]);

        var descriptorRepository =
            new CompactEndpointDescriptorRepositoryAdapter(
                definitionRepository);

        var candidateFilter =
            new UsbSerialEndpointMetadataFilter(
                vendorId:
                    ArduinoVendorId,
                productId:
                    ArduinoUnoProductId);

        UsbSerialEndpointDiscoveryService discoveryService =
            WindowsUsbSerialEndpointDiscovery.Create(
                descriptorRepository,
                candidateFilter);

        var discoveryOptions =
            new UsbSerialEndpointDiscoveryOptions(
                baudRate,
                verificationTimeout);

        WriteHeader(
            discoveryOptions);

        UsbSerialEndpointDiscoveryResult discoveryResult =
            await discoveryService.DiscoverAsync(
                discoveryOptions);

        if (discoveryResult.VerifiedEndpoints.Count != 1)
        {
            throw new InvalidOperationException(
                "Capability C-025 requires exactly one authoritatively "
                + "verified Arduino Uno endpoint after VID/PID filtering, "
                + $"but found "
                + $"{discoveryResult.VerifiedEndpoints.Count}.");
        }

        VerifiedUsbSerialEndpoint selectedEndpoint =
            discoveryResult.VerifiedEndpoints[0];

        WriteSelectedEndpoint(
            selectedEndpoint);

        SerialEndpointConnectionDefinition connectionDefinition =
            SerialEndpointConnectionDefinition.FromVerifiedEndpoint(
                selectedEndpoint,
                discoveryOptions);

        await using RuntimeEndpointAttachmentHost attachmentHost =
            RuntimeEndpointAttachmentHost.CreateCompactSerial(
                definitionRepository,
                new DefaultRuntimeEndpointReconnectPolicy(),
                CompactEndpointHealthProbeOptions.Default);

        var request =
            new EndpointAttachmentRequest(
                connectionDefinition,
                HostRepositoryDescriptorSource.Instance);

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

        RuntimeEndpointAttachmentInventoryEntry? entry =
            null;

        RuntimeEvent? runtimeEvent =
            null;

        RecordingRuntimeEventObserver? eventObserver =
            null;

        LatchedConnectionStatusObserver? connectionStatusObserver =
            null;

        try
        {
            Console.WriteLine(
                "Attaching the explicitly selected compact endpoint.");

            Console.WriteLine();

            entry =
                await attachmentHost.AttachmentInventory.AttachAsync(
                    request,
                    cancellationTokenSource.Token);

            RuntimeEndpoint runtimeEndpoint =
                entry.RuntimeEndpoint;

            if (runtimeEndpoint.ConnectionStatus.State
                != EndpointConnectionState.Ready)
            {
                throw new InvalidOperationException(
                    "The compact runtime endpoint did not enter the Ready "
                    + "state.");
            }

            runtimeEvent =
                GetButtonPressedRuntimeEvent(
                    runtimeEndpoint);

            eventObserver =
                new RecordingRuntimeEventObserver();

            runtimeEvent.Subscribe(
                eventObserver);

            connectionStatusObserver =
                new LatchedConnectionStatusObserver();

            runtimeEndpoint.SubscribeConnectionStatus(
                connectionStatusObserver);

            WriteReady(
                attachmentHost,
                entry,
                connectionDefinition,
                runtimeEvent);

            WriteStepHeader(
                "1. Verify event delivery before hardware reset");

            Console.WriteLine(
                "Press and release the Arduino Uno D7 pushbutton once.");

            Console.WriteLine();

            RuntimeEventOccurrence firstOccurrence =
                await eventObserver.FirstOccurrence.WaitAsync(
                    UserActionTimeout,
                    cancellationTokenSource.Token);

            ValidateOccurrence(
                firstOccurrence,
                runtimeEvent,
                "first");

            WriteOccurrence(
                firstOccurrence,
                eventObserver.OccurrenceCount);

            if (eventObserver.OccurrenceCount != 1)
            {
                throw new InvalidDataException(
                    "The first validation step did not produce exactly one "
                    + "runtime event occurrence.");
            }

            WriteStepHeader(
                "2. Verify fault detection during Arduino Uno hardware reset");

            Console.WriteLine(
                "Press and hold the Arduino Uno RESET button for at least "
                + "5 seconds, then release it.");

            Console.WriteLine(
                "Do not unplug the USB cable. The COM port must remain "
                + "present while the endpoint processor is held in reset.");

            Console.WriteLine();

            EndpointConnectionStatusChanged faultedChange =
                await connectionStatusObserver.Faulted.WaitAsync(
                    FaultObservationTimeout,
                    cancellationTokenSource.Token);

            Console.WriteLine(
                $"Faulted transition       : "
                + $"{faultedChange.PreviousStatus.State} -> "
                + $"{faultedChange.CurrentStatus.State}");

            Console.WriteLine(
                $"Fault detail             : "
                + $"{faultedChange.CurrentStatus.Detail}");

            Console.WriteLine(
                $"Observed event count     : "
                + $"{eventObserver.OccurrenceCount}");

            Console.WriteLine();

            if (eventObserver.OccurrenceCount != 1)
            {
                throw new InvalidDataException(
                    "An event occurrence was delivered while the endpoint "
                    + "was faulting.");
            }

            WriteStepHeader(
                "3. Verify automatic reset recovery and absence of replay");

            EndpointConnectionStatusChanged readyChange =
                await connectionStatusObserver.ReadyAfterFault.WaitAsync(
                    RecoveryTimeout,
                    cancellationTokenSource.Token);

            Console.WriteLine(
                $"Recovery transition      : "
                + $"{readyChange.PreviousStatus.State} -> "
                + $"{readyChange.CurrentStatus.State}");

            Console.WriteLine(
                "Runtime endpoint recovered to Ready.");

            Console.WriteLine();

            await Task.Delay(
                NoReplayObservationDuration,
                cancellationTokenSource.Token);

            if (eventObserver.OccurrenceCount != 1)
            {
                throw new InvalidDataException(
                    "A compact event occurrence was replayed after reset "
                    + "recovery.");
            }

            Console.WriteLine(
                "Observer subscription     : Preserved");

            Console.WriteLine(
                "Occurrence count after Ready: 1");

            Console.WriteLine(
                "Replay after reset        : None");

            Console.WriteLine();

            WriteStepHeader(
                "4. Verify event delivery after reset connection replacement");

            Console.WriteLine(
                "Press and release the Arduino Uno D7 pushbutton once more.");

            Console.WriteLine();

            RuntimeEventOccurrence secondOccurrence =
                await eventObserver.SecondOccurrence.WaitAsync(
                    UserActionTimeout,
                    cancellationTokenSource.Token);

            ValidateOccurrence(
                secondOccurrence,
                runtimeEvent,
                "second");

            WriteOccurrence(
                secondOccurrence,
                eventObserver.OccurrenceCount);

            if (eventObserver.OccurrenceCount != 2)
            {
                throw new InvalidDataException(
                    "Reset recovery did not produce exactly one new runtime "
                    + "event occurrence from the second physical press.");
            }

            WriteSuccess(
                firstOccurrence,
                secondOccurrence);
        }
        catch (OperationCanceledException)
            when (cancellationTokenSource.IsCancellationRequested)
        {
            Console.WriteLine();

            Console.WriteLine(
                "Compact event validation stop requested.");
        }
        finally
        {
            Console.CancelKeyPress -=
                cancelHandler;

            if (entry is not null
                && connectionStatusObserver is not null)
            {
                entry.RuntimeEndpoint.UnsubscribeConnectionStatus(
                    connectionStatusObserver);
            }

            if (runtimeEvent is not null
                && eventObserver is not null)
            {
                runtimeEvent.Unsubscribe(
                    eventObserver);

                Console.WriteLine();

                Console.WriteLine(
                    $"Observed event count    : "
                    + $"{eventObserver.OccurrenceCount}");
            }

            if (entry is not null)
            {
                bool detached =
                    await attachmentHost.AttachmentInventory.DetachAsync(
                        entry.EndpointId);

                WriteDetachmentResult(
                    attachmentHost,
                    entry,
                    detached);
            }
            else
            {
                Console.WriteLine();

                Console.WriteLine(
                    "No compact endpoint attachment inventory entry "
                    + "was added.");
            }
        }
    }

    internal static (int BaudRate, TimeSpan VerificationTimeout)
        ParseArguments(
            IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(
            arguments);

        if (arguments.Count > 2)
        {
            throw new ArgumentException(
                "Capability C-025 accepts an optional baud rate and an "
                + "optional verification timeout in seconds.",
                nameof(arguments));
        }

        int baudRate =
            arguments.Count >= 1
                ? ParsePositiveInteger(
                    arguments[0],
                    "baud rate")
                : DefaultBaudRate;

        int timeoutSeconds =
            arguments.Count == 2
                ? ParsePositiveInteger(
                    arguments[1],
                    "verification timeout")
                : DefaultVerificationTimeoutSeconds;

        return (
            baudRate,
            TimeSpan.FromSeconds(
                timeoutSeconds));
    }

    private static int ParsePositiveInteger(
        string value,
        string fieldName)
    {
        if (!int.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int parsedValue)
            || parsedValue <= 0)
        {
            throw new ArgumentException(
                $"'{value}' is not a valid positive {fieldName}.",
                nameof(value));
        }

        return parsedValue;
    }

    private static RuntimeEvent GetButtonPressedRuntimeEvent(
        RuntimeEndpoint runtimeEndpoint)
    {
        RuntimeInstrument runtimeInstrument =
            runtimeEndpoint.FindInstrument(
                PhysicalArduinoUnoCompactDescriptorFactory
                    .ControllerInstrumentId)
            ?? throw new InvalidOperationException(
                "The Arduino Uno controller runtime instrument was not "
                + "found.");

        return runtimeInstrument.FindEvent(
            PhysicalArduinoUnoCompactDescriptorFactory
                .ButtonPressedEventPath)
            ?? throw new InvalidOperationException(
                "The Arduino Uno Button Pressed runtime event was not found.");
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
    }

    private static void WriteHeader(
        UsbSerialEndpointDiscoveryOptions options)
    {
        const string title =
            "Capability C-025";

        Console.WriteLine(
            title);

        Console.WriteLine(
            new string(
                '=',
                title.Length));

        Console.WriteLine();

        Console.WriteLine(
            "Discover and authoritatively verify the physical Arduino Uno, "
            + "attach it through the runtime-host inventory, route unsolicited "
            + "compact D7 button notifications into the native runtime event "
            + "model, and verify recovery from a hardware reset while USB "
            + "remains connected.");

        Console.WriteLine();

        Console.WriteLine(
            "Platform             : Windows");

        Console.WriteLine(
            "Candidate filter     : VID 0x2341, PID 0x0043");

        Console.WriteLine(
            $"Baud rate            : {options.BaudRate}");

        Console.WriteLine(
            $"Verification timeout : {options.VerificationTimeout}");

        Console.WriteLine(
            "Protocol             : Compact Serial Protocol V1");

        Console.WriteLine(
            "Descriptor source    : Host repository");

        Console.WriteLine(
            $"Descriptor reference : "
            + $"{PhysicalArduinoUnoCompactDescriptorFactory.DescriptorReference.Id.Value} "
            + $"v{PhysicalArduinoUnoCompactDescriptorFactory.DescriptorReference.Version}");

        Console.WriteLine(
            "Button               : Arduino Uno D7");

        Console.WriteLine(
            "Input mode           : INPUT_PULLUP");

        Console.WriteLine(
            "Active level         : Low");

        Console.WriteLine(
            "Debounce              : 50 ms");

        Console.WriteLine(
            $"Compact event        : "
            + $"0x{PhysicalArduinoUnoCompactDescriptorFactory.ButtonPressedCompactEventId:X2}");

        Console.WriteLine(
            $"Runtime event        : "
            + $"{PhysicalArduinoUnoCompactDescriptorFactory.ButtonPressedEventPath}");

        Console.WriteLine(
            "Event value          : None");

        Console.WriteLine(
            "Offline queue        : None");

        Console.WriteLine(
            "Replay after recovery: Never");

        Console.WriteLine(
            "Reset validation     : USB remains connected");

        Console.WriteLine();

        Console.WriteLine(
            "USB metadata identifies candidates only.");

        Console.WriteLine(
            "CompactBootstrapResponse.EndpointId remains authoritative.");

        Console.WriteLine();

        Console.WriteLine(
            "Beginning discovery and authoritative verification.");

        Console.WriteLine();
    }

    private static void WriteSelectedEndpoint(
        VerifiedUsbSerialEndpoint selectedEndpoint)
    {
        Console.WriteLine(
            "Verified endpoint selected explicitly.");

        Console.WriteLine();

        Console.WriteLine(
            $"Candidate port        : "
            + $"{selectedEndpoint.Candidate.PortName}");

        Console.WriteLine(
            $"VID                   : "
            + $"{FormatIdentifier(selectedEndpoint.Candidate.VendorId)}");

        Console.WriteLine(
            $"PID                   : "
            + $"{FormatIdentifier(selectedEndpoint.Candidate.ProductId)}");

        Console.WriteLine(
            $"Product               : "
            + $"{selectedEndpoint.Candidate.ProductName ?? "Not reported"}");

        Console.WriteLine(
            $"Authoritative endpoint: "
            + $"{selectedEndpoint.EndpointId.Value}");

        Console.WriteLine(
            $"Descriptor reference  : "
            + $"{selectedEndpoint.DescriptorReference.Id.Value} "
            + $"v{selectedEndpoint.DescriptorReference.Version}");

        Console.WriteLine();
    }

    private static void WriteReady(
        RuntimeEndpointAttachmentHost attachmentHost,
        RuntimeEndpointAttachmentInventoryEntry entry,
        SerialEndpointConnectionDefinition connectionDefinition,
        RuntimeEvent runtimeEvent)
    {
        RuntimeEndpointAttachmentInventoryEntry? foundEntry =
            attachmentHost.AttachmentInventory.Find(
                entry.EndpointId);

        Console.WriteLine(
            "Compact endpoint inventory attachment completed.");

        Console.WriteLine();

        Console.WriteLine(
            $"Authoritative endpoint : {entry.EndpointId.Value}");

        Console.WriteLine(
            $"Connection origin      : {connectionDefinition.Origin}");

        Console.WriteLine(
            $"Operational port       : "
            + $"{connectionDefinition.TransportOptions.PortName}");

        Console.WriteLine(
            $"Connection state       : "
            + $"{entry.RuntimeEndpoint.ConnectionStatus.State}");

        Console.WriteLine(
            $"Inventory entries      : "
            + $"{attachmentHost.AttachmentInventory.List().Count}");

        Console.WriteLine(
            $"Authoritative lookup   : "
            + $"{(ReferenceEquals(entry, foundEntry) ? "Same entry" : "Failed")}");

        Console.WriteLine(
            $"Published endpoints    : "
            + $"{attachmentHost.RuntimeContext.Endpoints.Count}");

        Console.WriteLine(
            $"Runtime event          : {runtimeEvent.DisplayName}");

        Console.WriteLine(
            $"Instrument ID          : "
            + $"{runtimeEvent.Instrument.Descriptor.Id}");

        Console.WriteLine(
            $"Event path             : "
            + $"{runtimeEvent.Descriptor.Path}");

        Console.WriteLine();

        Console.WriteLine(
            "Runtime event and connection-status observers are subscribed.");

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
            "Compact button event received.");

        Console.WriteLine(
            $"Runtime event          : {occurrence.Event.DisplayName}");

        Console.WriteLine(
            $"Instrument ID          : "
            + $"{occurrence.Event.Instrument.Descriptor.Id}");

        Console.WriteLine(
            $"Event path             : "
            + $"{occurrence.Event.Descriptor.Path}");

        Console.WriteLine(
            $"Timestamp UTC          : {occurrence.TimestampUtc:O}");

        Console.WriteLine(
            $"Value                  : "
            + $"{(occurrence.Value is null ? "<null>" : occurrence.Value)}");

        Console.WriteLine(
            $"Occurrence count       : {occurrenceCount}");

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
            "Result                  : Success");

        Console.WriteLine(
            "Initial event delivery  : Verified");

        Console.WriteLine(
            "Reset fault detection   : Verified");

        Console.WriteLine(
            "Automatic reset recovery: Verified");

        Console.WriteLine(
            "Observer continuity     : Verified");

        Console.WriteLine(
            "Replay after reset      : None");

        Console.WriteLine(
            "Post-reset delivery     : Verified");

        Console.WriteLine(
            "Runtime event identity  : Preserved");

        Console.WriteLine(
            "Null event value        : Verified");

        Console.WriteLine(
            "UTC timestamps          : Verified");

        Console.WriteLine(
            "Occurrence count        : 2");

        Console.WriteLine(
            $"First timestamp         : {firstOccurrence.TimestampUtc:O}");

        Console.WriteLine(
            $"Second timestamp        : {secondOccurrence.TimestampUtc:O}");

        Console.WriteLine();
    }

    private static void WriteDetachmentResult(
        RuntimeEndpointAttachmentHost attachmentHost,
        RuntimeEndpointAttachmentInventoryEntry entry,
        bool detached)
    {
        Console.WriteLine();

        Console.WriteLine(
            "Compact endpoint inventory detachment completed.");

        Console.WriteLine();

        Console.WriteLine(
            $"Detached               : {detached}");

        Console.WriteLine(
            $"Connection state       : "
            + $"{entry.RuntimeEndpoint.ConnectionStatus.State}");

        Console.WriteLine(
            $"Inventory entries      : "
            + $"{attachmentHost.AttachmentInventory.List().Count}");

        Console.WriteLine(
            $"Published endpoints    : "
            + $"{attachmentHost.RuntimeContext.Endpoints.Count}");

        Console.WriteLine(
            "Operational connection : Disposed");
    }

    private static string FormatIdentifier(
        ushort? value)
    {
        return value.HasValue
            ? $"0x{value.Value:X4}"
            : "Not reported";
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

    private sealed class LatchedConnectionStatusObserver
        : IEndpointConnectionStatusObserver
    {
        private readonly TaskCompletionSource<EndpointConnectionStatusChanged>
            _faulted =
                new(
                    TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<EndpointConnectionStatusChanged>
            _readyAfterFault =
                new(
                    TaskCreationOptions.RunContinuationsAsynchronously);

        private int _faultObserved;

        public Task<EndpointConnectionStatusChanged> Faulted =>
            _faulted.Task;

        public Task<EndpointConnectionStatusChanged> ReadyAfterFault =>
            _readyAfterFault.Task;

        public void OnEndpointConnectionStatusChanged(
            EndpointConnectionStatusChanged change)
        {
            Console.WriteLine(
                $"Connection state         : "
                + $"{change.PreviousStatus.State} -> "
                + $"{change.CurrentStatus.State}");

            if (!string.IsNullOrWhiteSpace(
                    change.CurrentStatus.Detail))
            {
                Console.WriteLine(
                    $"Status detail           : "
                    + $"{change.CurrentStatus.Detail}");
            }

            Console.WriteLine();

            if (change.CurrentStatus.State
                == EndpointConnectionState.Faulted)
            {
                Interlocked.Exchange(
                    ref _faultObserved,
                    1);

                _faulted.TrySetResult(
                    change);

                return;
            }

            if (change.CurrentStatus.State
                    == EndpointConnectionState.Ready
                && Volatile.Read(
                    ref _faultObserved)
                    != 0)
            {
                _readyAfterFault.TrySetResult(
                    change);
            }
        }
    }
}