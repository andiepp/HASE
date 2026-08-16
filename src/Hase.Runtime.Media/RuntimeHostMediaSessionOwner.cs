using System.Security.Cryptography;
using System.Text;

namespace Hase.Runtime.Media;

/// <summary>
/// Owns the single process-local media session. It never discovers or selects
/// a device; only the exact locally configured source can be opened.
/// </summary>
public sealed class RuntimeHostMediaSessionOwner : IAsyncDisposable
{
    public const int MaximumIdentityUtf8Bytes = 128;
    public const int MaximumSessionDescriptionUtf8Bytes = 49_152;
    public const int MaximumIceCandidateUtf8Bytes = 4_096;
    public const int MaximumIceCandidatesPerPeer = 32;
    public const int MaximumNegotiationMessagesPerPeer = 36;
    public const int MaximumNegotiationExchanges = 128;

    public static readonly TimeSpan NegotiationIdleTimeout =
        TimeSpan.FromSeconds(15);
    public static readonly TimeSpan NegotiationLifetime =
        TimeSpan.FromSeconds(60);
    public static readonly TimeSpan SessionLeaseDuration =
        TimeSpan.FromSeconds(30);

    private readonly RuntimeHostMediaSourceConfiguration source;
    private readonly IRuntimeHostMediaCaptureBoundary boundary;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim gate = new(1, 1);
    private Session? activeSession;
    private bool disposed;

    public RuntimeHostMediaSessionOwner(
        RuntimeHostMediaSourceConfiguration source,
        IRuntimeHostMediaCaptureBoundary boundary,
        TimeProvider? timeProvider = null)
    {
        this.source = ValidateSource(source);
        this.boundary = boundary ??
            throw new ArgumentNullException(nameof(boundary));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public RuntimeHostMediaSourceConfiguration Source => source;

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

            if (request.Target != source.Target)
            {
                return RuntimeHostMediaOperationResult.Rejected(
                    RuntimeHostMediaOperationStatus.SourceNotCurrent);
            }

            if (source.Availability != RuntimeHostMediaSourceAvailability.Idle)
            {
                return RuntimeHostMediaOperationResult.Rejected(
                    RuntimeHostMediaOperationStatus.SourceUnavailable);
            }

            if (request.IncludeAudio && !source.SupportsAudio)
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
                now);
            activeSession = session;

            try
            {
                await boundary.OpenAsync(
                    source,
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
                    session.Snapshot(source.Target));
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

            if (session.State != RuntimeHostMediaSessionState.Negotiating)
            {
                return new(
                    RuntimeHostMediaOperationStatus.InvalidState,
                    session.Snapshot(source.Target));
            }

            var validation = ValidateNegotiation(session, message);
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

                return new(validation, session.Snapshot(source.Target));
            }

            try
            {
                await boundary.SubmitNegotiationAsync(
                    message,
                    cancellationToken).ConfigureAwait(false);
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
                    session.Snapshot(source.Target));
            }

            session.Accept(message, timeProvider.GetUtcNow());
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
                    session.Snapshot(source.Target));
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
                session.Snapshot(source.Target));
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
                session.Snapshot(source.Target));
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

    private static RuntimeHostMediaOperationStatus ValidateNegotiation(
        Session session,
        RuntimeHostMediaNegotiationMessage message)
    {
        if (message.Sequence != session.NextSequence)
        {
            return RuntimeHostMediaOperationStatus.InvalidRequest;
        }

        if (session.ExchangeCount >= MaximumNegotiationExchanges ||
            session.MessageCount >= MaximumNegotiationMessagesPerPeer ||
            (message.Kind == RuntimeHostMediaNegotiationKind.IceCandidate &&
             session.IceCandidateCount >= MaximumIceCandidatesPerPeer))
        {
            return RuntimeHostMediaOperationStatus.LimitExceeded;
        }

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

    private static RuntimeHostMediaSourceConfiguration ValidateSource(
        RuntimeHostMediaSourceConfiguration value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(value.Target);
        if (!IsValidIdentity(value.Target.MediaSourceId) ||
            !IsValidIdentity(value.Target.MediaSourceGeneration) ||
            string.IsNullOrWhiteSpace(value.VideoDeviceId) ||
            (value.AudioDeviceId is not null &&
             string.IsNullOrWhiteSpace(value.AudioDeviceId)))
        {
            throw new ArgumentException(
                "The configured media source is invalid.",
                nameof(value));
        }

        return value;
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
            session.Snapshot(source.Target));

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
            DateTimeOffset now)
        {
            Id = id;
            PrincipalId = principalId;
            AudioRequested = audioRequested;
            State = RuntimeHostMediaSessionState.Starting;
            StartedAtUtc = now;
            LastTransitionAtUtc = now;
            LastNegotiationAtUtc = now;
            LeaseExpiresAtUtc = now + SessionLeaseDuration;
        }

        public string Id { get; }
        public string PrincipalId { get; }
        public bool AudioRequested { get; }
        public RuntimeHostMediaSessionState State { get; private set; }
        public DateTimeOffset StartedAtUtc { get; }
        public DateTimeOffset LastTransitionAtUtc { get; private set; }
        public DateTimeOffset LastNegotiationAtUtc { get; private set; }
        public DateTimeOffset LeaseExpiresAtUtc { get; set; }
        public RuntimeHostMediaTerminalReason TerminalReason { get; private set; }
        public uint NextSequence { get; private set; } = 1;
        public int MessageCount { get; private set; }
        public int IceCandidateCount { get; private set; }
        public int ExchangeCount { get; private set; }
        public bool IsTerminal =>
            State is RuntimeHostMediaSessionState.Ended or
                RuntimeHostMediaSessionState.Faulted;

        public void Accept(
            RuntimeHostMediaNegotiationMessage message,
            DateTimeOffset now)
        {
            NextSequence++;
            MessageCount++;
            ExchangeCount++;
            if (message.Kind == RuntimeHostMediaNegotiationKind.IceCandidate)
            {
                IceCandidateCount++;
            }

            LastNegotiationAtUtc = now;
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

        public RuntimeHostMediaSessionSnapshot Snapshot(
            RuntimeHostMediaSourceTarget target) =>
            new(
                Id,
                target,
                AudioRequested,
                State,
                StartedAtUtc,
                LastTransitionAtUtc,
                LeaseExpiresAtUtc,
                TerminalReason);
    }
}
