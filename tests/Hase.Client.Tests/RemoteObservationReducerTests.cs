using Hase.Client;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Events;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;

namespace Hase.Client.Tests;

public sealed class RemoteObservationReducerTests
{
    private static readonly Guid GenerationOne =
        Guid.Parse(
            "0a11d9d4-7a02-43be-ae3f-eef9d11e0de8");

    private static readonly Guid GenerationTwo =
        Guid.Parse(
            "f64f6262-f154-4399-8c32-4bf15a1133af");

    [Fact]
    public void Empty_ShouldRepresentUninitializedStream()
    {
        RemoteObservationState state =
            RemoteObservationState.Empty;

        Assert.False(
            state.IsInitialized);
        Assert.Null(
            state.Snapshot);
        Assert.Null(
            state.LastSequence);
        Assert.Empty(
            state.PropertyValues);
    }

    [Fact]
    public void Initialize_InitialSnapshot_ShouldCreateImmutableState()
    {
        RemoteObservationInitialSnapshot initial =
            CreateInitial();

        RemoteObservationState state =
            new RemoteObservationReducer().Initialize(
                RemoteObservationState.Empty,
                initial);

        Assert.True(
            state.IsInitialized);
        Assert.Same(
            initial.Snapshot,
            state.Snapshot);
        Assert.Same(
            initial.SnapshotSequence,
            state.LastSequence);
        Assert.Empty(
            state.PropertyValues);
    }

    [Fact]
    public void Initialize_InitializedState_ShouldRejectDuplicateSnapshot()
    {
        var reducer =
            new RemoteObservationReducer();
        RemoteObservationState state =
            reducer.Initialize(
                RemoteObservationState.Empty,
                CreateInitial());

        Assert.Throws<InvalidDataException>(
            () => reducer.Initialize(
                state,
                CreateInitial()));
    }

