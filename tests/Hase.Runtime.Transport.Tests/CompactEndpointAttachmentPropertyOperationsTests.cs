using Hase.CompactProtocol;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class CompactEndpointAttachmentPropertyOperationsTests
{
    private static readonly InstrumentId InstrumentId =
        new(
            "controller-01");

    private static readonly PropertyId PropertyId =
        new(
            "led-state");

    [Fact]
    public async Task ReadAsync_Success_ReturnsAuthoritativeCachedValue()
    {
        TestFixture fixture =
            CreateFixture();

        PropertyValue confirmedValue =
            CreatePropertyValue(
                false);

        byte? capturedCompactPropertyId =
            null;

        var operations =
            CreateOperations(
                fixture,
                (
                    compactPropertyId,
                    cancellationToken) =>
                {
                    capturedCompactPropertyId =
                        compactPropertyId;

                    fixture.RuntimeProperty.UpdateValue(
                        confirmedValue);

                    return Task.FromResult(
                        new CompactRuntimePropertySynchronizationResult(
                            fixture.Mapping,
                            fixture.RuntimeProperty,
                            CompactPropertyReadStatus.Success));
                });

        EndpointAttachmentPropertyOperationResult result =
            await operations.ReadAsync(
                InstrumentId,
                PropertyId);

        Assert.Equal(
            (byte)0x01,
            capturedCompactPropertyId);

        Assert.True(
            result.IsSuccess);

        Assert.Same(
            confirmedValue,
            result.ConfirmedValue);
    }

    [Theory]
    [InlineData((byte)CompactPropertyReadStatus.UnknownProperty)]
    [InlineData((byte)CompactPropertyReadStatus.ReadFailed)]
    public async Task ReadAsync_EndpointFailure_MapsFailure(
        byte statusValue)
    {
        TestFixture fixture =
            CreateFixture();

        var status =
            (CompactPropertyReadStatus)statusValue;

        var operations =
            CreateOperations(
                fixture,
                (
                    compactPropertyId,
                    cancellationToken) =>
                        Task.FromResult(
                            new CompactRuntimePropertySynchronizationResult(
                                fixture.Mapping,
                                fixture.RuntimeProperty,
                                status)));

        EndpointAttachmentPropertyOperationResult result =
            await operations.ReadAsync(
                InstrumentId,
                PropertyId);

        Assert.Equal(
            EndpointAttachmentPropertyOperationStatus.Failure,
            result.Status);

        Assert.Null(
            result.ConfirmedValue);
    }

    [Fact]
    public async Task WriteAsync_Success_ReturnsConfirmationReadValue()
    {
        TestFixture fixture =
            CreateFixture();

        PropertyValue confirmedValue =
            CreatePropertyValue(
                true);

        object? capturedRequestedValue =
            null;

        var operations =
            CreateOperations(
                fixture,
                writeAsync:
                    (
                        compactPropertyId,
                        requestedValue,
                        cancellationToken) =>
                    {
                        capturedRequestedValue =
                            requestedValue;

                        fixture.RuntimeProperty.UpdateValue(
                            confirmedValue);

                        return Task.FromResult(
                            new CompactRuntimePropertyWriteResult(
                                fixture.Mapping,
                                fixture.RuntimeProperty,
                                CompactPropertyWriteStatus.Success,
                                CompactPropertyReadStatus.Success));
                    });

        EndpointAttachmentPropertyOperationResult result =
            await operations.WriteAsync(
                InstrumentId,
                PropertyId,
                true);

        Assert.Equal(
            true,
            capturedRequestedValue);

        Assert.True(
            result.IsSuccess);

        Assert.Same(
            confirmedValue,
            result.ConfirmedValue);
    }

    [Theory]
    [InlineData(
        (byte)CompactPropertyWriteStatus.UnknownProperty,
        EndpointAttachmentPropertyOperationStatus.Failure)]
    [InlineData(
        (byte)CompactPropertyWriteStatus.WriteNotSupported,
        EndpointAttachmentPropertyOperationStatus.NotSupported)]
    [InlineData(
        (byte)CompactPropertyWriteStatus.InvalidValue,
        EndpointAttachmentPropertyOperationStatus.InvalidValue)]
    [InlineData(
        (byte)CompactPropertyWriteStatus.WriteFailed,
        EndpointAttachmentPropertyOperationStatus.Failure)]
    public async Task WriteAsync_EndpointFailure_MapsStatus(
        byte statusValue,
        EndpointAttachmentPropertyOperationStatus expectedStatus)
    {
        TestFixture fixture =
            CreateFixture();

        var status =
            (CompactPropertyWriteStatus)statusValue;

        var operations =
            CreateOperations(
                fixture,
                writeAsync:
                    (
                        compactPropertyId,
                        requestedValue,
                        cancellationToken) =>
                            Task.FromResult(
                                new CompactRuntimePropertyWriteResult(
                                    fixture.Mapping,
                                    fixture.RuntimeProperty,
                                    status,
                                    confirmationReadStatus:
                                        null)));

        EndpointAttachmentPropertyOperationResult result =
            await operations.WriteAsync(
                InstrumentId,
                PropertyId,
                true);

        Assert.Equal(
            expectedStatus,
            result.Status);
    }

    [Theory]
    [InlineData((byte)CompactPropertyReadStatus.UnknownProperty)]
    [InlineData((byte)CompactPropertyReadStatus.ReadFailed)]
    public async Task WriteAsync_ConfirmationFailure_MapsFailure(
        byte confirmationStatusValue)
    {
        TestFixture fixture =
            CreateFixture();

        var confirmationStatus =
            (CompactPropertyReadStatus)confirmationStatusValue;

        var operations =
            CreateOperations(
                fixture,
                writeAsync:
                    (
                        compactPropertyId,
                        requestedValue,
                        cancellationToken) =>
                            Task.FromResult(
                                new CompactRuntimePropertyWriteResult(
                                    fixture.Mapping,
                                    fixture.RuntimeProperty,
                                    CompactPropertyWriteStatus.Success,
                                    confirmationStatus)));

        EndpointAttachmentPropertyOperationResult result =
            await operations.WriteAsync(
                InstrumentId,
                PropertyId,
                true);

        Assert.Equal(
            EndpointAttachmentPropertyOperationStatus.Failure,
            result.Status);
    }

    [Fact]
    public async Task ReadAsync_UnmappedLogicalTarget_ReturnsNotSupportedWithoutRead()
    {
        TestFixture fixture =
            CreateFixture();

        int readCount =
            0;

        var operations =
            CreateOperations(
                fixture,
                (
                    compactPropertyId,
                    cancellationToken) =>
                {
                    readCount++;

                    throw new InvalidOperationException();
                });

        EndpointAttachmentPropertyOperationResult result =
            await operations.ReadAsync(
                InstrumentId,
                new PropertyId(
                    "unmapped-property"));

        Assert.Equal(
            EndpointAttachmentPropertyOperationStatus.NotSupported,
            result.Status);

        Assert.Equal(
            0,
            readCount);
    }

    [Fact]
    public async Task WriteAsync_NullRequestedValue_ReturnsInvalidValueWithoutWrite()
    {
        TestFixture fixture =
            CreateFixture();

        int writeCount =
            0;

        var operations =
            CreateOperations(
                fixture,
                writeAsync:
                    (
                        compactPropertyId,
                        requestedValue,
                        cancellationToken) =>
                    {
                        writeCount++;

                        throw new InvalidOperationException();
                    });

        EndpointAttachmentPropertyOperationResult result =
            await operations.WriteAsync(
                InstrumentId,
                PropertyId,
                requestedValue:
                    null);

        Assert.Equal(
            EndpointAttachmentPropertyOperationStatus.InvalidValue,
            result.Status);

        Assert.Equal(
            0,
            writeCount);
    }

    [Fact]
    public async Task ReadAsync_Timeout_ReturnsTimedOut()
    {
        TestFixture fixture =
            CreateFixture();

        var operations =
            CreateOperations(
                fixture,
                static (
                    compactPropertyId,
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
    public async Task ReadAsync_UnavailableCoordinator_ReturnsUnavailable()
    {
        TestFixture fixture =
            CreateFixture();

        var operations =
            CreateOperations(
                fixture,
                static (
                    compactPropertyId,
                    cancellationToken) =>
                        throw new InvalidOperationException());

        EndpointAttachmentPropertyOperationResult result =
            await operations.ReadAsync(
                InstrumentId,
                PropertyId);

        Assert.Equal(
            EndpointAttachmentPropertyOperationStatus.Unavailable,
            result.Status);
    }

    [Fact]
    public async Task ReadAsync_CallerCancellation_Throws()
    {
        TestFixture fixture =
            CreateFixture();

        var operations =
            CreateOperations(
                fixture);

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

    private static CompactEndpointAttachmentPropertyOperations
        CreateOperations(
            TestFixture fixture,
            Func<
                byte,
                CancellationToken,
                Task<CompactRuntimePropertySynchronizationResult>>?
                readAsync = null,
            Func<
                byte,
                object,
                CancellationToken,
                Task<CompactRuntimePropertyWriteResult>>?
                writeAsync = null)
    {
        readAsync ??=
            (
                compactPropertyId,
                cancellationToken) =>
                    Task.FromResult(
                        new CompactRuntimePropertySynchronizationResult(
                            fixture.Mapping,
                            fixture.RuntimeProperty,
                            CompactPropertyReadStatus.ReadFailed));

        writeAsync ??=
            (
                compactPropertyId,
                requestedValue,
                cancellationToken) =>
                    Task.FromResult(
                        new CompactRuntimePropertyWriteResult(
                            fixture.Mapping,
                            fixture.RuntimeProperty,
                            CompactPropertyWriteStatus.WriteFailed,
                            confirmationReadStatus:
                                null));

        return new CompactEndpointAttachmentPropertyOperations(
            fixture.PropertyMap,
            readAsync,
            writeAsync);
    }

    private static TestFixture CreateFixture()
    {
        var property =
            new PropertyDescriptor(
                PropertyId,
                new DescriptorPath(
                    "Led",
                    "State"),
                "Built-in LED State",
                new BooleanDataDescriptor())
            {
                AccessMode =
                    PropertyAccessMode.ReadWrite
            };

        var instrument =
            new InstrumentDescriptor(
                InstrumentId,
                "Arduino Uno GPIO Controller",
                new InstrumentKind(
                    "controller"))
            {
                Interface =
                    new InstrumentInterface(
                        properties:
                        [
                            property
                        ])
            };

        var definition =
            new EndpointDescriptorDefinition(
                metadata:
                    new EndpointMetadata
                    {
                        DisplayName =
                            "Arduino Uno Compact Endpoint"
                    },
                instruments:
                [
                    instrument
                ]);

        var mapping =
            new CompactPropertyMapping(
                compactPropertyId: 0x01,
                InstrumentId,
                PropertyId,
                CompactPropertyValueEncoding.Boolean);

        var propertyMap =
            new CompactPropertyMap(
                definition,
                [
                    mapping
                ]);

        RuntimeEndpoint runtimeEndpoint =
            new RuntimeContext()
                .AddEndpoint(
                    definition.Materialize(
                        new EndpointId(
                            "arduino-uno-01")));

        RuntimeProperty runtimeProperty =
            runtimeEndpoint.FindInstrument(
                    InstrumentId)
                ?.FindProperty(
                    PropertyId)
                ?? throw new InvalidOperationException(
                    "The test runtime Property was not found.");

        return new TestFixture(
            propertyMap,
            mapping,
            runtimeProperty);
    }

    private static PropertyValue CreatePropertyValue(
        bool value)
    {
        return new PropertyValue(
            value,
            new DateTimeOffset(
                2026,
                7,
                24,
                7,
                30,
                0,
                TimeSpan.Zero));
    }

    private sealed record TestFixture(
        CompactPropertyMap PropertyMap,
        CompactPropertyMapping Mapping,
        RuntimeProperty RuntimeProperty);
}
