using Hase.ProtocolExplorer.Scenarios;
using Xunit;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.ProtocolExplorer.Tests;

public sealed class PrivateNetworkScenarioTests
{
    [Fact]
    public void IsCompactPropertyObservation_MatchingPayload_ShouldReturnTrue()
    {
        var observation =
            new GrpcV1.RuntimeHostObservation
            {
                Kind =
                    GrpcV1.RuntimeHostObservationKind.PropertyValueChanged,
                PropertyValueChanged =
                    new GrpcV1.PropertyValueChangedObservation
                    {
                        InstrumentId =
                            "arduino-uno-controller-01",
                        PropertyId =
                            "built-in-led-state"
                    }
            };

        Assert.True(
            PrivateNetworkClientScenario.IsCompactPropertyObservation(
                observation));
    }

    [Fact]
    public void IsCompactPropertyObservation_WrongProperty_ShouldReturnFalse()
    {
        var observation =
            new GrpcV1.RuntimeHostObservation
            {
                Kind =
                    GrpcV1.RuntimeHostObservationKind.PropertyValueChanged,
                PropertyValueChanged =
                    new GrpcV1.PropertyValueChangedObservation
                    {
                        InstrumentId =
                            "arduino-uno-controller-01",
                        PropertyId =
                            "other-property"
                    }
            };

        Assert.False(
            PrivateNetworkClientScenario.IsCompactPropertyObservation(
                observation));
    }

    [Fact]
    public void IsCompactButtonEventObservation_MatchingPayload_ShouldReturnTrue()
    {
        var payload =
            new GrpcV1.EventOccurredObservation
            {
                InstrumentId =
                    "arduino-uno-controller-01"
            };
        payload.EventPathSegments.AddRange(
            [
                "Controller",
                "ButtonPressed"
            ]);
        var observation =
            new GrpcV1.RuntimeHostObservation
            {
                Kind =
                    GrpcV1.RuntimeHostObservationKind.EventOccurred,
                EventOccurred =
                    payload
            };

        Assert.True(
            PrivateNetworkClientScenario.IsCompactButtonEventObservation(
                observation));
    }

    [Fact]
    public void IsCompactButtonEventObservation_WrongPath_ShouldReturnFalse()
    {
        var payload =
            new GrpcV1.EventOccurredObservation
            {
                InstrumentId =
                    "arduino-uno-controller-01"
            };
        payload.EventPathSegments.Add(
            "OtherEvent");
        var observation =
            new GrpcV1.RuntimeHostObservation
            {
                Kind =
                    GrpcV1.RuntimeHostObservationKind.EventOccurred,
                EventOccurred =
                    payload
            };

        Assert.False(
            PrivateNetworkClientScenario.IsCompactButtonEventObservation(
                observation));
    }

    [Fact]
    public void CreateCommandRequest_ValidTarget_ShouldPreserveAttachmentIdentity()
    {
        var propertyRequest =
            new GrpcV1.ReadAuthoritativePropertyRequest
            {
                Target =
                    new GrpcV1.PropertyTarget
                    {
                        EndpointId =
                            "endpoint-01",
                        AttachmentGeneration =
                            "9b531c0c-54f1-4fe6-87b4-48f917f60f4c",
                        InstrumentId =
                            "instrument-01",
                        PropertyId =
                            "property-01"
                    }
            };

        GrpcV1.ExecuteCommandRequest commandRequest =
            PrivateNetworkClientScenario.CreateCommandRequest(
                propertyRequest,
                [
                    "Led",
                    "Toggle"
                ]);

        Assert.Equal(
            "endpoint-01",
            commandRequest.Target.EndpointId);
        Assert.Equal(
            "9b531c0c-54f1-4fe6-87b4-48f917f60f4c",
            commandRequest.Target.AttachmentGeneration);
        Assert.Equal(
            "instrument-01",
            commandRequest.Target.InstrumentId);
        Assert.Equal(
            [
                "Led",
                "Toggle"
            ],
            commandRequest.Target.CommandPathSegments);
        Assert.Null(
            commandRequest.Argument);
    }

    [Theory]
    [InlineData()]
    [InlineData("")]
    [InlineData(" ")]
    public void CreateCommandRequest_InvalidPath_ShouldThrow(
        params string[] commandPathSegments)
    {
        var propertyRequest =
            new GrpcV1.ReadAuthoritativePropertyRequest
            {
                Target =
                    new GrpcV1.PropertyTarget()
            };

        Assert.Throws<ArgumentException>(
            "commandPathSegments",
            () =>
                PrivateNetworkClientScenario.CreateCommandRequest(
                    propertyRequest,
                    commandPathSegments));
    }

    [Fact]
    public void CreatePropertyReadRequest_KnownEndpoint_ShouldMapTarget()
    {
        var snapshot =
            new GrpcV1.GetSnapshotResponse();
        snapshot.Endpoints.Add(
            CreateEndpointSnapshot(
                "endpoint-01",
                "instrument-01",
                "property-01"));

        GrpcV1.ReadAuthoritativePropertyRequest request =
            PrivateNetworkClientScenario.CreatePropertyReadRequest(
                snapshot,
                "instrument-01",
                "property-01");

        Assert.Equal(
            "endpoint-01",
            request.Target.EndpointId);
        Assert.Equal(
            "9b531c0c-54f1-4fe6-87b4-48f917f60f4c",
            request.Target.AttachmentGeneration);
        Assert.Equal(
            "instrument-01",
            request.Target.InstrumentId);
        Assert.Equal(
            "property-01",
            request.Target.PropertyId);
    }