    [Fact]
    public void Apply_UninitializedState_ShouldThrow()
    {
        Assert.Throws<InvalidDataException>(
            () => new RemoteObservationReducer().Apply(
                RemoteObservationState.Empty,
                CreateEndedObservation(
                    1,
                    CreateKey())));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Apply_NonIncreasingSequence_ShouldThrow(
        ulong sequence)
    {
        RemoteObservationState state =
            CreateInitializedState();

        Assert.Throws<InvalidDataException>(
            () => new RemoteObservationReducer().Apply(
                state,
                CreateEndedObservation(
                    sequence,
                    CreateKey())));
    }

    [Fact]
    public void Apply_AttachmentPublished_ShouldAddCurrentAttachment()
    {
        RemoteObservationState state =
            CreateInitializedState(
                attachments: []);
        RemoteEndpointAttachmentSnapshot endpoint =
            CreateEndpoint();

        RemoteObservationState result =
            new RemoteObservationReducer().Apply(
                state,
                new RemoteRuntimeHostObservation(
                    new RemoteObservationSequence(
                        2),
                    endpoint.Key,
                    new RemoteAttachmentPublishedObservationPayload(
                        endpoint)));

        Assert.Single(
            result.Snapshot!.Attachments);
        Assert.Same(
            endpoint,
            result.Snapshot.Attachments[0]);
    }

    [Fact]
    public void Apply_AttachmentPublishedForExistingEndpoint_ShouldThrow()
    {
        RemoteObservationState state =
            CreateInitializedState();
        RemoteEndpointAttachmentSnapshot replacement =
            CreateEndpoint(
                generation: GenerationTwo);

        Assert.Throws<InvalidDataException>(
            () => new RemoteObservationReducer().Apply(
                state,
                new RemoteRuntimeHostObservation(
                    new RemoteObservationSequence(
                        2),
                    replacement.Key,
                    new RemoteAttachmentPublishedObservationPayload(
                        replacement))));
    }

    [Fact]
    public void Apply_AttachmentEnded_ShouldRemoveAttachmentAndValues()
    {
        var reducer =
            new RemoteObservationReducer();
        RemoteObservationState state =
            reducer.Apply(
                CreateInitializedState(),
                CreatePropertyObservation(
                    2,
                    CreateKey(),
                    null,
                    CreateValue(
                        true)));

        RemoteObservationState result =
            reducer.Apply(
                state,
                CreateEndedObservation(
                    3,
                    CreateKey()));

        Assert.Empty(
            result.Snapshot!.Attachments);
        Assert.Empty(
            result.PropertyValues);
    }

    [Fact]
    public void Apply_AttachmentEndedForStaleGeneration_ShouldThrow()
    {
        RemoteObservationState state =
            CreateInitializedState();

        Assert.Throws<InvalidDataException>(
            () => new RemoteObservationReducer().Apply(
                state,
                CreateEndedObservation(
                    2,
                    CreateKey(
                        GenerationTwo))));
    }

    [Fact]
    public void Apply_ConnectionStatusChanged_ShouldReplaceStatus()
    {
        RemoteObservationState state =
            CreateInitializedState();
        var disconnected =
            new RemoteEndpointConnectionStatus(
                RemoteEndpointConnectionState.Disconnected);

        RemoteObservationState result =
            new RemoteObservationReducer().Apply(
                state,
                new RemoteRuntimeHostObservation(
                    new RemoteObservationSequence(
                        2),
                    CreateKey(),
                    new RemoteConnectionStatusChangedObservationPayload(
                        CreateReadyStatus(),
                        disconnected)));

        Assert.Same(
            disconnected,
            result.Snapshot!.Attachments[0].ConnectionStatus);
    }

    [Fact]
    public void Apply_ConnectionStatusPreviousMismatch_ShouldThrow()
    {
        RemoteObservationState state =
            CreateInitializedState();

        Assert.Throws<InvalidDataException>(
            () => new RemoteObservationReducer().Apply(
                state,
                new RemoteRuntimeHostObservation(
                    new RemoteObservationSequence(
                        2),
                    CreateKey(),
                    new RemoteConnectionStatusChangedObservationPayload(
                        new RemoteEndpointConnectionStatus(
                            RemoteEndpointConnectionState.Disconnected),
                        CreateReadyStatus()))));
    }

    [Fact]
    public void Apply_PropertyValueChanged_ShouldStoreLatestValue()
    {
        RemoteObservationState state =
            CreateInitializedState();
        RemotePropertyValue value =
            CreateValue(
                true);

        RemoteObservationState result =
            new RemoteObservationReducer().Apply(
                state,
                CreatePropertyObservation(
                    2,
                    CreateKey(),
                    null,
                    value));

        RemotePropertyValue stored =
            Assert.Single(
                result.PropertyValues).Value;
        Assert.Same(
            value,
            stored);
    }

    [Fact]
    public void Apply_PropertyValueChanged_ShouldReplaceMatchingPreviousValue()
    {
        var reducer =
            new RemoteObservationReducer();
        RemotePropertyValue previous =
            CreateValue(
                false);
        RemoteObservationState state =
            reducer.Apply(
                CreateInitializedState(),
                CreatePropertyObservation(
                    2,
                    CreateKey(),
                    null,
                    previous));
        RemotePropertyValue current =
            CreateValue(
                true);

        RemoteObservationState result =
            reducer.Apply(
                state,
                CreatePropertyObservation(
                    3,
                    CreateKey(),
                    previous,
                    current));

        Assert.Same(
            current,
            Assert.Single(
                result.PropertyValues).Value);
    }

    [Fact]
    public void Apply_PropertyValueChangedWithPreviousMismatch_ShouldThrow()
    {
        var reducer =
            new RemoteObservationReducer();
        RemoteObservationState state =
            reducer.Apply(
                CreateInitializedState(),
                CreatePropertyObservation(
                    2,
                    CreateKey(),
                    null,
                    CreateValue(
                        false)));

        Assert.Throws<InvalidDataException>(
            () => reducer.Apply(
                state,
                CreatePropertyObservation(
                    3,
                    CreateKey(),
                    CreateValue(
                        true),
                    CreateValue(
                        false))));
    }

    [Fact]
    public void Apply_PropertyForUnknownInstrument_ShouldThrow()
    {
        RemoteObservationState state =
            CreateInitializedState();

        Assert.Throws<InvalidDataException>(
            () => new RemoteObservationReducer().Apply(
                state,
                new RemoteRuntimeHostObservation(
                    new RemoteObservationSequence(
                        2),
                    CreateKey(),
                    new RemotePropertyValueChangedObservationPayload(
                        new InstrumentId(
                            "unknown"),
                        new PropertyId(
                            "enabled"),
                        null,
                        CreateValue(
                            true)))));
    }

    [Fact]
    public void Apply_PropertyForUnknownProperty_ShouldThrow()
    {
        RemoteObservationState state =
            CreateInitializedState();

        Assert.Throws<InvalidDataException>(
            () => new RemoteObservationReducer().Apply(
                state,
                new RemoteRuntimeHostObservation(
                    new RemoteObservationSequence(
                        2),
                    CreateKey(),
                    new RemotePropertyValueChangedObservationPayload(
                        new InstrumentId(
                            "instrument-01"),
                        new PropertyId(
                            "unknown"),
                        null,
                        CreateValue(
                            true)))));
    }

    [Fact]
    public void Apply_EventOccurred_ShouldAdvanceWithoutPersistingEvent()
    {
        RemoteObservationState state =
            CreateInitializedState();

        RemoteObservationState result =
            new RemoteObservationReducer().Apply(
                state,
                new RemoteRuntimeHostObservation(
                    new RemoteObservationSequence(
                        2),
                    CreateKey(),
                    new RemoteEventOccurredObservationPayload(
                        new InstrumentId(
                            "instrument-01"),
                        new DescriptorPath(
                            "ButtonPressed"),
                        DateTimeOffset.UnixEpoch,
                        RemoteValue.FromBoolean(
                            true))));

        Assert.Equal(
            2UL,
            result.LastSequence!.Value);
        Assert.Equal(
            state.Snapshot!.RuntimeHostId,
            result.Snapshot!.RuntimeHostId);
        Assert.Equal(
            state.Snapshot.ApiVersion,
            result.Snapshot.ApiVersion);
        Assert.Equal(
            state.Snapshot.Attachments,
            result.Snapshot.Attachments);
        Assert.Empty(
            result.PropertyValues);
    }

    [Fact]
    public void Apply_EventForUnknownPath_ShouldThrow()
    {
        RemoteObservationState state =
            CreateInitializedState();

        Assert.Throws<InvalidDataException>(
            () => new RemoteObservationReducer().Apply(
                state,
                new RemoteRuntimeHostObservation(
                    new RemoteObservationSequence(
                        2),
                    CreateKey(),
                    new RemoteEventOccurredObservationPayload(
                        new InstrumentId(
                            "instrument-01"),
                        new DescriptorPath(
                            "Unknown"),
                        DateTimeOffset.UnixEpoch,
                        null))));
    }

    private static RemoteObservationState CreateInitializedState(
        IReadOnlyList<RemoteEndpointAttachmentSnapshot>? attachments = null)
    {
        return new RemoteObservationReducer().Initialize(
            RemoteObservationState.Empty,
            CreateInitial(
                attachments));
    }

    private static RemoteObservationInitialSnapshot CreateInitial(
        IReadOnlyList<RemoteEndpointAttachmentSnapshot>? attachments = null)
    {
        return new RemoteObservationInitialSnapshot(
            new RemoteRuntimeHostSnapshot(
                new RemoteRuntimeHostId(
                    "runtime-01"),
                new RuntimeHostClientApiVersion(
                    1,
                    0),
                attachments
                    ?? [CreateEndpoint()]),
            new RemoteObservationSequence(
                1));
    }

    private static RemoteEndpointAttachmentSnapshot CreateEndpoint(
        Guid? generation = null)
    {
        var instrument =
            new InstrumentDescriptor(
                new InstrumentId(
                    "instrument-01"),
                "Controller",
                new InstrumentKind(
                    "controller"))
            {
                Interface =
                    new InstrumentInterface(
                        properties:
                        [
                            new PropertyDescriptor(
                                new PropertyId(
                                    "enabled"),
                                new DescriptorPath(
                                    "Enabled"),
                                "Enabled",
                                new BooleanDataDescriptor())
                        ],
                        events:
                        [
                            new EventDescriptor(
                                new DescriptorPath(
                                    "ButtonPressed"),
                                "Button pressed")
                        ])
            };

        return new RemoteEndpointAttachmentSnapshot(
            new RemoteEndpointAttachmentGeneration(
                generation
                    ?? GenerationOne),
            new EndpointDescriptor(
                new EndpointId(
                    "endpoint-01"),
                [instrument]),
            CreateReadyStatus());
    }

    private static RemoteEndpointAttachmentKey CreateKey(
        Guid? generation = null)
    {
        return new RemoteEndpointAttachmentKey(
            new EndpointId(
                "endpoint-01"),
            new RemoteEndpointAttachmentGeneration(
                generation
                    ?? GenerationOne));
    }

    private static RemoteEndpointConnectionStatus CreateReadyStatus()
    {
        return new RemoteEndpointConnectionStatus(
            RemoteEndpointConnectionState.Ready);
    }

    private static RemotePropertyValue CreateValue(
        bool value)
    {
        return new RemotePropertyValue(
            RemoteValue.FromBoolean(
                value),
            DateTimeOffset.UnixEpoch,
            RemotePropertyQuality.Good);
    }

    private static RemoteRuntimeHostObservation CreatePropertyObservation(
        ulong sequence,
        RemoteEndpointAttachmentKey attachment,
        RemotePropertyValue? previous,
        RemotePropertyValue current)
    {
        return new RemoteRuntimeHostObservation(
            new RemoteObservationSequence(
                sequence),
            attachment,
            new RemotePropertyValueChangedObservationPayload(
                new InstrumentId(
                    "instrument-01"),
                new PropertyId(
                    "enabled"),
                previous,
                current));
    }

    private static RemoteRuntimeHostObservation CreateEndedObservation(
        ulong sequence,
        RemoteEndpointAttachmentKey attachment)
    {
        return new RemoteRuntimeHostObservation(
            new RemoteObservationSequence(
                sequence),
            attachment,
            new RemoteAttachmentEndedObservationPayload(
                DateTimeOffset.UnixEpoch));
    }
}
