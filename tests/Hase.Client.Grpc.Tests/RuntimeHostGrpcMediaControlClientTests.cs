using Google.Protobuf.WellKnownTypes;
using Hase.Client.Media;
using MediaV1 = global::Hase.Runtime.Media.Grpc.V1;

namespace Hase.Client.Grpc.Tests;

public sealed class RuntimeHostGrpcMediaControlClientTests
{
    [Fact]
    public async Task CapabilitiesMapLogicalSourceWithoutInventingDeviceData()
    {
        var transport = new RecordingTransport
        {
            CapabilitiesResponse = new MediaV1.GetMediaCapabilitiesResponse
            {
                Sources =
                {
                    new MediaV1.MediaSourceCapability
                    {
                        Target = Target(),
                        DisplayName = "USB camera",
                        Availability = MediaV1.MediaSourceAvailability.Idle,
                        SupportsVideo = true,
                        SupportsAudio = true
                    }
                }
            }
        };
        var client = new RuntimeHostGrpcMediaControlClient(transport);

        RemoteMediaSourceCapability source = Assert.Single(
            await client.GetCapabilitiesAsync());

        Assert.Equal("camera-01", source.Target.MediaSourceId);
        Assert.Equal("USB camera", source.DisplayName);
        Assert.True(source.SupportsAudio);
    }

    [Fact]
    public async Task CapabilityWatchPreservesRevisionAndCompleteSnapshot()
    {
        var transport = new RecordingTransport
        {
            CapabilityWatchResponses =
            [
                new MediaV1.GetMediaCapabilitiesResponse
                {
                    CapabilityRevision = 7,
                    Sources =
                    {
                        new MediaV1.MediaSourceCapability
                        {
                            Target = Target(),
                            DisplayName = "USB camera",
                            Availability = MediaV1.MediaSourceAvailability.Idle,
                            SupportsVideo = true
                        }
                    }
                }
            ]
        };
        var client = new RuntimeHostGrpcMediaControlClient(transport);

        var snapshots = new List<RemoteMediaCapabilitySnapshot>();
        await foreach (RemoteMediaCapabilitySnapshot snapshot in
            client.WatchCapabilitiesAsync(afterRevision: 6))
        {
            snapshots.Add(snapshot);
        }

        RemoteMediaCapabilitySnapshot actual = Assert.Single(snapshots);
        Assert.Equal((ulong)7, actual.Revision);
        Assert.Equal("camera-01",
            Assert.Single(actual.Sources).Target.MediaSourceId);
        Assert.Equal((ulong)6, transport.WatchRequest!.AfterRevision);
    }

    [Fact]
    public async Task StartPreservesExactLogicalTargetAndAudioChoice()
    {
        var transport = new RecordingTransport
        {
            StartResponse = new MediaV1.StartMediaSessionResponse
            {
                Status = MediaV1.MediaControlOperationStatus.Success,
                Session = Snapshot()
            }
        };
        var client = new RuntimeHostGrpcMediaControlClient(transport);

        RemoteMediaStartResult result = await client.StartAsync(
            new("camera-01", "generation-01"), true);

        Assert.True(result.Succeeded);
        Assert.Equal("camera-01", transport.StartRequest!.Target.MediaSourceId);
        Assert.True(transport.StartRequest.IncludeAudio);
        Assert.Equal(RemoteMediaTerminalReason.None,
            result.Session!.TerminalReason);
    }

    [Fact]
    public async Task ExchangeMapsSensitivePayloadsWithoutTransformingThem()
    {
        var transport = new RecordingTransport
        {
            ExchangeResponse = new MediaV1.ExchangeMediaNegotiationResponse
            {
                Status = MediaV1.MediaControlOperationStatus.Success,
                Session = Snapshot(),
                AcceptedSubmissionSequence = 1,
                HasMore = false,
                DeliveredMessages =
                {
                    new MediaV1.MediaNegotiationMessage
                    {
                        Sequence = 2,
                        Kind = MediaV1.MediaNegotiationMessageKind.IceCandidate,
                        SensitivePayload = "host-ice-secret"
                    }
                }
            }
        };
        var client = new RuntimeHostGrpcMediaControlClient(transport);

        RemoteMediaExchangeResult result = await client.ExchangeAsync(
            "session-01",
            1,
            new(1, RemoteMediaNegotiationKind.Answer, "client-sdp-secret"));

        Assert.True(result.Succeeded);
        Assert.Equal((uint)1, transport.ExchangeRequest!
            .AcknowledgedDeliverySequence);
        Assert.Equal("client-sdp-secret",
            transport.ExchangeRequest.SubmittedMessage.SensitivePayload);
        Assert.Equal("host-ice-secret",
            Assert.Single(result.DeliveredMessages).SensitivePayload);
        Assert.Equal(RemoteMediaNegotiationKind.IceCandidate,
            result.DeliveredMessages[0].Kind);
    }