    [Fact]
    public void CreatePropertyReadRequest_MissingEndpoint_ShouldThrow()
    {
        Assert.Throws<InvalidDataException>(
            () =>
                PrivateNetworkClientScenario.CreatePropertyReadRequest(
                    new GrpcV1.GetSnapshotResponse(),
                    "instrument-01",
                    "property-01"));
    }

    [Fact]
    public void CreatePropertyReadRequest_DuplicateEndpoint_ShouldThrow()
    {
        var snapshot =
            new GrpcV1.GetSnapshotResponse();
        snapshot.Endpoints.Add(
            CreateEndpointSnapshot(
                "endpoint-01",
                "instrument-01",
                "property-01"));
        snapshot.Endpoints.Add(
            CreateEndpointSnapshot(
                "endpoint-02",
                "instrument-01",
                "property-01"));

        Assert.Throws<InvalidOperationException>(
            () =>
                PrivateNetworkClientScenario.CreatePropertyReadRequest(
                    snapshot,
                    "instrument-01",
                    "property-01"));
    }

    private static GrpcV1.PublishedRuntimeEndpointSnapshot
        CreateEndpointSnapshot(
            string endpointId,
            string instrumentId,
            string propertyId)
    {
        var property =
            new GrpcV1.PropertyDescriptor
            {
                PropertyId =
                    propertyId
            };
        var instrument =
            new GrpcV1.InstrumentDescriptor
            {
                InstrumentId =
                    instrumentId
            };
        instrument.Properties.Add(
            property);
        var descriptor =
            new GrpcV1.EndpointDescriptor
            {
                EndpointId =
                    endpointId
            };
        descriptor.Instruments.Add(
            instrument);

        return new GrpcV1.PublishedRuntimeEndpointSnapshot
        {
            EndpointId =
                endpointId,
            AttachmentGeneration =
                "9b531c0c-54f1-4fe6-87b4-48f917f60f4c",
            Descriptor_ =
                descriptor
        };
    }

    [Fact]
    public void HostName_ShouldBePrivateNetworkHost()
    {
        Assert.Equal(
            "private-network-host",
            new PrivateNetworkHostScenario().Name);
    }

    [Fact]
    public void ClientName_ShouldBePrivateNetworkClient()
    {
        Assert.Equal(
            "private-network-client",
            new PrivateNetworkClientScenario().Name);
    }

    [Fact]
    public void Scenarios_ShouldImplementParameterizedScenario()
    {
        Assert.IsAssignableFrom<IParameterizedScenario>(
            new PrivateNetworkHostScenario());
        Assert.IsAssignableFrom<IParameterizedScenario>(
            new PrivateNetworkClientScenario());
    }

    [Fact]
    public void ParseArguments_ValidPath_ShouldPreserveValue()
    {
        string configurationFilePath =
            Path.Combine(
                Path.GetTempPath(),
                "private-network-configuration.json");

        PrivateNetworkHostArguments hostArguments =
            PrivateNetworkHostScenario.ParseArguments(
                [
                    configurationFilePath,
                    "device.example"
                ]);
        PrivateNetworkConfigurationFileArguments clientArguments =
            PrivateNetworkClientScenario.ParseArguments(
                [
                    configurationFilePath
                ]);

        Assert.Equal(
            configurationFilePath,
            hostArguments.Configuration.ConfigurationFilePath);
        Assert.Equal(
            "device.example",
            hostArguments.Esp32Host);
        Assert.Equal(
            configurationFilePath,
            clientArguments.ConfigurationFilePath);
    }

    [Theory]
    [InlineData()]
    [InlineData("one")]
    [InlineData("one", "two", "three")]
    [InlineData("one", " ")]
    public void HostParseArguments_InvalidShape_ShouldThrow(
        params string[] arguments)
    {
        Assert.Throws<ArgumentException>(
            "arguments",
            () =>
                PrivateNetworkHostScenario.ParseArguments(
                    arguments));
    }

    [Theory]
    [InlineData()]
    [InlineData("one", "two")]
    public void ClientParseArguments_InvalidShape_ShouldThrow(
        params string[] arguments)
    {
        Assert.Throws<ArgumentException>(
            "arguments",
            () =>
                PrivateNetworkClientScenario.ParseArguments(
                    arguments));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("private-network-configuration.json")]
    public void ParseArguments_InvalidPath_ShouldThrow(
        string configurationFilePath)
    {
        Assert.ThrowsAny<ArgumentException>(
            () =>
                PrivateNetworkHostScenario.ParseArguments(
                    [
                        configurationFilePath,
                        "device.example"
                    ]));
        Assert.ThrowsAny<ArgumentException>(
            () =>
                PrivateNetworkClientScenario.ParseArguments(
                    [
                        configurationFilePath
                    ]));
    }
}
