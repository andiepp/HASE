using Hase.Client;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;

namespace Hase.Client.Tests;

public sealed class RemoteRuntimeHostObservationTests
{
    [Fact]
    public void Constructor_Values_ShouldPreserveEnvelope()
    {
        var sequence =
            new RemoteObservationSequence(
                1);
        RemoteEndpointAttachmentKey attachment =
            CreateAttachmentKey();
        var payload =
            new RemoteAttachmentEndedObservationPayload(
                DateTimeOffset.UnixEpoch);

        var observation =
            new RemoteRuntimeHostObservation(
                sequence,
                attachment,
                payload);

        Assert.Same(
            sequence,
            observation.Sequence);
        Assert.Same(
            attachment,
            observation.Attachment);
        Assert.Equal(
            RemoteObservationKind.AttachmentEnded,
            observation.Kind);
        Assert.Same(
            payload,
            observation.Payload);
    }

    [Fact]
    public void Constructor_MatchingPublishedAttachment_ShouldSucceed()
    {
        RemoteEndpointAttachmentSnapshot endpoint =
            CreateEndpoint(
                CreateAttachmentKey());

        var observation =
            new RemoteRuntimeHostObservation(
                new RemoteObservationSequence(
                    1),
                endpoint.Key,
                new RemoteAttachmentPublishedObservationPayload(
                    endpoint));

        Assert.Equal(
            RemoteObservationKind.AttachmentPublished,
            observation.Kind);
    }

    [Fact]
    public void Constructor_MismatchedPublishedEndpointId_ShouldThrow()
    {
        RemoteEndpointAttachmentKey attachment =
            CreateAttachmentKey();
        RemoteEndpointAttachmentSnapshot endpoint =
            CreateEndpoint(
                new RemoteEndpointAttachmentKey(
                    new EndpointId(
                        "endpoint-02"),
                    attachment.Generation));

        Assert.Throws<ArgumentException>(
            "payload",
            () => new RemoteRuntimeHostObservation(
                new RemoteObservationSequence(
                    1),
                attachment,
                new RemoteAttachmentPublishedObservationPayload(
                    endpoint)));
    }

    [Fact]
    public void Constructor_MismatchedPublishedGeneration_ShouldThrow()
    {
        RemoteEndpointAttachmentKey attachment =
            CreateAttachmentKey();
        RemoteEndpointAttachmentSnapshot endpoint =
            CreateEndpoint(
                new RemoteEndpointAttachmentKey(
                    attachment.EndpointId,
                    new RemoteEndpointAttachmentGeneration(
                        Guid.Parse(
                            "f64f6262-f154-4399-8c32-4bf15a1133af"))));

        Assert.Throws<ArgumentException>(
            "payload",
            () => new RemoteRuntimeHostObservation(
                new RemoteObservationSequence(
                    1),
                attachment,
                new RemoteAttachmentPublishedObservationPayload(
                    endpoint)));
    }

    [Fact]
    public void Constructor_NullSequence_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "sequence",
            () => new RemoteRuntimeHostObservation(
                null!,
                CreateAttachmentKey(),
                new RemoteAttachmentEndedObservationPayload(
                    DateTimeOffset.UnixEpoch)));
    }

    [Fact]
    public void Constructor_NullAttachment_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "attachment",
            () => new RemoteRuntimeHostObservation(
                new RemoteObservationSequence(
                    1),
                null!,
                new RemoteAttachmentEndedObservationPayload(
                    DateTimeOffset.UnixEpoch)));
    }

    [Fact]
    public void Constructor_NullPayload_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "payload",
            () => new RemoteRuntimeHostObservation(
                new RemoteObservationSequence(
                    1),
                CreateAttachmentKey(),
                null!));
    }

    private static RemoteEndpointAttachmentKey CreateAttachmentKey()
    {
        return new RemoteEndpointAttachmentKey(
            new EndpointId(
                "endpoint-01"),
            new RemoteEndpointAttachmentGeneration(
                Guid.Parse(
                    "0a11d9d4-7a02-43be-ae3f-eef9d11e0de8")));
    }

    private static RemoteEndpointAttachmentSnapshot CreateEndpoint(
        RemoteEndpointAttachmentKey key)
    {
        return new RemoteEndpointAttachmentSnapshot(
            key.Generation,
            new EndpointDescriptor(
                key.EndpointId),
            new RemoteEndpointConnectionStatus(
                RemoteEndpointConnectionState.Ready));
    }
}
