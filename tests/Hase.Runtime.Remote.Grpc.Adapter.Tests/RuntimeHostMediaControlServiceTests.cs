using Grpc.Core;
using Hase.Runtime.Media;
using MediaV1 = global::Hase.Runtime.Media.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostMediaControlServiceTests
{
    [Fact]
    public async Task CapabilitiesRequirePermissionAndExposeLogicalSourcesOnly()
    {
        var boundary = new RecordingBoundary();
        await using var owner = CreateOwner(boundary);
        var authorization = new RecordingAuthorizationService(
            RuntimeHostPermission.ReadMediaCapabilities);
        var service = CreateService(owner, "principal-01", authorization);

        MediaV1.GetMediaCapabilitiesResponse response =
            await service.GetMediaCapabilities(
                new MediaV1.GetMediaCapabilitiesRequest(), null!);

        Assert.Equal("runtime-host-01", response.RuntimeHostId);
        MediaV1.MediaSourceCapability source = Assert.Single(response.Sources);
        Assert.Equal("Operator camera", source.DisplayName);
        Assert.Equal("camera-01", source.Target.MediaSourceId);
        Assert.DoesNotContain("local-device-secret", response.ToString());
        Assert.Equal(
            [RuntimeHostPermission.ReadMediaCapabilities.Value],
            authorization.Requested);
    }

    [Fact]
    public async Task AudioStartRequiresEveryAssignedPermission()
    {
        var boundary = new RecordingBoundary();
        await using var owner = CreateOwner(boundary);
        var authorization = new RecordingAuthorizationService(
            RuntimeHostPermission.ReceiveMediaVideo,
            RuntimeHostPermission.StartMediaSession);
        var service = CreateService(owner, "principal-01", authorization);

        RpcException exception = await Assert.ThrowsAsync<RpcException>(() =>
            service.StartMediaSession(
                new MediaV1.StartMediaSessionRequest
                {
                    Target = Target(),
                    IncludeAudio = true
                },
                null!));

        Assert.Equal(StatusCode.PermissionDenied, exception.StatusCode);
        Assert.DoesNotContain("media.audio.receive", exception.Status.Detail);
        Assert.Empty(boundary.Opened);
    }

    [Fact]
    public async Task ExchangeCarriesAnswerAndAcknowledgedHostOffer()
    {
        var boundary = new RecordingBoundary();
        await using var owner = CreateOwner(boundary);
        var service = CreateService(
            owner,
            "principal-01",
            new RecordingAuthorizationService(
                RuntimeHostPermission.ReceiveMediaVideo,
                RuntimeHostPermission.StartMediaSession,
                RuntimeHostPermission.NegotiateMediaSession));
        MediaV1.StartMediaSessionResponse start = await service.StartMediaSession(
            new MediaV1.StartMediaSessionRequest { Target = Target() }, null!);
        await owner.PublishNegotiationAsync(
            start.Session.SessionId,
            new(1, RuntimeHostMediaNegotiationKind.Offer, "sdp-host-secret"));

        MediaV1.ExchangeMediaNegotiationResponse first =
            await service.ExchangeMediaNegotiation(
                new MediaV1.ExchangeMediaNegotiationRequest
                {
                    SessionId = start.Session.SessionId,
                    SubmittedMessage = new MediaV1.MediaNegotiationMessage
                    {
                        Sequence = 1,
                        Kind = MediaV1.MediaNegotiationMessageKind.Answer,
                        SensitivePayload = "sdp-client-secret"
                    }
                },
                null!);
        MediaV1.ExchangeMediaNegotiationResponse acknowledged =
            await service.ExchangeMediaNegotiation(
                new MediaV1.ExchangeMediaNegotiationRequest
                {
                    SessionId = start.Session.SessionId,
                    AcknowledgedDeliverySequence = 1
                },
                null!);

        Assert.Equal(MediaV1.MediaControlOperationStatus.Success, first.Status);
        Assert.Equal((uint)1, first.AcceptedSubmissionSequence);
        Assert.Equal("sdp-host-secret",
            Assert.Single(first.DeliveredMessages).SensitivePayload);
        Assert.Equal("sdp-client-secret",
            Assert.Single(boundary.Submitted).SensitivePayload);
        Assert.Empty(acknowledged.DeliveredMessages);
        Assert.Equal((uint)1, acknowledged.AcceptedSubmissionSequence);
    }

    [Fact]
    public async Task SessionOwnershipIsEnforcedAfterAuthorization()
    {
        var boundary = new RecordingBoundary();
        await using var owner = CreateOwner(boundary);
        var permissions = new RecordingAuthorizationService(
            RuntimeHostPermission.ReceiveMediaVideo,
            RuntimeHostPermission.StartMediaSession,
            RuntimeHostPermission.StopMediaSession);
        var ownerService = CreateService(owner, "principal-01", permissions);
        MediaV1.StartMediaSessionResponse start =
            await ownerService.StartMediaSession(
                new MediaV1.StartMediaSessionRequest { Target = Target() },
                null!);
        var otherService = CreateService(owner, "principal-02", permissions);

        MediaV1.StopMediaSessionResponse response =
            await otherService.StopMediaSession(
                new MediaV1.StopMediaSessionRequest
                {
                    SessionId = start.Session.SessionId
                },
                null!);

        Assert.Equal(MediaV1.MediaControlOperationStatus.SessionNotOwned,
            response.Status);
        Assert.Equal(0, boundary.CloseCount);
    }

    [Fact]
    public async Task MalformedRequestProducesSanitizedInvalidArgument()
    {
        var boundary = new RecordingBoundary();
        await using var owner = CreateOwner(boundary);
        var service = CreateService(
            owner,
            "principal-01",
            new RecordingAuthorizationService(
                RuntimeHostPermission.ReceiveMediaVideo,
                RuntimeHostPermission.StartMediaSession));

        RpcException exception = await Assert.ThrowsAsync<RpcException>(() =>
            service.StartMediaSession(
                new MediaV1.StartMediaSessionRequest(), null!));

        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
        Assert.Equal("The media-control request is invalid.",
            exception.Status.Detail);
        Assert.Empty(boundary.Opened);
    }

    private static RuntimeHostMediaControlService CreateService(
        RuntimeHostMediaSessionOwner owner,
        string principalId,
        IRuntimeHostAuthorizationService authorization) => new(
        "runtime-host-01",
        owner,
        new FixedPrincipalProvider(Principal(principalId)),
        new RuntimeHostMediaAuthorizationGate(authorization),
        new RuntimeHostMediaCapabilityMapper(),
        new RuntimeHostMediaControlLimitsMapper(),
        new RuntimeHostMediaControlContractValidator(),
        new RuntimeHostMediaGrpcMapper());

    private static RuntimeHostMediaSessionOwner CreateOwner(
        RecordingBoundary boundary) => new(
        new RuntimeHostMediaSourceConfiguration(
            new("camera-01", "generation-01"),
            "local-device-secret",
            "local-audio-secret",
            RuntimeHostMediaSourceAvailability.Idle,
            "Operator camera"),
        boundary);

    private static MediaV1.MediaSourceTarget Target() => new()
    {
        MediaSourceId = "camera-01",
        MediaSourceGeneration = "generation-01"
    };

    private static RuntimeHostClientPrincipal Principal(string id) => new(
        id,
        $"credential-{id}",
        "mutual-tls",
        new DateTimeOffset(2026, 8, 16, 8, 0, 0, TimeSpan.Zero),
        "media-policy-v1");

    private sealed class FixedPrincipalProvider
        : IRuntimeHostClientPrincipalProvider
    {
        private readonly RuntimeHostClientPrincipal principal;

        public FixedPrincipalProvider(RuntimeHostClientPrincipal principal) =>
            this.principal = principal;

        public RuntimeHostClientPrincipal GetPrincipal(
            ServerCallContext? context) => principal;
    }

    private sealed class RecordingAuthorizationService
        : IRuntimeHostAuthorizationService
    {
        private readonly HashSet<string> granted;

        public RecordingAuthorizationService(
            params RuntimeHostPermission[] granted) =>
            this.granted = granted.Select(item => item.Value)
                .ToHashSet(StringComparer.Ordinal);

        public List<string> Requested { get; } = [];

        public RuntimeHostAuthorizationDecision Authorize(
            RuntimeHostClientPrincipal principal,
            RuntimeHostPermission permission)
        {
            Requested.Add(permission.Value);
            return granted.Contains(permission.Value)
                ? RuntimeHostAuthorizationDecision.Allow("test-granted")
                : RuntimeHostAuthorizationDecision.Deny("test-denied");
        }
    }

    private sealed class RecordingBoundary : IRuntimeHostMediaCaptureBoundary
    {
        public List<RuntimeHostMediaSourceConfiguration> Opened { get; } = [];
        public List<RuntimeHostMediaNegotiationMessage> Submitted { get; } = [];
        public int CloseCount { get; private set; }

        public ValueTask OpenAsync(
            RuntimeHostMediaSourceConfiguration source,
            bool includeAudio,
            CancellationToken cancellationToken)
        {
            Opened.Add(source);
            return ValueTask.CompletedTask;
        }

        public ValueTask SubmitNegotiationAsync(
            RuntimeHostMediaNegotiationMessage message,
            CancellationToken cancellationToken)
        {
            Submitted.Add(message);
            return ValueTask.CompletedTask;
        }

        public ValueTask CloseAsync(CancellationToken cancellationToken)
        {
            CloseCount++;
            return ValueTask.CompletedTask;
        }
    }
}
