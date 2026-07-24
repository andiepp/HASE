using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Connections;
using Hase.Runtime.Northbound;

namespace Hase.Runtime.Tests.Northbound;

public sealed class RuntimeHostObservationContractTests
{
    [Theory]
    [InlineData(RuntimeHostObservationKind.AttachmentPublished)]
    [InlineData(RuntimeHostObservationKind.AttachmentEnded)]
    [InlineData(RuntimeHostObservationKind.ConnectionStatusChanged)]
    [InlineData(RuntimeHostObservationKind.PropertyValueChanged)]
    [InlineData(RuntimeHostObservationKind.EventOccurred)]
    public void Kind_IsDefined(
        RuntimeHostObservationKind kind)
    {
        Assert.True(
            Enum.IsDefined(
                kind));
    }

    [Fact]
    public void Sequence_NegativeValue_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new RuntimeHostObservationSequence(
                -1));
    }

    [Fact]
    public void Sequence_NonNegativeValue_StoresValue()
    {
        var sequence =
            new RuntimeHostObservationSequence(
                42);

        Assert.Equal(
            42,
            sequence.Value);

        Assert.Equal(
            "42",
            sequence.ToString());
    }

    [Fact]
    public void AttachmentPublishedPayload_StoresSnapshotAndKind()
    {
        PublishedRuntimeEndpointSnapshot endpoint =
            CreatePublishedEndpointSnapshot();

        var payload =
            new RuntimeHostAttachmentPublishedObservationPayload(
                endpoint);

        Assert.Equal(
            RuntimeHostObservationKind.AttachmentPublished,
            payload.Kind);

        Assert.Same(
            endpoint,
            payload.Endpoint);
    }

    [Fact]
    public void AttachmentPublishedPayload_NullSnapshot_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeHostAttachmentPublishedObservationPayload(
                null!));
    }

    [Fact]
    public void Observation_StoresGenerationBoundPayloadAndDerivedKind()
    {
        RuntimeHostObservationSequence sequence =
            new(
                7);

        PublishedRuntimeEndpointSnapshot endpoint =
            CreatePublishedEndpointSnapshot();

        RuntimeHostObservationPayload payload =
            new RuntimeHostAttachmentPublishedObservationPayload(
                endpoint);

        var observation =
            new RuntimeHostObservation(
                sequence,
                endpoint.EndpointId,
                endpoint.Generation,
                payload);

        Assert.Same(
            sequence,
            observation.Sequence);

        Assert.Same(
            endpoint.EndpointId,
            observation.EndpointId);

        Assert.Same(
            endpoint.Generation,
            observation.AttachmentGeneration);

        Assert.Equal(
            RuntimeHostObservationKind.AttachmentPublished,
            observation.Kind);

        Assert.Same(
            payload,
            observation.Payload);
    }

    [Fact]
    public void Observation_NullRequiredValue_Throws()
    {
        PublishedRuntimeEndpointSnapshot endpoint =
            CreatePublishedEndpointSnapshot();

        RuntimeHostObservationPayload payload =
            new RuntimeHostAttachmentPublishedObservationPayload(
                endpoint);

        Assert.Throws<ArgumentNullException>(
            () => new RuntimeHostObservation(
                null!,
                endpoint.EndpointId,
                endpoint.Generation,
                payload));

        Assert.Throws<ArgumentNullException>(
            () => new RuntimeHostObservation(
                new RuntimeHostObservationSequence(
                    1),
                null!,
                endpoint.Generation,
                payload));

        Assert.Throws<ArgumentNullException>(
            () => new RuntimeHostObservation(
                new RuntimeHostObservationSequence(
                    1),
                endpoint.EndpointId,
                null!,
                payload));

        Assert.Throws<ArgumentNullException>(
            () => new RuntimeHostObservation(
                new RuntimeHostObservationSequence(
                    1),
                endpoint.EndpointId,
                endpoint.Generation,
                null!));
    }

    [Fact]
    public void Observation_PublishedEndpointIdentityMismatch_Throws()
    {
        PublishedRuntimeEndpointSnapshot endpoint =
            CreatePublishedEndpointSnapshot();

        Assert.Throws<ArgumentException>(
            () => new RuntimeHostObservation(
                new RuntimeHostObservationSequence(
                    1),
                new EndpointId(
                    "another-endpoint"),
                endpoint.Generation,
                new RuntimeHostAttachmentPublishedObservationPayload(
                    endpoint)));
    }

    [Fact]
    public void Observation_PublishedGenerationMismatch_Throws()
    {
        PublishedRuntimeEndpointSnapshot endpoint =
            CreatePublishedEndpointSnapshot();

        Assert.Throws<ArgumentException>(
            () => new RuntimeHostObservation(
                new RuntimeHostObservationSequence(
                    1),
                endpoint.EndpointId,
                RuntimeEndpointAttachmentGeneration.CreateNew(),
                new RuntimeHostAttachmentPublishedObservationPayload(
                    endpoint)));
    }

    [Fact]
    public void AttachmentEndedPayload_StoresUtcEndTimeAndKind()
    {
        DateTimeOffset endedAtUtc =
            new(
                2026,
                7,
                24,
                16,
                30,
                0,
                TimeSpan.Zero);

        var payload =
            new RuntimeHostAttachmentEndedObservationPayload(
                endedAtUtc);

        Assert.Equal(
            RuntimeHostObservationKind.AttachmentEnded,
            payload.Kind);

        Assert.Equal(
            endedAtUtc,
            payload.EndedAtUtc);
    }

    [Fact]
    public void AttachmentEndedPayload_NonUtcEndTime_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new RuntimeHostAttachmentEndedObservationPayload(
                new DateTimeOffset(
                    2026,
                    7,
                    24,
                    18,
                    30,
                    0,
                    TimeSpan.FromHours(
                        2))));
    }

    [Fact]
    public void ConnectionStatusChangedPayload_StoresStatusesAndKind()
    {
        EndpointConnectionStatus previousStatus =
            new(
                EndpointConnectionState.Connecting);

        EndpointConnectionStatus currentStatus =
            new(
                EndpointConnectionState.Ready);

        var payload =
            new RuntimeHostConnectionStatusChangedObservationPayload(
                previousStatus,
                currentStatus);

        Assert.Equal(
            RuntimeHostObservationKind.ConnectionStatusChanged,
            payload.Kind);

        Assert.Same(
            previousStatus,
            payload.PreviousStatus);

        Assert.Same(
            currentStatus,
            payload.CurrentStatus);
    }

    [Fact]
    public void ConnectionStatusChangedPayload_NullPreviousStatus_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeHostConnectionStatusChangedObservationPayload(
                null!,
                new EndpointConnectionStatus(
                    EndpointConnectionState.Ready)));
    }

    [Fact]
    public void ConnectionStatusChangedPayload_NullCurrentStatus_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeHostConnectionStatusChangedObservationPayload(
                new EndpointConnectionStatus(
                    EndpointConnectionState.Connecting),
                null!));
    }

    [Fact]
    public void PropertyValueChangedPayload_StoresValuesAndKind()
    {
        InstrumentId instrumentId =
            new(
                "instrument-one");

        PropertyId propertyId =
            new(
                "property-one");

        PropertyValue previousValue =
            CreatePropertyValue(
                false,
                1);

        PropertyValue currentValue =
            CreatePropertyValue(
                true,
                2);

        var payload =
            new RuntimeHostPropertyValueChangedObservationPayload(
                instrumentId,
                propertyId,
                previousValue,
                currentValue);

        Assert.Equal(
            RuntimeHostObservationKind.PropertyValueChanged,
            payload.Kind);

        Assert.Same(
            instrumentId,
            payload.InstrumentId);

        Assert.Same(
            propertyId,
            payload.PropertyId);

        Assert.Same(
            previousValue,
            payload.PreviousValue);

        Assert.Same(
            currentValue,
            payload.CurrentValue);
    }

    [Fact]
    public void PropertyValueChangedPayload_AllowsUnknownPreviousValue()
    {
        var payload =
            new RuntimeHostPropertyValueChangedObservationPayload(
                new InstrumentId(
                    "instrument-one"),
                new PropertyId(
                    "property-one"),
                null,
                CreatePropertyValue(
                    true,
                    1));

        Assert.Null(
            payload.PreviousValue);
    }

    [Fact]
    public void PropertyValueChangedPayload_NullInstrumentId_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeHostPropertyValueChangedObservationPayload(
                null!,
                new PropertyId(
                    "property-one"),
                null,
                CreatePropertyValue(
                    true,
                    1)));
    }

    [Fact]
    public void PropertyValueChangedPayload_NullPropertyId_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeHostPropertyValueChangedObservationPayload(
                new InstrumentId(
                    "instrument-one"),
                null!,
                null,
                CreatePropertyValue(
                    true,
                    1)));
    }

    [Fact]
    public void PropertyValueChangedPayload_NullCurrentValue_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeHostPropertyValueChangedObservationPayload(
                new InstrumentId(
                    "instrument-one"),
                new PropertyId(
                    "property-one"),
                null,
                null!));
    }

    [Fact]
    public void EventOccurredPayload_StoresOccurrenceAndKind()
    {
        InstrumentId instrumentId =
            new(
                "instrument-one");

        DescriptorPath eventPath =
            new(
                "Button",
                "Pressed");

        DateTimeOffset occurredAtUtc =
            new(
                2026,
                7,
                24,
                16,
                31,
                0,
                TimeSpan.Zero);

        object value =
            true;

        var payload =
            new RuntimeHostEventOccurredObservationPayload(
                instrumentId,
                eventPath,
                occurredAtUtc,
                value);

        Assert.Equal(
            RuntimeHostObservationKind.EventOccurred,
            payload.Kind);

        Assert.Same(
            instrumentId,
            payload.InstrumentId);

        Assert.Same(
            eventPath,
            payload.EventPath);

        Assert.Equal(
            occurredAtUtc,
            payload.OccurredAtUtc);

        Assert.Same(
            value,
            payload.Value);
    }

    [Fact]
    public void EventOccurredPayload_AllowsNullValue()
    {
        var payload =
            new RuntimeHostEventOccurredObservationPayload(
                new InstrumentId(
                    "instrument-one"),
                new DescriptorPath(
                    "Button",
                    "Pressed"),
                DateTimeOffset.UtcNow,
                null);

        Assert.Null(
            payload.Value);
    }

    [Fact]
    public void EventOccurredPayload_NullInstrumentId_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeHostEventOccurredObservationPayload(
                null!,
                new DescriptorPath(
                    "Button",
                    "Pressed"),
                DateTimeOffset.UtcNow,
                null));
    }

    [Fact]
    public void EventOccurredPayload_NullEventPath_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeHostEventOccurredObservationPayload(
                new InstrumentId(
                    "instrument-one"),
                null!,
                DateTimeOffset.UtcNow,
                null));
    }

    [Fact]
    public void EventOccurredPayload_NonUtcOccurrenceTime_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new RuntimeHostEventOccurredObservationPayload(
                new InstrumentId(
                    "instrument-one"),
                new DescriptorPath(
                    "Button",
                    "Pressed"),
                new DateTimeOffset(
                    2026,
                    7,
                    24,
                    18,
                    31,
                    0,
                    TimeSpan.FromHours(
                        2)),
                null));
    }

    [Fact]
    public void SubscriptionOptions_DefaultCapacity_IsBoundedAndPositive()
    {
        var options =
            new RuntimeHostObservationSubscriptionOptions();

        Assert.Equal(
            RuntimeHostObservationSubscriptionOptions.DefaultBufferCapacity,
            options.BufferCapacity);

        Assert.True(
            options.BufferCapacity > 0);
    }

    [Fact]
    public void SubscriptionOptions_PositiveCapacity_StoresValue()
    {
        var options =
            new RuntimeHostObservationSubscriptionOptions(
                32);

        Assert.Equal(
            32,
            options.BufferCapacity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SubscriptionOptions_NonPositiveCapacity_Throws(
        int bufferCapacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new RuntimeHostObservationSubscriptionOptions(
                bufferCapacity));
    }

    private static PublishedRuntimeEndpointSnapshot CreatePublishedEndpointSnapshot()
    {
        var endpointId =
            new EndpointId(
                "endpoint-one");

        var descriptor =
            new EndpointDescriptor(
                endpointId);

        return new PublishedRuntimeEndpointSnapshot(
            RuntimeEndpointAttachmentGeneration.CreateNew(),
            descriptor,
            new EndpointConnectionStatus(
                EndpointConnectionState.Ready));
    }

    private static PropertyValue CreatePropertyValue(
        object? value,
        int second)
    {
        return new PropertyValue(
            value,
            new DateTimeOffset(
                2026,
                7,
                24,
                16,
                30,
                second,
                TimeSpan.Zero));
    }
}