    [Theory]
    [InlineData(MediaV1.MediaControlOperationStatus.SessionNotOwned,
        "session-not-owned")]
    [InlineData(MediaV1.MediaControlOperationStatus.LimitExceeded,
        "limit-exceeded")]
    [InlineData(MediaV1.MediaControlOperationStatus.Faulted, "faulted")]
    [InlineData(MediaV1.MediaControlOperationStatus.Unspecified,
        "invalid-request")]
    public async Task StatusReturnsStableSanitizedFailureCode(
        MediaV1.MediaControlOperationStatus status,
        string expectedFailureCode)
    {
        var transport = new RecordingTransport
        {
            StatusResponse = new MediaV1.GetMediaSessionStatusResponse
            {
                Status = status
            }
        };
        var client = new RuntimeHostGrpcMediaControlClient(transport);

        RemoteMediaStatusResult result =
            await client.GetStatusAsync("session-01");

        Assert.False(result.Succeeded);
        Assert.Equal(expectedFailureCode, result.FailureCode);
        Assert.Equal("session-01", transport.StatusRequest!.SessionId);
    }

    [Fact]
    public async Task StopMapsTerminalSnapshot()
    {
        MediaV1.MediaSessionSnapshot snapshot = Snapshot();
        snapshot.State = MediaV1.MediaSessionState.Ended;
        snapshot.TerminalReason =
            MediaV1.MediaSessionTerminalReason.ClientStopped;
        var transport = new RecordingTransport
        {
            StopResponse = new MediaV1.StopMediaSessionResponse
            {
                Status = MediaV1.MediaControlOperationStatus.Success,
                Session = snapshot
            }
        };
        var client = new RuntimeHostGrpcMediaControlClient(transport);

        RemoteMediaStopResult result = await client.StopAsync("session-01");

        Assert.True(result.Succeeded);
        Assert.Equal(RemoteMediaSessionState.Ended, result.Session!.State);
        Assert.Equal(RemoteMediaTerminalReason.ClientStopped,
            result.Session.TerminalReason);
    }

    private static MediaV1.MediaSourceTarget Target() => new()
    {
        MediaSourceId = "camera-01",
        MediaSourceGeneration = "generation-01"
    };

    private static MediaV1.MediaSessionSnapshot Snapshot()
    {
        var now = new DateTimeOffset(
            2026, 8, 16, 8, 0, 0, TimeSpan.Zero);
        return new()
        {
            SessionId = "session-01",
            Target = Target(),
            AudioRequested = true,
            State = MediaV1.MediaSessionState.Negotiating,
            StartedAtUtc = Timestamp.FromDateTimeOffset(now),
            LastTransitionAtUtc = Timestamp.FromDateTimeOffset(now),
            LeaseExpiresAtUtc = Timestamp.FromDateTimeOffset(
                now + TimeSpan.FromSeconds(30))
        };
    }

    private sealed class RecordingTransport :
        IRuntimeHostMediaGrpcTransport,
        IRuntimeHostMediaCapabilityGrpcTransport
    {
        public MediaV1.GetMediaCapabilitiesResponse CapabilitiesResponse
            { get; init; } = new();
        public MediaV1.StartMediaSessionResponse StartResponse
            { get; init; } = new();
        public MediaV1.ExchangeMediaNegotiationResponse ExchangeResponse
            { get; init; } = new();
        public MediaV1.GetMediaSessionStatusResponse StatusResponse
            { get; init; } = new();
        public MediaV1.StopMediaSessionResponse StopResponse
            { get; init; } = new();
        public IReadOnlyList<MediaV1.GetMediaCapabilitiesResponse>
            CapabilityWatchResponses { get; init; } = [];

        public MediaV1.StartMediaSessionRequest? StartRequest { get; private set; }
        public MediaV1.ExchangeMediaNegotiationRequest? ExchangeRequest
            { get; private set; }
        public MediaV1.GetMediaSessionStatusRequest? StatusRequest
            { get; private set; }
        public MediaV1.WatchMediaCapabilitiesRequest? WatchRequest
            { get; private set; }

        public Task<MediaV1.GetMediaCapabilitiesResponse> GetCapabilitiesAsync(
            MediaV1.GetMediaCapabilitiesRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(CapabilitiesResponse);

        public async IAsyncEnumerable<MediaV1.GetMediaCapabilitiesResponse>
            WatchCapabilitiesAsync(
                MediaV1.WatchMediaCapabilitiesRequest request,
                [System.Runtime.CompilerServices.EnumeratorCancellation]
                CancellationToken cancellationToken)
        {
            WatchRequest = request;
            foreach (MediaV1.GetMediaCapabilitiesResponse response in
                CapabilityWatchResponses)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return response;
                await Task.Yield();
            }
        }

        public Task<MediaV1.StartMediaSessionResponse> StartAsync(
            MediaV1.StartMediaSessionRequest request,
            CancellationToken cancellationToken)
        {
            StartRequest = request;
            return Task.FromResult(StartResponse);
        }

        public Task<MediaV1.ExchangeMediaNegotiationResponse> ExchangeAsync(
            MediaV1.ExchangeMediaNegotiationRequest request,
            CancellationToken cancellationToken)
        {
            ExchangeRequest = request;
            return Task.FromResult(ExchangeResponse);
        }

        public Task<MediaV1.GetMediaSessionStatusResponse> GetStatusAsync(
            MediaV1.GetMediaSessionStatusRequest request,
            CancellationToken cancellationToken)
        {
            StatusRequest = request;
            return Task.FromResult(StatusResponse);
        }

        public Task<MediaV1.StopMediaSessionResponse> StopAsync(
            MediaV1.StopMediaSessionRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(StopResponse);
    }
}
