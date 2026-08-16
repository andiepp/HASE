using System.Security.Cryptography;
using System.Text;

namespace Hase.Runtime.Media;

/// <summary>
/// Owns the single process-local media session. It never discovers or selects
/// a device; only an exact locally configured source can be opened and only
/// one source can own the process-wide session at a time.
/// </summary>
public sealed class RuntimeHostMediaSessionOwner : IAsyncDisposable
{
    public const int MaximumIdentityUtf8Bytes = 128;
    public const int MaximumSessionDescriptionUtf8Bytes = 49_152;
    public const int MaximumIceCandidateUtf8Bytes = 4_096;
    public const int MaximumIceCandidatesPerPeer = 32;
    public const int MaximumNegotiationMessagesPerPeer = 36;
    public const int MaximumPendingDeliveryMessages = 16;
    public const int MaximumNegotiationExchanges = 128;

    public static readonly TimeSpan NegotiationIdleTimeout =
        TimeSpan.FromSeconds(15);
    public static readonly TimeSpan NegotiationLifetime =
        TimeSpan.FromSeconds(60);
    public static readonly TimeSpan SessionLeaseDuration =
        TimeSpan.FromSeconds(30);

    private readonly IReadOnlyList<RuntimeHostMediaSourceConfiguration> sources;
    private readonly IReadOnlyDictionary<string, RuntimeHostMediaSourceConfiguration>
        sourcesById;
    private readonly IRuntimeHostMediaCaptureBoundary boundary;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim gate = new(1, 1);
    private Session? activeSession;
    private bool disposed;

    public RuntimeHostMediaSessionOwner(
        RuntimeHostMediaSourceConfiguration source,
        IRuntimeHostMediaCaptureBoundary boundary,
        TimeProvider? timeProvider = null)
        : this([source], boundary, timeProvider)
    {
    }

