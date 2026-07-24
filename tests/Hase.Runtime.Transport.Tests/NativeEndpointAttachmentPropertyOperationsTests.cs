using Hase.Core.Domain.Data;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Protocol;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class NativeEndpointAttachmentPropertyOperationsTests
{
    private static readonly InstrumentId InstrumentId =
        new(
            "native-instrument");

    private static readonly PropertyId PropertyId =
        new(
            "native.value");

    [Fact]
    public void Constructor_InvalidTimeout_Throws()
    {
        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                ready: true);

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new NativeEndpointAttachmentPropertyOperations(
                    runtimeEndpoint,
                    TimeSpan.Zero,
                    static (
                        request,
                        timeout,
                        cancellationToken) =>
                            Task.FromResult(
                                request)));
    }

    [Fact]
    public async Task ReadAsync_Success_UsesLogicalTargetAndUpdatesCache()
    {
        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                ready: true);

        PropertyValue confirmedValue =
            CreatePropertyValue(
                42.5);

        ProtocolMessage? capturedRequest =
            null;

        TimeSpan? capturedTimeout =
            null;

        var operations =
            new NativeEndpointAttachmentPropertyOperations(
                runtimeEndpoint,
                TimeSpan.FromSeconds(
                    3),
                (
                    request,
                    timeout,
                    cancellationToken) =>
                {
                    capturedRequest =
                        request;

                    capturedTimeout =
                        timeout;

                    ReadPropertyRequest readRequest =
                        Assert.IsType<ReadPropertyRequest>(
                            request);

                    return Task.FromResult<ProtocolMessage>(
                        new ReadPropertyResponse(
                            readRequest.CorrelationId,
                            ProtocolResult.Success,
                            confirmedValue));
                });

        EndpointAttachmentPropertyOperationResult result =
            await operations.ReadAsync(
                InstrumentId,
                PropertyId);

        ReadPropertyRequest request =
            Assert.IsType<ReadPropertyRequest>(
                capturedRequest);

        Assert.False(
            request.CorrelationId.IsNone);

        Assert.Equal(
            InstrumentId,
            request.InstrumentId);

        Assert.Equal(
            PropertyId,
            request.PropertyId);

        Assert.Equal(
            TimeSpan.FromSeconds(
                3),
            capturedTimeout);

        Assert.True(
            result.IsSuccess);

        Assert.Same(
            confirmedValue,
            result.ConfirmedValue);

        Assert.Same(
            confirmedValue,
            GetRuntimeProperty(
                runtimeEndpoint)
                .CurrentValue);
    }

    [Fact]
    public async Task WriteAsync_Success_UsesRequestedValueAndUpdatesCache()
    {
        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                ready: true);

        PropertyValue confirmedValue =
            CreatePropertyValue(
                17.25);

        WritePropertyRequest? capturedRequest =
            null;

        var operations =
            new NativeEndpointAttachmentPropertyOperations(
                runtimeEndpoint,
                Timeout.InfiniteTimeSpan,
                (
                    request,
                    timeout,
                    cancellationToken) =>
                {
                    capturedRequest =
                        Assert.IsType<WritePropertyRequest>(
                            request);

                    return Task.FromResult<ProtocolMessage>(
                        new WritePropertyResponse(
                            capturedRequest.CorrelationId,
                            ProtocolResult.Success,
                            confirmedValue));
                });

        EndpointAttachmentPropertyOperationResult result =
            await operations.WriteAsync(
                InstrumentId,
                PropertyId,
                17.0);

        Assert.NotNull(
            capturedRequest);

        Assert.Equal(
            InstrumentId,
            capturedRequest.InstrumentId);

        Assert.Equal(
            PropertyId,
            capturedRequest.PropertyId);

        Assert.Equal(
            17.0,
            capturedRequest.Value);

        Assert.True(
            result.IsSuccess);

        Assert.Same(
            confirmedValue,
            GetRuntimeProperty(
                runtimeEndpoint)
                .CurrentValue);
    }

    [Theory]
    [InlineData(
        ProtocolResultCode.InvalidRequest,
        EndpointAttachmentPropertyOperationStatus.Failure)]
    [InlineData(
        ProtocolResultCode.NotFound,
        EndpointAttachmentPropertyOperationStatus.Failure)]
    [InlineData(
        ProtocolResultCode.NotSupported,
        EndpointAttachmentPropertyOperationStatus.NotSupported)]
    [InlineData(
        ProtocolResultCode.Rejected,
        EndpointAttachmentPropertyOperationStatus.Rejected)]
    [InlineData(
        ProtocolResultCode.InternalError,
        EndpointAttachmentPropertyOperationStatus.Failure)]
    public async Task ReadAsync_ProtocolFailure_MapsStatus(
        ProtocolResultCode protocolStatus,
        EndpointAttachmentPropertyOperationStatus expectedStatus)
    {
        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                ready: true);

        var operations =
            CreateOperations(
                runtimeEndpoint,
                request =>
                {
                    ReadPropertyRequest readRequest =
                        Assert.IsType<ReadPropertyRequest>(
                            request);

                    return new ReadPropertyResponse(
                        readRequest.CorrelationId,
                        new ProtocolResult(
                            protocolStatus,
                            "endpoint detail"),
                        PropertyValue:
                            null);
                });

        EndpointAttachmentPropertyOperationResult result =
            await operations.ReadAsync(
                InstrumentId,
                PropertyId);

        Assert.Equal(
            expectedStatus,
            result.Status);

        Assert.Null(
            GetRuntimeProperty(
                runtimeEndpoint)
                .CurrentValue);
    }

    [Fact]
    public async Task WriteAsync_InvalidRequest_MapsInvalidValue()
    {
        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                ready: true);

        var operations =
            CreateOperations(
                runtimeEndpoint,
                request =>
                {
                    WritePropertyRequest writeRequest =
                        Assert.IsType<WritePropertyRequest>(
                            request);

                    return new WritePropertyResponse(
                        writeRequest.CorrelationId,
                        ProtocolResult.InvalidRequest,
                        PropertyValue:
                            null);
                });

        EndpointAttachmentPropertyOperationResult result =
            await operations.WriteAsync(
                InstrumentId,
                PropertyId,
                99.0);

        Assert.Equal(
            EndpointAttachmentPropertyOperationStatus.InvalidValue,
            result.Status);
    }

    [Fact]
    public async Task ReadAsync_MismatchedCorrelation_FailsWithoutCacheUpdate()
    {
        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                ready: true);

        var operations =
            CreateOperations(
                runtimeEndpoint,
                request =>
                    new ReadPropertyResponse(
                        new CorrelationId(
                            uint.MaxValue),
                        ProtocolResult.Success,
                        CreatePropertyValue(
                            1.0)));

        EndpointAttachmentPropertyOperationResult result =
            await operations.ReadAsync(
                InstrumentId,
                PropertyId);

        Assert.Equal(
            EndpointAttachmentPropertyOperationStatus.Failure,
            result.Status);

        Assert.Null(
            GetRuntimeProperty(
                runtimeEndpoint)
                .CurrentValue);
    }

    [Fact]
    public async Task WriteAsync_SuccessWithoutConfirmedValue_FailsWithoutCacheUpdate()
    {
        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                ready: true);

        var operations =
            CreateOperations(
                runtimeEndpoint,
                request =>
                {
                    WritePropertyRequest writeRequest =
                        Assert.IsType<WritePropertyRequest>(
                            request);

                    return new WritePropertyResponse(
                        writeRequest.CorrelationId,
                        ProtocolResult.Success,
                        PropertyValue:
                            null);
                });

        EndpointAttachmentPropertyOperationResult result =
            await operations.WriteAsync(
                InstrumentId,
                PropertyId,
                12.0);

        Assert.Equal(
            EndpointAttachmentPropertyOperationStatus.Failure,
            result.Status);

        Assert.Null(
            GetRuntimeProperty(
                runtimeEndpoint)
                .CurrentValue);
    }

    [Fact]
    public async Task ReadAsync_EndpointNotReady_ReturnsUnavailableWithoutExchange()
    {
        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                ready: false);

        int exchangeCount =
            0;

        var operations =
            new NativeEndpointAttachmentPropertyOperations(
                runtimeEndpoint,
                TimeSpan.FromSeconds(
                    1),
                (
                    request,
                    timeout,
                    cancellationToken) =>
                {
                    exchangeCount++;

                    return Task.FromResult(
                        request);
                });

        EndpointAttachmentPropertyOperationResult result =
            await operations.ReadAsync(
                InstrumentId,
                PropertyId);

        Assert.Equal(
            EndpointAttachmentPropertyOperationStatus.Unavailable,
            result.Status);

        Assert.Equal(
            0,
            exchangeCount);
    }

    [Fact]
    public async Task ReadAsync_ExchangeTimeout_ReturnsTimedOut()
    {
        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                ready: true);

        var operations =
            new NativeEndpointAttachmentPropertyOperations(
                runtimeEndpoint,
                TimeSpan.FromMilliseconds(
                    50),
                static (
                    request,
                    timeout,
                    cancellationToken) =>
                        throw new TimeoutException());

        EndpointAttachmentPropertyOperationResult result =
            await operations.ReadAsync(
                InstrumentId,
                PropertyId);

        Assert.Equal(
            EndpointAttachmentPropertyOperationStatus.TimedOut,
            result.Status);
    }

    [Fact]
    public async Task ReadAsync_CallerCancellation_Throws()
    {
        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                ready: true);

        var operations =
            new NativeEndpointAttachmentPropertyOperations(
                runtimeEndpoint,
                Timeout.InfiniteTimeSpan,
                static async (
                    request,
                    timeout,
                    cancellationToken) =>
                {
                    await Task.Delay(
                        Timeout.InfiniteTimeSpan,
                        cancellationToken);

                    return request;
                });

        using var cancellationSource =
            new CancellationTokenSource();

        cancellationSource.Cancel();

        Task Act()
        {
            return operations.ReadAsync(
                InstrumentId,
                PropertyId,
                cancellationSource.Token);
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            Act);
    }

    private static NativeEndpointAttachmentPropertyOperations
        CreateOperations(
            RuntimeEndpoint runtimeEndpoint,
            Func<ProtocolMessage, ProtocolMessage> respond)
    {
        return new NativeEndpointAttachmentPropertyOperations(
            runtimeEndpoint,
            TimeSpan.FromSeconds(
                1),
            (
                request,
                timeout,
                cancellationToken) =>
                    Task.FromResult(
                        respond(
                            request)));
    }

    private static RuntimeEndpoint CreateRuntimeEndpoint(
        bool ready)
    {
        var property =
            new PropertyDescriptor(
                PropertyId,
                new DescriptorPath(
                    "Native",
                    "Value"),
                "Native Value",
                new NumericDataDescriptor(
                    Quantities.Temperature,
                    Units.Celsius))
            {
                AccessMode =
                    PropertyAccessMode.ReadWrite
            };

        var instrument =
            new InstrumentDescriptor(
                InstrumentId,
                "Native Instrument",
                new InstrumentKind(
                    "native-instrument"))
            {
                Interface =
                    new InstrumentInterface(
                        properties:
                        [
                            property
                        ])
            };

        var descriptor =
            new EndpointDescriptor(
                new EndpointId(
                    "native-property-endpoint"),
                [
                    instrument
                ])
            {
                Metadata =
                    new EndpointMetadata
                    {
                        DisplayName =
                            "Native Property Endpoint",
                        Description =
                            "Endpoint used to test native Property operations."
                    }
            };

        var context =
            new RuntimeContext();

        RuntimeEndpoint runtimeEndpoint =
            context.AddEndpoint(
                descriptor);

        if (ready)
        {
            runtimeEndpoint.UpdateConnectionStatus(
                new EndpointConnectionStatus(
                    EndpointConnectionState.Ready));
        }

        return runtimeEndpoint;
    }

    private static RuntimeProperty GetRuntimeProperty(
        RuntimeEndpoint runtimeEndpoint)
    {
        return runtimeEndpoint.FindInstrument(
                InstrumentId)
            ?.FindProperty(
                PropertyId)
            ?? throw new InvalidOperationException(
                "The test runtime Property was not found.");
    }

    private static PropertyValue CreatePropertyValue(
        double value)
    {
        return new PropertyValue(
            value,
            new DateTimeOffset(
                2026,
                7,
                24,
                7,
                0,
                0,
                TimeSpan.Zero));
    }
}