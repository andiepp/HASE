using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Mcnf;
using Hase.Mcnf.RfLab.Runtime;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;

namespace Hase.Mcnf.RfLab.Hosting.Tests;

public sealed class RfLabEndpointAttachmentOperationsTests
{
    private static readonly InstrumentId Instrument = new("rf-minilab-01");
    private static readonly PropertyId Property = new("sensor-voltage");

    private static RuntimeEndpoint CreateEndpoint() =>
        new RuntimeContext().CreateEndpoint(
            RfLabReadOnlyDefinition.EndpointDefinition.Materialize(
                new EndpointId("rflab-test-01")));

    private static RuntimeProperty SomeProperty(RuntimeEndpoint endpoint)
    {
        RuntimeProperty property = endpoint.Instruments.Single().Properties[0];
        property.UpdateValue(new PropertyValue(
            1.0,
            DateTimeOffset.UnixEpoch,
            PropertyQuality.Good));
        return property;
    }

    private static RfLabEndpointAttachmentCommandOperations CreateCommandOperations(
        Exception failure,
        RuntimeEndpoint endpoint,
        bool sessionFaulted = true) =>
        new(
            (_, _, _, _) => Task.FromException<RuntimeCommand>(failure),
            () => sessionFaulted,
            endpoint,
            TimeProvider.System);

    [Fact]
    public async Task CommandExecute_MapsDeviceRejectionsWithoutFaultingTheEndpoint()
    {
        RuntimeEndpoint endpoint = CreateEndpoint();
        RfLabEndpointAttachmentCommandOperations operations = CreateCommandOperations(
            new McnfDeviceErrorException(
                RfLabDeviceErrorCode.Si5351Disconnected,
                "The RF-Lab node rejected the request: Si5351Disconnected."),
            endpoint,
            sessionFaulted: false);

        EndpointAttachmentCommandOperationResult result = await operations.ExecuteAsync(
            Instrument,
            RfLabCommandMapping.ApplyClock0.CommandPath,
            argument: null);

        Assert.False(result.IsSuccess);
        Assert.Equal(EndpointAttachmentCommandOperationStatus.Rejected, result.Status);
        Assert.Contains("Si5351Disconnected", result.Diagnostic, StringComparison.Ordinal);
        Assert.NotEqual(
            EndpointConnectionState.Faulted,
            endpoint.ConnectionStatus.State);
    }

    [Fact]
    public async Task CommandExecute_MapsUncertainOutcomesToUnavailableAndFaults()
    {
        RuntimeEndpoint endpoint = CreateEndpoint();
        RfLabEndpointAttachmentCommandOperations operations = CreateCommandOperations(
            new RfLabMutationOutcomeUncertainException(
                "The RF-Lab carrier outcome is uncertain because no acknowledged response was established.",
                new TimeoutException()),
            endpoint);

        EndpointAttachmentCommandOperationResult result = await operations.ExecuteAsync(
            Instrument,
            RfLabCommandMapping.ApplyCarrier.CommandPath,
            argument: null);

        Assert.Equal(EndpointAttachmentCommandOperationStatus.Unavailable, result.Status);
        Assert.Contains("uncertain", result.Diagnostic, StringComparison.Ordinal);
        Assert.Equal(EndpointConnectionState.Faulted, endpoint.ConnectionStatus.State);
    }

    [Fact]
    public async Task CommandExecute_MapsUncertainExchangeFailuresToUnavailable()
    {
        RuntimeEndpoint endpoint = CreateEndpoint();
        RfLabEndpointAttachmentCommandOperations operations = CreateCommandOperations(
            new McnfExchangeException(
                "The MCNF exchange outcome is uncertain because it failed after transmission began.",
                executionMayHaveOccurred: true,
                new IOException()),
            endpoint);

        EndpointAttachmentCommandOperationResult result = await operations.ExecuteAsync(
            Instrument,
            RfLabCommandMapping.ApplyCarrier.CommandPath,
            argument: null);

        Assert.Equal(EndpointAttachmentCommandOperationStatus.Unavailable, result.Status);
        Assert.Equal(EndpointConnectionState.Faulted, endpoint.ConnectionStatus.State);
    }

