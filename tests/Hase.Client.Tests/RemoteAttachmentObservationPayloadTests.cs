using Hase.Client;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;

namespace Hase.Client.Tests;

public sealed class RemoteAttachmentObservationPayloadTests
{
    [Fact]
    public void Published_Endpoint_ShouldPreservePayload()
    {
        RemoteEndpointAttachmentSnapshot endpoint =
            CreateEndpoint();

        var payload =
            new RemoteAttachmentPublishedObservationPayload(
                endpoint);

        Assert.Equal(
            RemoteObservationKind.AttachmentPublished,
            payload.Kind);
        Assert.Same(
            endpoint,
            payload.Endpoint);
    }

    [Fact]
    public void Published_NullEndpoint_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "endpoint",
            () => new RemoteAttachmentPublishedObservationPayload(
                null!));
    }

    [Fact]
    public void Ended_UtcTime_ShouldPreservePayload()
    {
        DateTimeOffset endedAtUtc =
            new(
                2026,
                7,
                27,
                8,
                0,
                0,
                TimeSpan.Zero);

        var payload =
            new RemoteAttachmentEndedObservationPayload(
                endedAtUtc);

        Assert.Equal(
            RemoteObservationKind.AttachmentEnded,
            payload.Kind);
        Assert.Equal(
            endedAtUtc,
            payload.EndedAtUtc);
    }

    [Fact]
    public void Ended_NonUtcTime_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "endedAtUtc",
            () => new RemoteAttachmentEndedObservationPayload(
                new DateTimeOffset(
                    2026,
                    7,
                    27,
                    10,
                    0,
                    0,
                    TimeSpan.FromHours(
                        2))));
    }

    private static RemoteEndpointAttachmentSnapshot CreateEndpoint()
    {
        return new RemoteEndpointAttachmentSnapshot(
            new RemoteEndpointAttachmentGeneration(
                Guid.Parse(
                    "0a11d9d4-7a02-43be-ae3f-eef9d11e0de8")),
            new EndpointDescriptor(
                new EndpointId(
                    "endpoint-01")),
            new RemoteEndpointConnectionStatus(
                RemoteEndpointConnectionState.Ready));
    }
}