    public RuntimeHostMediaSessionOwner(
        IReadOnlyList<RuntimeHostMediaSourceConfiguration> sources,
        IRuntimeHostMediaCaptureBoundary boundary,
        TimeProvider? timeProvider = null)
    {
        this.sources = ValidateSources(sources);
        sourcesById = this.sources.ToDictionary(
            item => item.Target.MediaSourceId,
            StringComparer.Ordinal);
        this.boundary = boundary ??
            throw new ArgumentNullException(nameof(boundary));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public IReadOnlyList<RuntimeHostMediaSourceConfiguration> Sources => sources;

    // Retained for callers built against the original single-source boundary.
    public RuntimeHostMediaSourceConfiguration Source => sources[0];

    public async ValueTask<RuntimeHostMediaOperationResult> StartAsync(
        RuntimeHostMediaStartRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (!IsValidIdentity(request.PrincipalId))
            {
                return RuntimeHostMediaOperationResult.Rejected(
                    RuntimeHostMediaOperationStatus.InvalidRequest);
            }

            if (!sourcesById.TryGetValue(
                    request.Target.MediaSourceId,
                    out var selectedSource) ||
                request.Target != selectedSource.Target)
            {
                return RuntimeHostMediaOperationResult.Rejected(
                    RuntimeHostMediaOperationStatus.SourceNotCurrent);
            }

            if (selectedSource.Availability != RuntimeHostMediaSourceAvailability.Idle)
            {
                return RuntimeHostMediaOperationResult.Rejected(
                    RuntimeHostMediaOperationStatus.SourceUnavailable);
            }

            if (request.IncludeAudio && !selectedSource.SupportsAudio)
            {
                return RuntimeHostMediaOperationResult.Rejected(
                    RuntimeHostMediaOperationStatus.AudioNotSupported);
            }

            if (activeSession is { IsTerminal: false })
            {
                return RuntimeHostMediaOperationResult.Rejected(
                    RuntimeHostMediaOperationStatus.SessionBusy);
            }

            var now = timeProvider.GetUtcNow();
            var session = new Session(
                CreateSessionId(),
                request.PrincipalId,
                request.IncludeAudio,
                selectedSource,
                now);
            activeSession = session;

            try
            {
                await boundary.OpenAsync(
                    selectedSource,
                    request.IncludeAudio,
                    cancellationToken).ConfigureAwait(false);
                session.Transition(
                    RuntimeHostMediaSessionState.Negotiating,
                    now,
                    RuntimeHostMediaTerminalReason.None);
                return Success(session);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                await TerminateLockedAsync(
                    session,
                    RuntimeHostMediaSessionState.Faulted,
                    RuntimeHostMediaTerminalReason.MediaBoundaryFailed)
                    .ConfigureAwait(false);
                throw;
            }
            catch
            {
                await TerminateLockedAsync(
                    session,
                    RuntimeHostMediaSessionState.Faulted,
                    RuntimeHostMediaTerminalReason.MediaBoundaryFailed)
                    .ConfigureAwait(false);
                return new(
                    RuntimeHostMediaOperationStatus.Faulted,
                    session.Snapshot());
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<RuntimeHostMediaOperationResult> ExchangeAsync(
        string principalId,
        string sessionId,
        RuntimeHostMediaNegotiationMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        RuntimeHostMediaNegotiationExchangeResult result =
            await ExchangeNegotiationAsync(
                principalId,
                sessionId,
                acknowledgedDeliverySequence: 0,
                message,
                cancellationToken).ConfigureAwait(false);
        return new(result.Status, result.Session);
    }

    public async ValueTask<RuntimeHostMediaNegotiationExchangeResult>
        ExchangeNegotiationAsync(
            string principalId,
            string sessionId,
            uint acknowledgedDeliverySequence,
            RuntimeHostMediaNegotiationMessage? submittedMessage,
            CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            var resolved = ResolveOwned(principalId, sessionId);
            if (resolved.Status != RuntimeHostMediaOperationStatus.Success)
            {
                return RuntimeHostMediaNegotiationExchangeResult.Rejected(
                    resolved.Status);
            }

            var session = resolved.Session!;
            var timeout = await EnforceTimeoutsLockedAsync(session)
                .ConfigureAwait(false);
            if (timeout is not null)
            {
                return RuntimeHostMediaNegotiationExchangeResult.Rejected(
                    timeout.Status,
                    timeout.Session);
            }

            if (session.State != RuntimeHostMediaSessionState.Negotiating)
            {
                return RuntimeHostMediaNegotiationExchangeResult.Rejected(
                    RuntimeHostMediaOperationStatus.InvalidState,
                    session.Snapshot());
            }

            if (session.ExchangeCount >= MaximumNegotiationExchanges)
            {
                await TerminateLockedAsync(
                    session,
                    RuntimeHostMediaSessionState.Faulted,
                    RuntimeHostMediaTerminalReason.ProtocolRejected)
                    .ConfigureAwait(false);
                return RuntimeHostMediaNegotiationExchangeResult.Rejected(
                    RuntimeHostMediaOperationStatus.LimitExceeded,
                    session.Snapshot());
            }

            RuntimeHostMediaOperationStatus acknowledgmentValidation =
                session.ValidateAcknowledgment(acknowledgedDeliverySequence);
            if (acknowledgmentValidation !=
                RuntimeHostMediaOperationStatus.Success)
            {
                return RuntimeHostMediaNegotiationExchangeResult.Rejected(
                    acknowledgmentValidation,
                    session.Snapshot());
            }

            if (submittedMessage is not null)
            {
                var validation = ValidateClientNegotiation(
                    session,
                    submittedMessage);
                if (validation != RuntimeHostMediaOperationStatus.Success)
                {
                    if (validation == RuntimeHostMediaOperationStatus.LimitExceeded)
                    {
                        await TerminateLockedAsync(
                            session,
                            RuntimeHostMediaSessionState.Faulted,
                            RuntimeHostMediaTerminalReason.ProtocolRejected)
                            .ConfigureAwait(false);
                    }

                    return RuntimeHostMediaNegotiationExchangeResult.Rejected(
                        validation,
                        session.Snapshot());
                }

                try
                {
                    await boundary.SubmitNegotiationAsync(
                        submittedMessage,
                        cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    await TerminateLockedAsync(
                        session,
                        RuntimeHostMediaSessionState.Faulted,
                        RuntimeHostMediaTerminalReason.MediaBoundaryFailed)
                        .ConfigureAwait(false);
                    throw;
                }
                catch
                {
                    await TerminateLockedAsync(
                        session,
                        RuntimeHostMediaSessionState.Faulted,
                        RuntimeHostMediaTerminalReason.MediaBoundaryFailed)
                        .ConfigureAwait(false);
                    return RuntimeHostMediaNegotiationExchangeResult.Rejected(
                        RuntimeHostMediaOperationStatus.Faulted,
                        session.Snapshot());
                }

                session.AcceptClient(submittedMessage, timeProvider.GetUtcNow());
            }

            session.Acknowledge(acknowledgedDeliverySequence);
            session.RecordExchange(timeProvider.GetUtcNow());
            IReadOnlyList<RuntimeHostMediaNegotiationMessage> deliveries =
                session.GetPendingDeliveries();
            return new(
                RuntimeHostMediaOperationStatus.Success,
                session.Snapshot(),
                session.AcceptedSubmissionSequence,
                deliveries,
                session.PendingDeliveryCount > deliveries.Count);
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<RuntimeHostMediaOperationResult>
        PublishNegotiationAsync(
            string sessionId,
            RuntimeHostMediaNegotiationMessage message,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            Session? session = ResolveSession(sessionId);
            if (session is null)
            {
                return RuntimeHostMediaOperationResult.Rejected(
                    RuntimeHostMediaOperationStatus.SessionNotFound);
            }

            if (session.State != RuntimeHostMediaSessionState.Negotiating)
            {
                return new(
                    RuntimeHostMediaOperationStatus.InvalidState,
                    session.Snapshot());
            }

            RuntimeHostMediaOperationStatus validation =
                ValidateHostNegotiation(session, message);
            if (validation != RuntimeHostMediaOperationStatus.Success)
            {
                if (validation == RuntimeHostMediaOperationStatus.LimitExceeded)
                {
                    await TerminateLockedAsync(
                        session,
                        RuntimeHostMediaSessionState.Faulted,
                        RuntimeHostMediaTerminalReason.ProtocolRejected)
                        .ConfigureAwait(false);
                }

                return new(validation, session.Snapshot());
            }

            session.Publish(message, timeProvider.GetUtcNow());
            return Success(session);
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<RuntimeHostMediaOperationResult> MarkStreamingAsync(
        string principalId,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            var resolved = ResolveOwned(principalId, sessionId);
            if (resolved.Status != RuntimeHostMediaOperationStatus.Success)
            {
                return RuntimeHostMediaOperationResult.Rejected(resolved.Status);
            }

            var session = resolved.Session!;
            if (session.State != RuntimeHostMediaSessionState.Negotiating)
            {
                return new(
                    RuntimeHostMediaOperationStatus.InvalidState,
                    session.Snapshot());
            }

            session.Transition(
                RuntimeHostMediaSessionState.Streaming,
                timeProvider.GetUtcNow(),
                RuntimeHostMediaTerminalReason.None);
            return Success(session);
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<RuntimeHostMediaOperationResult> GetStatusAsync(
        string principalId,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            var resolved = ResolveOwned(principalId, sessionId);
            if (resolved.Status != RuntimeHostMediaOperationStatus.Success)
            {
                return RuntimeHostMediaOperationResult.Rejected(resolved.Status);
            }

            var timeout = await EnforceTimeoutsLockedAsync(resolved.Session!)
                .ConfigureAwait(false);
            return timeout ?? Success(resolved.Session!);
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<RuntimeHostMediaOperationResult> RenewLeaseAsync(
        string principalId,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            var resolved = ResolveOwned(principalId, sessionId);
            if (resolved.Status != RuntimeHostMediaOperationStatus.Success)
            {
                return RuntimeHostMediaOperationResult.Rejected(resolved.Status);
            }

            var session = resolved.Session!;
            var timeout = await EnforceTimeoutsLockedAsync(session)
                .ConfigureAwait(false);
            if (timeout is not null)
            {
                return timeout;
            }

            session.LeaseExpiresAtUtc =
                timeProvider.GetUtcNow() + SessionLeaseDuration;
            return Success(session);
        }
        finally
        {
            gate.Release();
        }
    }

    public ValueTask<RuntimeHostMediaOperationResult> StopAsync(
        string principalId,
        string sessionId,
        CancellationToken cancellationToken = default) =>
        TerminateAsync(
            principalId,
            sessionId,
            RuntimeHostMediaTerminalReason.ClientStopped,
            cancellationToken);

    public ValueTask<RuntimeHostMediaOperationResult> ControlDisconnectedAsync(
        string principalId,
        string sessionId,
        CancellationToken cancellationToken = default) =>
        TerminateAsync(
            principalId,
            sessionId,
            RuntimeHostMediaTerminalReason.ControlDisconnected,
            cancellationToken);

    public ValueTask<RuntimeHostMediaOperationResult> SourceLostAsync(
        string principalId,
        string sessionId,
        CancellationToken cancellationToken = default) =>
        TerminateAsync(
            principalId,
            sessionId,
            RuntimeHostMediaTerminalReason.SourceLost,
            cancellationToken,
            RuntimeHostMediaSessionState.Faulted);

    public ValueTask<RuntimeHostMediaOperationResult> MediaBoundaryFailedAsync(
        string principalId,
        string sessionId,
        CancellationToken cancellationToken = default) =>
        TerminateAsync(
            principalId,
            sessionId,
            RuntimeHostMediaTerminalReason.MediaBoundaryFailed,
            cancellationToken,
            RuntimeHostMediaSessionState.Faulted);

    public async ValueTask StopForHostShutdownAsync(
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (activeSession is { IsTerminal: false } session)
            {
                await TerminateLockedAsync(
                    session,
                    RuntimeHostMediaSessionState.Ended,
                    RuntimeHostMediaTerminalReason.HostStopping)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (disposed)
            {
                return;
            }

            if (activeSession is { IsTerminal: false } session)
            {
                await TerminateLockedAsync(
                    session,
                    RuntimeHostMediaSessionState.Ended,
                    RuntimeHostMediaTerminalReason.HostStopping)
                    .ConfigureAwait(false);
            }

            disposed = true;
        }
        finally
        {
            gate.Release();
        }
    }

    private async ValueTask<RuntimeHostMediaOperationResult> TerminateAsync(
        string principalId,
        string sessionId,
        RuntimeHostMediaTerminalReason reason,
        CancellationToken cancellationToken,
        RuntimeHostMediaSessionState terminalState =
            RuntimeHostMediaSessionState.Ended)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            var resolved = ResolveOwned(principalId, sessionId);
            if (resolved.Status != RuntimeHostMediaOperationStatus.Success)
            {
                return RuntimeHostMediaOperationResult.Rejected(resolved.Status);
            }

            var session = resolved.Session!;
            if (!session.IsTerminal)
            {
                await TerminateLockedAsync(
                    session,
                    terminalState,
                    reason).ConfigureAwait(false);
            }

            return Success(session);
        }
        finally
        {
            gate.Release();
        }
    }

    private async ValueTask<RuntimeHostMediaOperationResult?>
        EnforceTimeoutsLockedAsync(Session session)
    {
        if (session.IsTerminal)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        if (now >= session.LeaseExpiresAtUtc)
        {
            await TerminateLockedAsync(
                session,
                RuntimeHostMediaSessionState.Ended,
                RuntimeHostMediaTerminalReason.LeaseExpired)
                .ConfigureAwait(false);
            return new(
                RuntimeHostMediaOperationStatus.TimedOut,
                session.Snapshot());
        }

        if (session.State == RuntimeHostMediaSessionState.Negotiating &&
            (now - session.StartedAtUtc >= NegotiationLifetime ||
             now - session.LastNegotiationAtUtc >= NegotiationIdleTimeout))
        {
            await TerminateLockedAsync(
                session,
                RuntimeHostMediaSessionState.Faulted,
                RuntimeHostMediaTerminalReason.NegotiationTimedOut)
                .ConfigureAwait(false);
            return new(
                RuntimeHostMediaOperationStatus.TimedOut,
                session.Snapshot());
        }

        return null;
    }

    private async ValueTask TerminateLockedAsync(
        Session session,
        RuntimeHostMediaSessionState terminalState,
        RuntimeHostMediaTerminalReason reason)
    {
        if (session.IsTerminal)
        {
            return;
        }

        session.Transition(
            RuntimeHostMediaSessionState.Stopping,
            timeProvider.GetUtcNow(),
            RuntimeHostMediaTerminalReason.None);
        try
        {
            await boundary.CloseAsync(CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch
        {
            terminalState = RuntimeHostMediaSessionState.Faulted;
            reason = RuntimeHostMediaTerminalReason.MediaBoundaryFailed;
        }

        session.Transition(
            terminalState,
            timeProvider.GetUtcNow(),
            reason);
    }

    private (RuntimeHostMediaOperationStatus Status, Session? Session)
        ResolveOwned(string principalId, string sessionId)
    {
        if (!IsValidIdentity(principalId) || !IsValidIdentity(sessionId))
        {
            return (RuntimeHostMediaOperationStatus.InvalidRequest, null);
        }

        if (activeSession is null ||
            !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(activeSession.Id),
                Encoding.UTF8.GetBytes(sessionId)))
        {
            return (RuntimeHostMediaOperationStatus.SessionNotFound, null);
        }

        if (!string.Equals(
                activeSession.PrincipalId,
                principalId,
                StringComparison.Ordinal))
        {
            return (RuntimeHostMediaOperationStatus.SessionNotOwned, null);
        }

        return (RuntimeHostMediaOperationStatus.Success, activeSession);
    }

    private static RuntimeHostMediaOperationStatus ValidateClientNegotiation(
        Session session,
        RuntimeHostMediaNegotiationMessage message)
    {
        if (message.Sequence != session.NextSubmissionSequence)
        {
            return RuntimeHostMediaOperationStatus.InvalidRequest;
        }

        if (session.ClientMessageCount >= MaximumNegotiationMessagesPerPeer ||
            (message.Kind == RuntimeHostMediaNegotiationKind.IceCandidate &&
             session.ClientIceCandidateCount >= MaximumIceCandidatesPerPeer))
        {
            return RuntimeHostMediaOperationStatus.LimitExceeded;
        }

        if (message.Kind == RuntimeHostMediaNegotiationKind.Offer ||
            (message.Kind == RuntimeHostMediaNegotiationKind.Answer &&
             (!session.HostOfferPublished || session.ClientAnswerAccepted)) ||
            ((message.Kind is RuntimeHostMediaNegotiationKind.IceCandidate or
                RuntimeHostMediaNegotiationKind.IceComplete) &&
             !session.HostOfferPublished))
        {
            return RuntimeHostMediaOperationStatus.InvalidRequest;
        }

        return ValidateNegotiationPayload(message);
    }

    private static RuntimeHostMediaOperationStatus ValidateHostNegotiation(
        Session session,
        RuntimeHostMediaNegotiationMessage message)
    {
        if (message.Sequence != session.NextDeliverySequence)
        {
            return RuntimeHostMediaOperationStatus.InvalidRequest;
        }

        if (session.PendingDeliveryCount >= MaximumPendingDeliveryMessages ||
            session.HostMessageCount >= MaximumNegotiationMessagesPerPeer ||
            (message.Kind == RuntimeHostMediaNegotiationKind.IceCandidate &&
             session.HostIceCandidateCount >= MaximumIceCandidatesPerPeer))
        {
            return RuntimeHostMediaOperationStatus.LimitExceeded;
        }

        if (message.Kind == RuntimeHostMediaNegotiationKind.Answer ||
            (message.Kind == RuntimeHostMediaNegotiationKind.Offer &&
             session.HostOfferPublished) ||
            ((message.Kind is RuntimeHostMediaNegotiationKind.IceCandidate or
                RuntimeHostMediaNegotiationKind.IceComplete) &&
             !session.HostOfferPublished))
        {
            return RuntimeHostMediaOperationStatus.InvalidRequest;
        }

        return ValidateNegotiationPayload(message);
    }

    private static RuntimeHostMediaOperationStatus ValidateNegotiationPayload(
        RuntimeHostMediaNegotiationMessage message)
    {

        var bytes = Encoding.UTF8.GetByteCount(message.SensitivePayload ?? "");
        return message.Kind switch
        {
            RuntimeHostMediaNegotiationKind.Offer or
            RuntimeHostMediaNegotiationKind.Answer
                when bytes is > 0 and <= MaximumSessionDescriptionUtf8Bytes =>
                    RuntimeHostMediaOperationStatus.Success,
            RuntimeHostMediaNegotiationKind.IceCandidate
                when bytes is > 0 and <= MaximumIceCandidateUtf8Bytes =>
                    RuntimeHostMediaOperationStatus.Success,
            RuntimeHostMediaNegotiationKind.IceComplete
                when bytes == 0 => RuntimeHostMediaOperationStatus.Success,
            _ => RuntimeHostMediaOperationStatus.InvalidRequest
        };
    }

    private Session? ResolveSession(string sessionId)
    {
        if (!IsValidIdentity(sessionId) || activeSession is null)
        {
            return null;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(activeSession.Id),
            Encoding.UTF8.GetBytes(sessionId))
            ? activeSession
            : null;
    }

    private static IReadOnlyList<RuntimeHostMediaSourceConfiguration>
        ValidateSources(IReadOnlyList<RuntimeHostMediaSourceConfiguration> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
        {
            throw new ArgumentException(
                "At least one configured media source is required.",
                nameof(values));
        }

        var validated = new List<RuntimeHostMediaSourceConfiguration>(values.Count);
        var sourceIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            ArgumentNullException.ThrowIfNull(value);
            ArgumentNullException.ThrowIfNull(value.Target);
            if (!IsValidIdentity(value.Target.MediaSourceId) ||
                !IsValidIdentity(value.Target.MediaSourceGeneration) ||
                !IsValidIdentity(value.DisplayName) ||
                string.IsNullOrWhiteSpace(value.VideoDeviceId) ||
                (value.AudioDeviceId is not null &&
                 string.IsNullOrWhiteSpace(value.AudioDeviceId)) ||
                !sourceIds.Add(value.Target.MediaSourceId))
            {
                throw new ArgumentException(
                    "The configured media source inventory is invalid.",
                    nameof(values));
            }

            validated.Add(value);
        }

        return validated.AsReadOnly();
    }

    private static bool IsValidIdentity(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        Encoding.UTF8.GetByteCount(value) <= MaximumIdentityUtf8Bytes;

    private static string CreateSessionId()
    {
        Span<byte> bytes = stackalloc byte[24];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes);
    }

    private RuntimeHostMediaOperationResult Success(Session session) =>
        new(
            RuntimeHostMediaOperationStatus.Success,
            session.Snapshot());

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    private sealed class Session
    {
        public Session(
            string id,
            string principalId,
            bool audioRequested,
            RuntimeHostMediaSourceConfiguration source,
            DateTimeOffset now)
        {
            Id = id;
            PrincipalId = principalId;
            AudioRequested = audioRequested;
            Source = source;
            State = RuntimeHostMediaSessionState.Starting;
            StartedAtUtc = now;
            LastTransitionAtUtc = now;
            LastNegotiationAtUtc = now;
            LeaseExpiresAtUtc = now + SessionLeaseDuration;
        }

        public string Id { get; }
        public string PrincipalId { get; }
        public bool AudioRequested { get; }
        public RuntimeHostMediaSourceConfiguration Source { get; }
        public RuntimeHostMediaSessionState State { get; private set; }
        public DateTimeOffset StartedAtUtc { get; }
        public DateTimeOffset LastTransitionAtUtc { get; private set; }
        public DateTimeOffset LastNegotiationAtUtc { get; private set; }
        public DateTimeOffset LeaseExpiresAtUtc { get; set; }
        public RuntimeHostMediaTerminalReason TerminalReason { get; private set; }
        private readonly List<RuntimeHostMediaNegotiationMessage>
            pendingDeliveries = [];
        public uint NextSubmissionSequence { get; private set; } = 1;
        public uint NextDeliverySequence { get; private set; } = 1;
        public uint AcceptedSubmissionSequence { get; private set; }
        public uint AcknowledgedDeliverySequence { get; private set; }
        public int ClientMessageCount { get; private set; }
        public int HostMessageCount { get; private set; }
        public int ClientIceCandidateCount { get; private set; }
        public int HostIceCandidateCount { get; private set; }
        public int ExchangeCount { get; private set; }
        public bool HostOfferPublished { get; private set; }
        public bool ClientAnswerAccepted { get; private set; }
        public int PendingDeliveryCount => pendingDeliveries.Count;
        public bool IsTerminal =>
            State is RuntimeHostMediaSessionState.Ended or
                RuntimeHostMediaSessionState.Faulted;

        public void AcceptClient(
            RuntimeHostMediaNegotiationMessage message,
            DateTimeOffset now)
        {
            AcceptedSubmissionSequence = message.Sequence;
            NextSubmissionSequence++;
            ClientMessageCount++;
            if (message.Kind == RuntimeHostMediaNegotiationKind.IceCandidate)
            {
                ClientIceCandidateCount++;
            }
            if (message.Kind == RuntimeHostMediaNegotiationKind.Answer)
            {
                ClientAnswerAccepted = true;
            }

            LastNegotiationAtUtc = now;
        }

        public void Publish(
            RuntimeHostMediaNegotiationMessage message,
            DateTimeOffset now)
        {
            pendingDeliveries.Add(message);
            NextDeliverySequence++;
            HostMessageCount++;
            if (message.Kind == RuntimeHostMediaNegotiationKind.IceCandidate)
            {
                HostIceCandidateCount++;
            }
            if (message.Kind == RuntimeHostMediaNegotiationKind.Offer)
            {
                HostOfferPublished = true;
            }

            LastNegotiationAtUtc = now;
        }

        public RuntimeHostMediaOperationStatus ValidateAcknowledgment(
            uint sequence)
        {
            uint lastPublished = NextDeliverySequence - 1;
            if (sequence < AcknowledgedDeliverySequence ||
                sequence > lastPublished)
            {
                return RuntimeHostMediaOperationStatus.InvalidRequest;
            }

            return RuntimeHostMediaOperationStatus.Success;
        }

        public void Acknowledge(uint sequence)
        {
            AcknowledgedDeliverySequence = sequence;
            pendingDeliveries.RemoveAll(item => item.Sequence <= sequence);
        }

        public IReadOnlyList<RuntimeHostMediaNegotiationMessage>
            GetPendingDeliveries() =>
            pendingDeliveries
                .Take(MaximumPendingDeliveryMessages)
                .ToArray();

        public void RecordExchange(DateTimeOffset now)
        {
            ExchangeCount++;
            LeaseExpiresAtUtc = now + SessionLeaseDuration;
        }

        public void Transition(
            RuntimeHostMediaSessionState state,
            DateTimeOffset now,
            RuntimeHostMediaTerminalReason reason)
        {
            State = state;
            LastTransitionAtUtc = now;
            TerminalReason = reason;
        }

        public RuntimeHostMediaSessionSnapshot Snapshot() =>
            new(
                Id,
                Source.Target,
                AudioRequested,
                State,
                StartedAtUtc,
                LastTransitionAtUtc,
                LeaseExpiresAtUtc,
                TerminalReason);
    }
}