    [Theory]
    [InlineData(typeof(KeyNotFoundException), EndpointAttachmentCommandOperationStatus.Failure)]
    [InlineData(typeof(ArgumentException), EndpointAttachmentCommandOperationStatus.ArgumentNotSupported)]
    [InlineData(typeof(TimeoutException), EndpointAttachmentCommandOperationStatus.TimedOut)]
    [InlineData(typeof(InvalidDataException), EndpointAttachmentCommandOperationStatus.Failure)]
    [InlineData(typeof(IOException), EndpointAttachmentCommandOperationStatus.Unavailable)]
    public async Task CommandExecute_MapsFailuresToTheEstablishedStatuses(
        Type exceptionType,
        EndpointAttachmentCommandOperationStatus expectedStatus)
    {
        RuntimeEndpoint endpoint = CreateEndpoint();
        RfLabEndpointAttachmentCommandOperations operations = CreateCommandOperations(
            (Exception)Activator.CreateInstance(exceptionType)!,
            endpoint);

        EndpointAttachmentCommandOperationResult result = await operations.ExecuteAsync(
            Instrument,
            RfLabCommandMapping.ApplyCarrier.CommandPath,
            argument: null);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedStatus, result.Status);
    }

    [Fact]
    public async Task CommandExecute_MapsHealthySessionRejectionsToRejected()
    {
        RuntimeEndpoint endpoint = CreateEndpoint();
        RfLabEndpointAttachmentCommandOperations operations = CreateCommandOperations(
            new InvalidOperationException("rejected"),
            endpoint,
            sessionFaulted: false);

        EndpointAttachmentCommandOperationResult result = await operations.ExecuteAsync(
            Instrument,
            RfLabCommandMapping.ApplyCarrier.CommandPath,
            argument: null);

        Assert.Equal(EndpointAttachmentCommandOperationStatus.Rejected, result.Status);
        Assert.NotEqual(
            EndpointConnectionState.Faulted,
            endpoint.ConnectionStatus.State);
    }

    [Fact]
    public async Task PropertyRead_MapsDeviceRejectionsToFailureWithoutFaulting()
    {
        RuntimeEndpoint endpoint = CreateEndpoint();
        var operations = new RfLabEndpointAttachmentPropertyOperations(
            (_, _, _) => Task.FromException<RuntimeProperty>(
                new McnfDeviceErrorException(3, "rejected")),
            (_, _, _, _) => Task.FromException<RuntimeProperty>(
                new McnfDeviceErrorException(3, "rejected")),
            () => false,
            endpoint,
            TimeProvider.System);

        EndpointAttachmentPropertyOperationResult result = await operations.ReadAsync(
            Instrument,
            Property);

        Assert.Equal(EndpointAttachmentPropertyOperationStatus.Failure, result.Status);
        Assert.NotEqual(
            EndpointConnectionState.Faulted,
            endpoint.ConnectionStatus.State);
    }

    [Fact]
    public async Task PropertyRead_MapsFaultedSessionFailuresAndProjectsTheFault()
    {
        RuntimeEndpoint endpoint = CreateEndpoint();
        var operations = new RfLabEndpointAttachmentPropertyOperations(
            (_, _, _) => Task.FromException<RuntimeProperty>(new TimeoutException()),
            (_, _, _, _) => Task.FromException<RuntimeProperty>(new TimeoutException()),
            () => true,
            endpoint,
            TimeProvider.System);

        EndpointAttachmentPropertyOperationResult result = await operations.ReadAsync(
            Instrument,
            Property);

        Assert.Equal(EndpointAttachmentPropertyOperationStatus.TimedOut, result.Status);
        Assert.Equal(EndpointConnectionState.Faulted, endpoint.ConnectionStatus.State);
    }

    [Fact]
    public async Task PropertyOperations_ReturnTheConfirmedValueOnSuccess()
    {
        RuntimeEndpoint endpoint = CreateEndpoint();
        RuntimeProperty property = SomeProperty(endpoint);
        var operations = new RfLabEndpointAttachmentPropertyOperations(
            (_, _, _) => Task.FromResult(property),
            (_, _, _, _) => Task.FromResult(property),
            () => false,
            endpoint,
            TimeProvider.System);

        EndpointAttachmentPropertyOperationResult readResult =
            await operations.ReadAsync(Instrument, Property);
        EndpointAttachmentPropertyOperationResult writeResult =
            await operations.WriteAsync(Instrument, Property, 1.0);

        Assert.True(readResult.IsSuccess);
        Assert.True(writeResult.IsSuccess);
        Assert.Equal(1.0, readResult.ConfirmedValue!.Value);
    }

    [Fact]
    public async Task PropertyWrite_MapsRangeRejectionsToFailure()
    {
        RuntimeEndpoint endpoint = CreateEndpoint();
        var operations = new RfLabEndpointAttachmentPropertyOperations(
            (_, _, _) => Task.FromException<RuntimeProperty>(new KeyNotFoundException()),
            (_, _, _, _) => Task.FromException<RuntimeProperty>(
                new ArgumentOutOfRangeException()),
            () => false,
            endpoint,
            TimeProvider.System);

        EndpointAttachmentPropertyOperationResult readResult =
            await operations.ReadAsync(Instrument, Property);
        EndpointAttachmentPropertyOperationResult writeResult =
            await operations.WriteAsync(Instrument, Property, -1.0);

        Assert.Equal(EndpointAttachmentPropertyOperationStatus.NotSupported, readResult.Status);
        Assert.Equal(EndpointAttachmentPropertyOperationStatus.Failure, writeResult.Status);
    }
}
