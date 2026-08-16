using System.Text;
using Grpc.Core;
using Hase.Runtime.Media;
using MediaV1 = global::Hase.Runtime.Media.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Authenticated unary adapter for the version 1 media control plane. It is
/// deliberately not registered by an application in Increment 55E1.
/// </summary>
public sealed class RuntimeHostMediaControlService
    : MediaV1.RuntimeHostMediaControl.RuntimeHostMediaControlBase
{
    private readonly string runtimeHostId;
    private readonly RuntimeHostMediaSessionOwner owner;
    private readonly IRuntimeHostClientPrincipalProvider principalProvider;
    private readonly RuntimeHostMediaAuthorizationGate authorizationGate;
    private readonly RuntimeHostMediaCapabilityMapper capabilityMapper;
    private readonly RuntimeHostMediaControlLimitsMapper limitsMapper;
    private readonly RuntimeHostMediaControlContractValidator validator;
    private readonly RuntimeHostMediaGrpcMapper mapper;

    public RuntimeHostMediaControlService(
        string runtimeHostId,
        RuntimeHostMediaSessionOwner owner,
        IRuntimeHostClientPrincipalProvider principalProvider,
        RuntimeHostMediaAuthorizationGate authorizationGate,
        RuntimeHostMediaCapabilityMapper capabilityMapper,
        RuntimeHostMediaControlLimitsMapper limitsMapper,
        RuntimeHostMediaControlContractValidator validator,
        RuntimeHostMediaGrpcMapper mapper)
    {
        if (string.IsNullOrWhiteSpace(runtimeHostId) ||
            Encoding.UTF8.GetByteCount(runtimeHostId) >
                RuntimeHostMediaControlLimits.MaximumSourceIdentityUtf8Bytes)
        {
            throw new ArgumentException(
                "A bounded Runtime Host identity is required.",
                nameof(runtimeHostId));
        }

        this.runtimeHostId = runtimeHostId;
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        this.principalProvider = principalProvider ??
            throw new ArgumentNullException(nameof(principalProvider));
        this.authorizationGate = authorizationGate ??
            throw new ArgumentNullException(nameof(authorizationGate));
        this.capabilityMapper = capabilityMapper ??
            throw new ArgumentNullException(nameof(capabilityMapper));
        this.limitsMapper = limitsMapper ??
            throw new ArgumentNullException(nameof(limitsMapper));
        this.validator = validator ??
            throw new ArgumentNullException(nameof(validator));
        this.mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public override Task<MediaV1.GetMediaCapabilitiesResponse>
        GetMediaCapabilities(
            MediaV1.GetMediaCapabilitiesRequest request,
            ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimeHostClientPrincipal principal = Authorize(
            context,
            RuntimeHostMediaAuthorizationRequirements.ForCapabilities);
        _ = principal;

        var response = new MediaV1.GetMediaCapabilitiesResponse
        {
            RuntimeHostId = runtimeHostId,
            ApiVersion = new MediaV1.MediaControlApiVersion
            {
                Major = 1,
                Minor = 0
            },
            Limits = limitsMapper.Map()
        };
        response.Sources.AddRange(capabilityMapper.Map(owner.Sources));
        return Task.FromResult(response);
    }

    public override async Task<MediaV1.StartMediaSessionResponse>
        StartMediaSession(
            MediaV1.StartMediaSessionRequest request,
            ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimeHostClientPrincipal principal = Authorize(
            context,
            RuntimeHostMediaAuthorizationRequirements.ForStart(
                request.IncludeAudio));
        Validate(() => validator.ValidateSourceTarget(request.Target));
        RuntimeHostMediaOperationResult result = await owner.StartAsync(
            new(
                principal.PrincipalId,
                mapper.Map(request.Target),
                request.IncludeAudio),
            GetCancellationToken(context)).ConfigureAwait(false);
        var response = new MediaV1.StartMediaSessionResponse
        {
            Status = mapper.Map(result.Status)
        };
        if (result.Session is not null)
        {
            response.Session = mapper.Map(result.Session);
        }
        return response;
    }

    public override async Task<MediaV1.ExchangeMediaNegotiationResponse>
        ExchangeMediaNegotiation(
            MediaV1.ExchangeMediaNegotiationRequest request,
            ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimeHostClientPrincipal principal = Authorize(
            context,
            RuntimeHostMediaAuthorizationRequirements.ForNegotiation);
        Validate(() => validator.ValidateSessionId(request.SessionId));
        if (request.SubmittedMessage is not null)
        {
            Validate(() => validator.ValidateNegotiationMessage(
                request.SubmittedMessage));
        }

        RuntimeHostMediaNegotiationExchangeResult result =
            await owner.ExchangeNegotiationAsync(
                principal.PrincipalId,
                request.SessionId,
                request.AcknowledgedDeliverySequence,
                request.SubmittedMessage is null
                    ? null
                    : mapper.Map(request.SubmittedMessage),
                GetCancellationToken(context)).ConfigureAwait(false);
        var response = new MediaV1.ExchangeMediaNegotiationResponse
        {
            Status = mapper.Map(result.Status),
            AcceptedSubmissionSequence = result.AcceptedSubmissionSequence,
            HasMore = result.HasMore
        };
        if (result.Session is not null)
        {
            response.Session = mapper.Map(result.Session);
        }
        response.DeliveredMessages.AddRange(
            result.DeliveredMessages.Select(item => mapper.Map(item)));
        return response;
    }

    public override async Task<MediaV1.GetMediaSessionStatusResponse>
        GetMediaSessionStatus(
            MediaV1.GetMediaSessionStatusRequest request,
            ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimeHostClientPrincipal principal = Authorize(
            context,
            RuntimeHostMediaAuthorizationRequirements.ForStatus);
        Validate(() => validator.ValidateSessionId(request.SessionId));
        RuntimeHostMediaOperationResult result = await owner.RenewLeaseAsync(
            principal.PrincipalId,
            request.SessionId,
            GetCancellationToken(context)).ConfigureAwait(false);
        var response = new MediaV1.GetMediaSessionStatusResponse
        {
            Status = mapper.Map(result.Status)
        };
        if (result.Session is not null)
        {
            response.Session = mapper.Map(result.Session);
        }
        return response;
    }

    public override async Task<MediaV1.StopMediaSessionResponse>
        StopMediaSession(
            MediaV1.StopMediaSessionRequest request,
            ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimeHostClientPrincipal principal = Authorize(
            context,
            RuntimeHostMediaAuthorizationRequirements.ForStop);
        Validate(() => validator.ValidateSessionId(request.SessionId));
        RuntimeHostMediaOperationResult result = await owner.StopAsync(
            principal.PrincipalId,
            request.SessionId,
            GetCancellationToken(context)).ConfigureAwait(false);
        var response = new MediaV1.StopMediaSessionResponse
        {
            Status = mapper.Map(result.Status)
        };
        if (result.Session is not null)
        {
            response.Session = mapper.Map(result.Session);
        }
        return response;
    }

    private RuntimeHostClientPrincipal Authorize(
        ServerCallContext? context,
        IReadOnlyList<RuntimeHostPermission> requirements)
    {
        RuntimeHostClientPrincipal principal =
            principalProvider.GetPrincipal(context) ??
            throw new InvalidOperationException(
                "The principal provider returned no principal.");
        RuntimeHostAuthorizationDecision decision =
            authorizationGate.Authorize(principal, requirements);
        if (!decision.IsAllowed)
        {
            throw new RpcException(new Status(
                StatusCode.PermissionDenied,
                "The authenticated client is not authorized for this media operation."));
        }
        return principal;
    }

    private static CancellationToken GetCancellationToken(
        ServerCallContext? context) => context?.CancellationToken ?? default;

    private static void Validate(Action validation)
    {
        try
        {
            validation();
        }
        catch (ArgumentException)
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                "The media-control request is invalid."));
        }
    }
}
