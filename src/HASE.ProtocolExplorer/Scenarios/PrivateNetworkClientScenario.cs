using Grpc.Core;
using Hase.Runtime.Remote.Grpc.Hosting;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Validates authenticated laptop access to a configured private-network
/// runtime host.
/// </summary>
internal sealed class PrivateNetworkClientScenario
    : IParameterizedScenario
{
    public string Name =>
        "private-network-client";

    public void Execute(
        IReadOnlyList<string> arguments)
    {
        PrivateNetworkConfigurationFileArguments parsedArguments =
            ParseArguments(
                arguments);

        ExecuteAsync(
                parsedArguments)
            .GetAwaiter()
            .GetResult();
    }

    internal static PrivateNetworkConfigurationFileArguments ParseArguments(
        IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(
            arguments);

        if (arguments.Count != 1)
        {
            throw new ArgumentException(
                "The private-network client requires exactly one laptop "
                + "configuration file.",
                nameof(arguments));
        }

        return new PrivateNetworkConfigurationFileArguments(
            arguments[0]);
    }

    private static async Task ExecuteAsync(
        PrivateNetworkConfigurationFileArguments arguments)
    {
        RuntimeHostPrivateNetworkClientOptions options =
            await RuntimeHostPrivateNetworkClientOptionsFile.LoadAsync(
                arguments.ConfigurationFilePath);
        using RuntimeHostPrivateNetworkClientDeployment deployment =
            RuntimeHostPrivateNetworkClientDeployment.Create(
                options);

        using var observationCancellation =
            new CancellationTokenSource(
                TimeSpan.FromSeconds(
                    60));
        using AsyncServerStreamingCall<GrpcV1.ObserveResponse>
            observationCall =
                deployment.Client.Client.Observe(
                    new GrpcV1.ObserveRequest(),
                    deadline:
                        DateTime.UtcNow.AddSeconds(
                            60),
                    cancellationToken:
                        observationCancellation.Token);

        GrpcV1.ObserveResponse initialResponse =
            await ReadNextObservationResponseAsync(
                observationCall,
                observationCancellation.Token);

        if (initialResponse.ContentCase
                != GrpcV1.ObserveResponse.ContentOneofCase.InitialSnapshot
            || initialResponse.InitialSnapshot.Snapshot.Endpoints.Count != 2)
        {
            throw new InvalidDataException(
                "The authenticated observation stream did not begin with "
                + "the required two-endpoint snapshot.");
        }

        GrpcV1.GetSnapshotResponse response =
            initialResponse.InitialSnapshot.Snapshot;
        ulong lastObservationSequence =
            initialResponse.InitialSnapshot.SnapshotSequence;

        GrpcV1.ReadAuthoritativePropertyRequest esp32Request =
            CreatePropertyReadRequest(
                response,
                PhysicalEnvironmentEndpointDescriptorFactory
                    .InstrumentId
                    .Value,
                PhysicalEnvironmentEndpointDescriptorFactory
                    .TemperaturePropertyId
                    .Value);
        GrpcV1.ReadAuthoritativePropertyRequest arduinoRequest =
            CreatePropertyReadRequest(
                response,
                PhysicalArduinoUnoCompactDescriptorFactory
                    .ControllerInstrumentId
                    .Value,
                PhysicalArduinoUnoCompactDescriptorFactory
                    .BuiltInLedStatePropertyId
                    .Value);

        GrpcV1.PropertyOperationResult esp32Result =
            await deployment.Client.Client.ReadAuthoritativePropertyAsync(
                esp32Request,
                deadline:
                    DateTime.UtcNow.AddSeconds(
                        10));
        GrpcV1.PropertyOperationResult arduinoResult =
            await deployment.Client.Client.ReadAuthoritativePropertyAsync(
                arduinoRequest,
                deadline:
                    DateTime.UtcNow.AddSeconds(
                        10));

        ValidateSuccessfulRead(
            esp32Result,
            GrpcV1.RemoteValue.KindOneofCase.NumericValue,
            "native endpoint");
        ValidateSuccessfulRead(
            arduinoResult,
            GrpcV1.RemoteValue.KindOneofCase.BooleanValue,
            "compact endpoint");

        GrpcV1.RuntimeHostObservation propertyBaseline =
            await ReadMatchingObservationAsync(
                observationCall,
                arduinoRequest.Target,
                IsCompactPropertyObservation,
                lastObservationSequence,
                observationCancellation.Token);
        lastObservationSequence =
            propertyBaseline.Sequence;

        bool originalArduinoState =
            GetBooleanValue(
                arduinoResult,
                "original compact endpoint state");
        GrpcV1.ExecuteCommandRequest toggleRequest =
            CreateCommandRequest(
                arduinoRequest,
                PhysicalArduinoUnoCompactDescriptorFactory
                    .ToggleBuiltInLedCommandPath
                    .Segments);

        GrpcV1.CommandOperationResult toggleResult =
            await deployment.Client.Client.ExecuteCommandAsync(
                toggleRequest,
                deadline:
                    DateTime.UtcNow.AddSeconds(
                        10));
        ValidateSuccessfulCommand(
            toggleResult,
            "compact endpoint toggle");

        try
        {
            GrpcV1.PropertyOperationResult toggledReadResult =
                await deployment.Client.Client
                    .ReadAuthoritativePropertyAsync(
                        arduinoRequest,
                        deadline:
                            DateTime.UtcNow.AddSeconds(
                                10));
            ValidateSuccessfulRead(
                toggledReadResult,
                GrpcV1.RemoteValue.KindOneofCase.BooleanValue,
                "toggled compact endpoint");

            if (GetBooleanValue(
                    toggledReadResult,
                    "toggled compact endpoint state")
                == originalArduinoState)
            {
                throw new InvalidDataException(
                    "The authoritative Property read did not confirm the "
                    + "compact endpoint Command result.");
            }

            GrpcV1.RuntimeHostObservation changedPropertyObservation =
                await ReadMatchingObservationAsync(
                    observationCall,
                    arduinoRequest.Target,
                    IsCompactPropertyObservation,
                    lastObservationSequence,
                    observationCancellation.Token);
            lastObservationSequence =
                changedPropertyObservation.Sequence;

            if (changedPropertyObservation
                    .PropertyValueChanged
                    .CurrentValue
                    .Value
                    .KindCase
                    != GrpcV1.RemoteValue.KindOneofCase.BooleanValue
                || changedPropertyObservation
                    .PropertyValueChanged
                    .CurrentValue
                    .Value
                    .BooleanValue
                    == originalArduinoState)
            {
                throw new InvalidDataException(
                    "The observed compact Property notification did not "
                    + "confirm the Command result.");
            }
        }
        finally
        {
            GrpcV1.CommandOperationResult restorationResult =
                await deployment.Client.Client.ExecuteCommandAsync(
                    toggleRequest,
                    deadline:
                        DateTime.UtcNow.AddSeconds(
                            10));
            ValidateSuccessfulCommand(
                restorationResult,
                "compact endpoint restoration");

            GrpcV1.PropertyOperationResult restoredReadResult =
                await deployment.Client.Client
                    .ReadAuthoritativePropertyAsync(
                        arduinoRequest,
                        deadline:
                            DateTime.UtcNow.AddSeconds(
                                10));
            ValidateSuccessfulRead(
                restoredReadResult,
                GrpcV1.RemoteValue.KindOneofCase.BooleanValue,
                "restored compact endpoint");

            if (GetBooleanValue(
                    restoredReadResult,
                    "restored compact endpoint state")
                != originalArduinoState)
            {
                throw new InvalidDataException(
                    "The authoritative Property read did not confirm "
                    + "restoration of the original compact endpoint state.");
            }
        }

        Console.WriteLine(
            "Press and release the Arduino Uno validation pushbutton once.");
        Console.WriteLine();

        GrpcV1.RuntimeHostObservation eventObservation =
            await ReadMatchingObservationAsync(
                observationCall,
                arduinoRequest.Target,
                IsCompactButtonEventObservation,
                lastObservationSequence,
                observationCancellation.Token);
        lastObservationSequence =
            eventObservation.Sequence;

        observationCancellation.Cancel();

        Console.WriteLine(
            "Private-network runtime-host client");
        Console.WriteLine(
            "===================================");
        Console.WriteLine();
        Console.WriteLine(
            "Transport             : HTTPS / HTTP/2 gRPC");
        Console.WriteLine(
            "Client authentication : Mutual TLS");
        Console.WriteLine(
            "Remote configuration  : External and withheld");
        Console.WriteLine(
            $"Runtime host          : {response.RuntimeHostId}");
        Console.WriteLine(
            $"API version           : {response.ApiVersion.Major}."
            + response.ApiVersion.Minor);
        Console.WriteLine(
            $"Published endpoints   : {response.Endpoints.Count}");

        foreach (GrpcV1.PublishedRuntimeEndpointSnapshot endpoint
            in response.Endpoints)
        {
            Console.WriteLine(
                $"  {endpoint.EndpointId}");
        }

        Console.WriteLine();
        Console.WriteLine(
            "Authenticated snapshot completed.");
        Console.WriteLine(
            "Native authoritative read completed.");
        Console.WriteLine(
            "Compact authoritative read completed.");
        Console.WriteLine(
            "Compact Command completed and confirmed.");
        Console.WriteLine(
            "Compact state restoration completed and confirmed.");
        Console.WriteLine(
            "Authenticated Property observation completed.");
        Console.WriteLine(
            "Authenticated physical Event observation completed.");
        Console.WriteLine(
            $"Final observation sequence: {lastObservationSequence}");
    }

    internal static GrpcV1.ReadAuthoritativePropertyRequest
        CreatePropertyReadRequest(
            GrpcV1.GetSnapshotResponse snapshot,
            string instrumentId,
            string propertyId)
    {
        ArgumentNullException.ThrowIfNull(
            snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            instrumentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            propertyId);

        GrpcV1.PublishedRuntimeEndpointSnapshot endpoint =
            snapshot.Endpoints.SingleOrDefault(
                candidate =>
                    candidate.Descriptor_ is not null
                    && candidate.Descriptor_.Instruments.Any(
                        instrument =>
                            string.Equals(
                                instrument.InstrumentId,
                                instrumentId,
                                StringComparison.Ordinal)
                            && instrument.Properties.Any(
                                property =>
                                    string.Equals(
                                        property.PropertyId,
                                        propertyId,
                                        StringComparison.Ordinal))))
            ?? throw new InvalidDataException(
                "The authenticated snapshot does not contain the required "
                + "physical endpoint.");

        return new GrpcV1.ReadAuthoritativePropertyRequest
        {
            Target =
                new GrpcV1.PropertyTarget
                {
                    EndpointId =
                        endpoint.EndpointId,
                    AttachmentGeneration =
                        endpoint.AttachmentGeneration,
                    InstrumentId =
                        instrumentId,
                    PropertyId =
                        propertyId
                }
        };
    }

    internal static bool IsCompactPropertyObservation(
        GrpcV1.RuntimeHostObservation observation)
    {
        ArgumentNullException.ThrowIfNull(
            observation);

        return observation.Kind
                == GrpcV1.RuntimeHostObservationKind.PropertyValueChanged
            && observation.PropertyValueChanged is not null
            && string.Equals(
                observation.PropertyValueChanged.InstrumentId,
                PhysicalArduinoUnoCompactDescriptorFactory
                    .ControllerInstrumentId
                    .Value,
                StringComparison.Ordinal)
            && string.Equals(
                observation.PropertyValueChanged.PropertyId,
                PhysicalArduinoUnoCompactDescriptorFactory
                    .BuiltInLedStatePropertyId
                    .Value,
                StringComparison.Ordinal);
    }

    internal static bool IsCompactButtonEventObservation(
        GrpcV1.RuntimeHostObservation observation)
    {
        ArgumentNullException.ThrowIfNull(
            observation);

        return observation.Kind
                == GrpcV1.RuntimeHostObservationKind.EventOccurred
            && observation.EventOccurred is not null
            && string.Equals(
                observation.EventOccurred.InstrumentId,
                PhysicalArduinoUnoCompactDescriptorFactory
                    .ControllerInstrumentId
                    .Value,
                StringComparison.Ordinal)
            && observation.EventOccurred.EventPathSegments.SequenceEqual(
                PhysicalArduinoUnoCompactDescriptorFactory
                    .ButtonPressedEventPath
                    .Segments);
    }

    private static async Task<GrpcV1.RuntimeHostObservation>
        ReadMatchingObservationAsync(
            AsyncServerStreamingCall<GrpcV1.ObserveResponse> call,
            GrpcV1.PropertyTarget target,
            Func<GrpcV1.RuntimeHostObservation, bool> matchesPayload,
            ulong lastSequence,
            CancellationToken cancellationToken)
    {
        while (true)
        {
            GrpcV1.ObserveResponse response =
                await ReadNextObservationResponseAsync(
                    call,
                    cancellationToken);

            if (response.ContentCase
                != GrpcV1.ObserveResponse.ContentOneofCase.Observation)
            {
                throw new InvalidDataException(
                    "The observation stream published an unexpected second "
                    + "initial snapshot.");
            }

            GrpcV1.RuntimeHostObservation observation =
                response.Observation;

            if (observation.Sequence <= lastSequence)
            {
                throw new InvalidDataException(
                    "The observation sequence is not strictly increasing.");
            }

            lastSequence =
                observation.Sequence;

            if (string.Equals(
                    observation.EndpointId,
                    target.EndpointId,
                    StringComparison.Ordinal)
                && string.Equals(
                    observation.AttachmentGeneration,
                    target.AttachmentGeneration,
                    StringComparison.Ordinal)
                && matchesPayload(
                    observation))
            {
                return observation;
            }
        }
    }

    private static async Task<GrpcV1.ObserveResponse>
        ReadNextObservationResponseAsync(
            AsyncServerStreamingCall<GrpcV1.ObserveResponse> call,
            CancellationToken cancellationToken)
    {
        if (!await call.ResponseStream.MoveNext(
                cancellationToken))
        {
            throw new InvalidDataException(
                "The authenticated observation stream ended unexpectedly.");
        }

        return call.ResponseStream.Current;
    }

    internal static GrpcV1.ExecuteCommandRequest CreateCommandRequest(
        GrpcV1.ReadAuthoritativePropertyRequest propertyRequest,
        IEnumerable<string> commandPathSegments)
    {
        ArgumentNullException.ThrowIfNull(
            propertyRequest);
        ArgumentNullException.ThrowIfNull(
            commandPathSegments);

        if (propertyRequest.Target is null)
        {
            throw new ArgumentException(
                "The Property request must contain a target.",
                nameof(propertyRequest));
        }

        string[] segments =
            commandPathSegments.ToArray();

        if (segments.Length == 0
            || segments.Any(
                string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "The Command path must contain only non-empty segments.",
                nameof(commandPathSegments));
        }

        var target =
            new GrpcV1.CommandTarget
            {
                EndpointId =
                    propertyRequest.Target.EndpointId,
                AttachmentGeneration =
                    propertyRequest.Target.AttachmentGeneration,
                InstrumentId =
                    propertyRequest.Target.InstrumentId
            };
        target.CommandPathSegments.AddRange(
            segments);

        return new GrpcV1.ExecuteCommandRequest
        {
            Target =
                target
        };
    }

    private static void ValidateSuccessfulRead(
        GrpcV1.PropertyOperationResult result,
        GrpcV1.RemoteValue.KindOneofCase expectedKind,
        string endpointFamily)
    {
        if (result.Status
                != GrpcV1.PropertyOperationStatus.Success
            || result.ConfirmedValue?.Value is null
            || result.ConfirmedValue.Value.KindCase
                != expectedKind)
        {
            throw new InvalidDataException(
                $"The authenticated {endpointFamily} authoritative "
                + "Property read did not return the expected value.");
        }
    }

    private static bool GetBooleanValue(
        GrpcV1.PropertyOperationResult result,
        string operationName)
    {
        if (result.ConfirmedValue?.Value?.KindCase
            != GrpcV1.RemoteValue.KindOneofCase.BooleanValue)
        {
            throw new InvalidDataException(
                $"The {operationName} did not return a Boolean value.");
        }

        return result.ConfirmedValue.Value.BooleanValue;
    }

    private static void ValidateSuccessfulCommand(
        GrpcV1.CommandOperationResult result,
        string operationName)
    {
        if (result.Status
            != GrpcV1.CommandOperationStatus.Success)
        {
            throw new InvalidDataException(
                $"The authenticated {operationName} failed.");
        }
    }
}
