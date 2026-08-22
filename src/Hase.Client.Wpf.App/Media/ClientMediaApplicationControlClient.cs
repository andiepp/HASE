using System.Threading.Channels;
using System.Runtime.CompilerServices;
using Hase.Client.Configuration;
using Hase.Client.Diagnostics;
using Hase.Client.Media;

namespace Hase.Client.Wpf.AppHost.Media;

/// <summary>
/// Owns one explicitly selected, non-resumable Client media session and its
/// bounded signaling pump. Ordinary Runtime Host recovery never replays Start.
/// </summary>
public sealed class ClientMediaApplicationControlClient :
    IRuntimeHostMediaControlClient,
    IRuntimeHostMediaCapabilityWatchClient,
    IRuntimeHostMediaSessionNotifications,
    IAsyncDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan StatusInterval = TimeSpan.FromSeconds(5);

    private readonly Func<RuntimeHostProfileId, IRuntimeHostMediaControlClient>
        clientResolver;
    private readonly IClientMediaPresentationBoundary boundary;
    private readonly SynchronizationContext uiContext;
    private readonly ClientDiagnosticPublisher diagnostics;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Channel<RemoteMediaNegotiationMessage> submissions =
        Channel.CreateBounded<RemoteMediaNegotiationMessage>(64);
    private IRuntimeHostMediaControlClient? selectedClient;
    private RuntimeHostProfileId? selectedProfileId;
    private RemoteMediaSessionSnapshot? session;
    private CancellationTokenSource? sessionCancellation;
    private Task? pump;
    private volatile bool peerConnected;
    private bool disposed;

    public ClientMediaApplicationControlClient(
        Func<RuntimeHostProfileId, IRuntimeHostMediaControlClient> clientResolver,
        IClientMediaPresentationBoundary boundary,
        SynchronizationContext? uiContext = null,
        ClientDiagnosticPublisher? diagnostics = null)
    {
        this.clientResolver = clientResolver ??
            throw new ArgumentNullException(nameof(clientResolver));
        this.boundary = boundary ?? throw new ArgumentNullException(nameof(boundary));
        this.uiContext = uiContext ?? SynchronizationContext.Current
            ?? throw new InvalidOperationException(
                "Client media composition requires the WPF synchronization context.");
        this.diagnostics = diagnostics ?? new ClientDiagnosticPublisher();
        boundary.ValidatedMessage += OnValidatedMessage;
    }

    public event EventHandler<RemoteMediaSessionChangedEventArgs>? SessionChanged;

    public void SelectRuntimeHost(RuntimeHostProfileId? profileId)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (profileId == selectedProfileId)
        {
            return;
        }

        StopRemoteAndCancelLocalSession(
            "Media stopped because the selected Runtime Host changed.");
        selectedProfileId = profileId;
        selectedClient = profileId is null
            ? null
            : clientResolver(profileId);
    }

    public void NotifyRuntimeHostState(
        RuntimeHostProfileId profileId,
        RuntimeHostClientSessionState state)
    {
        ArgumentNullException.ThrowIfNull(profileId);
        if (profileId == selectedProfileId &&
            state != RuntimeHostClientSessionState.Connected &&
            session is not null)
        {
            StopRemoteAndCancelLocalSession(
                "Media stopped because the Runtime Host control session disconnected.");
        }
    }

    public async Task<IReadOnlyList<RemoteMediaSourceCapability>> GetCapabilitiesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetSelectedClient()
                .GetCapabilitiesAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (RuntimeHostClientException exception)
        {
            diagnostics.Publish(
                ClientDiagnosticLevel.Operational,
                () => new ClientDiagnosticEvent(
                    ClientDiagnosticLevel.Operational,
                    ClientDiagnosticCategory.ClientPresentation,
                    "MediaCapabilitiesRefreshFailed",
                    ClientDiagnosticSeverity.Warning,
                    outcome: ClientDiagnosticOutcome.Failed,
                    metadata: new Dictionary<string, string>
                    {
                        ["failureCategory"] = exception.Category.ToString(),
                        ["safeMessage"] = exception.Message
                    }));
            throw;
        }
    }

    public async IAsyncEnumerable<RemoteMediaCapabilitySnapshot>
        WatchCapabilitiesAsync(
            ulong afterRevision = 0,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        IRuntimeHostMediaControlClient activeClient = GetSelectedClient();
        if (activeClient is IRuntimeHostMediaCapabilityWatchClient watchClient)
        {
            await foreach (RemoteMediaCapabilitySnapshot snapshot in
                watchClient.WatchCapabilitiesAsync(
                    afterRevision,
                    cancellationToken).ConfigureAwait(false))
            {
                yield return snapshot;
            }
            yield break;
        }

        IReadOnlyList<RemoteMediaSourceCapability> sources =
            await activeClient.GetCapabilitiesAsync(cancellationToken)
                .ConfigureAwait(false);
        yield return new(afterRevision + 1, sources);
    }

    public async Task<RemoteMediaStartResult> StartAsync(
        RemoteMediaSourceTarget target,
        bool includeAudio,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (session is not null)
            {
                throw new InvalidOperationException("A Client media session is already active.");
            }

            IRuntimeHostMediaControlClient client = GetSelectedClient();
            RemoteMediaStartResult result = await client.StartAsync(
                target, includeAudio, cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded || result.Session is null)
            {
                return result;
            }

            try
            {
                await boundary.BeginAsync(includeAudio, cancellationToken)
                    .ConfigureAwait(false);
                session = result.Session;
                peerConnected = false;
                sessionCancellation = new CancellationTokenSource();
                pump = PumpAsync(client, result.Session.SessionId,
                    sessionCancellation.Token);
                Publish(result.Session, "Encrypted media negotiation started.");
                return result;
            }
            catch
            {
                _ = await TryStopRemoteAsync(client, result.Session.SessionId)
                    .ConfigureAwait(false);
                boundary.ClearPresentation();
                throw;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public Task<RemoteMediaExchangeResult> ExchangeAsync(
        string sessionId,
        uint acknowledgedDeliverySequence,
        RemoteMediaNegotiationMessage? submittedMessage,
        CancellationToken cancellationToken = default) =>
        GetSelectedClient().ExchangeAsync(sessionId,
            acknowledgedDeliverySequence, submittedMessage, cancellationToken);

    public Task<RemoteMediaStatusResult> GetStatusAsync(
        string sessionId,
        CancellationToken cancellationToken = default) =>
        GetSelectedClient().GetStatusAsync(sessionId, cancellationToken);

    public async Task<RemoteMediaStopResult> StopAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        IRuntimeHostMediaControlClient client = GetSelectedClient();
        CancellationTokenSource? cancellation = sessionCancellation;
        Task? activePump = pump;
        cancellation?.Cancel();
        if (activePump is not null)
        {
            try
            {
                await activePump.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellation?.IsCancellationRequested == true)
            {
            }
        }

        RemoteMediaStopResult result = await client.StopAsync(
            sessionId, cancellationToken).ConfigureAwait(false);
        ClearSession("Media session stopped.");
        return result;
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        boundary.ValidatedMessage -= OnValidatedMessage;
        RemoteMediaSessionSnapshot? active = session;
        IRuntimeHostMediaControlClient? client = selectedClient;
        CancelLocalSession("Media session ended during Client shutdown.");
        // Dispose the boundary before the remote stop. DisposeAsync is
        // called from OnExit, which blocks the UI thread on this task; the
        // remote stop resumes on the thread pool, from where the boundary
        // disposal would have to marshal back to the blocked UI thread and
        // deadlock the shutdown. On the UI thread it runs synchronously.
        await boundary.DisposeAsync().ConfigureAwait(false);
        if (active is not null && client is not null)
        {
            _ = await TryStopRemoteAsync(client, active.SessionId)
                .ConfigureAwait(false);
        }
        gate.Dispose();
    }

    private async Task PumpAsync(
        IRuntimeHostMediaControlClient client,
        string sessionId,
        CancellationToken cancellationToken)
    {
        uint acknowledgedSequence = 0;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (peerConnected)
                {
                    RemoteMediaStatusResult status = await client.GetStatusAsync(
                        sessionId, cancellationToken).ConfigureAwait(false);
                    if (!status.Succeeded || status.Session is null ||
                        status.Session.State is RemoteMediaSessionState.Ended or
                            RemoteMediaSessionState.Faulted)
                    {
                        ClearSessionIfCurrent(
                            sessionId,
                            status.Session?.TerminalReason ==
                                RemoteMediaTerminalReason.SourceLost
                                ? "Media stopped because the camera was disconnected."
                                : "The remote media session ended.",
                            status.Session?.TerminalReason ??
                                RemoteMediaTerminalReason.None);
                        return;
                    }
                    session = status.Session;
                    Publish(status.Session, "Encrypted live media is streaming.");
                    await Task.Delay(StatusInterval, cancellationToken)
                        .ConfigureAwait(false);
                    continue;
                }

                submissions.Reader.TryRead(out RemoteMediaNegotiationMessage? submission);
                RemoteMediaExchangeResult exchange = await client.ExchangeAsync(
                    sessionId, acknowledgedSequence, submission, cancellationToken)
                    .ConfigureAwait(false);
                if (!exchange.Succeeded || exchange.Session is null)
                {
                    await FailNegotiationAndStopRemoteAsync(
                        client,
                        sessionId,
                        "Media negotiation failed.",
                        exchange.FailureCode).ConfigureAwait(false);
                    return;
                }

                foreach (RemoteMediaNegotiationMessage delivery in exchange.DeliveredMessages)
                {
                    boundary.SubmitNegotiation(delivery);
                    acknowledgedSequence = delivery.Sequence;
                }
                session = exchange.Session;
                Publish(exchange.Session, "Encrypted media negotiation is active.");
                await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await FailNegotiationAndStopRemoteAsync(
                client,
                sessionId,
                "The media control connection failed.",
                FailureCategory(exception)).ConfigureAwait(false);
        }
    }

    private void OnValidatedMessage(ClientMediaWebMessage message)
    {
        if (message.Kind == ClientMediaWebMessageKind.Negotiation &&
            message.NegotiationMessage is not null)
        {
            if (!submissions.Writer.TryWrite(message.NegotiationMessage))
            {
                StopRemoteAndCancelLocalSession(
                    "Media negotiation exceeded its local bound.");
            }
            return;
        }
        if (message.Kind == ClientMediaWebMessageKind.PeerConnected)
        {
            peerConnected = true;
            return;
        }
        if (message.Kind == ClientMediaWebMessageKind.AudioActivationBlocked)
        {
            diagnostics.Publish(
                ClientDiagnosticLevel.Operational,
                () => new ClientDiagnosticEvent(
                    ClientDiagnosticLevel.Operational,
                    ClientDiagnosticCategory.ClientPresentation,
                    "MediaAudioActivationBlocked",
                    ClientDiagnosticSeverity.Warning,
                    outcome: ClientDiagnosticOutcome.Failed,
                    metadata: new Dictionary<string, string>
                    {
                        ["failureCategory"] = "playback-blocked"
                    }));
            return;
        }
        if (message.Kind == ClientMediaWebMessageKind.PresentationFaulted)
        {
            diagnostics.Publish(
                ClientDiagnosticLevel.Operational,
                () => new ClientDiagnosticEvent(
                    ClientDiagnosticLevel.Operational,
                    ClientDiagnosticCategory.ClientPresentation,
                    "MediaBoundaryFaulted",
                    ClientDiagnosticSeverity.Warning,
                    outcome: ClientDiagnosticOutcome.Failed,
                    metadata: new Dictionary<string, string>
                    {
                        ["failureCategory"] = message.FailureCode ?? "unspecified"
                    }));
            StopRemoteAndCancelLocalSession(
                "The media presentation boundary failed.");
        }
    }

    private IRuntimeHostMediaControlClient GetSelectedClient() =>
        selectedClient ?? throw new InvalidOperationException(
            "A connected Runtime Host must be selected for media control.");

    private void CancelLocalSession(
        string statusText,
        RemoteMediaTerminalReason terminalReason =
            RemoteMediaTerminalReason.None)
    {
        sessionCancellation?.Cancel();
        sessionCancellation?.Dispose();
        sessionCancellation = null;
        pump = null;
        session = null;
        peerConnected = false;
        while (submissions.Reader.TryRead(out _))
        {
        }
        boundary.ClearPresentation();
        Publish(null, statusText, terminalReason);
    }

    private void ClearSession(
        string statusText,
        RemoteMediaTerminalReason terminalReason =
            RemoteMediaTerminalReason.None) =>
        CancelLocalSession(statusText, terminalReason);

    private void ClearSessionIfCurrent(
        string sessionId,
        string statusText,
        RemoteMediaTerminalReason terminalReason =
            RemoteMediaTerminalReason.None)
    {
        if (session?.SessionId == sessionId)
        {
            ClearSession(statusText, terminalReason);
        }
    }

    private void StopRemoteAndCancelLocalSession(string statusText)
    {
        RemoteMediaSessionSnapshot? activeSession = session;
        IRuntimeHostMediaControlClient? activeClient = selectedClient;
        CancelLocalSession(statusText);
        if (activeSession is not null && activeClient is not null)
        {
            _ = TryStopRemoteAsync(activeClient, activeSession.SessionId);
        }
    }

    private async Task FailNegotiationAndStopRemoteAsync(
        IRuntimeHostMediaControlClient client,
        string sessionId,
        string statusText,
        string? failureCategory)
    {
        PublishFailureDiagnostic(
            "MediaNegotiationExchangeFailed",
            failureCategory);

        string? cleanupFailure = await TryStopRemoteAsync(client, sessionId)
            .ConfigureAwait(false);
        if (cleanupFailure is not null)
        {
            PublishFailureDiagnostic(
                "MediaSessionCleanupFailed",
                cleanupFailure);
        }

        ClearSessionIfCurrent(sessionId, statusText);
    }

    private async Task<string?> TryStopRemoteAsync(
        IRuntimeHostMediaControlClient client,
        string sessionId)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try
        {
            RemoteMediaStopResult result = await client.StopAsync(
                sessionId, timeout.Token).ConfigureAwait(false);
            return result.Succeeded
                ? null
                : NormalizeFailureCategory(result.FailureCode);
        }
        catch (RuntimeHostClientException exception)
        {
            return NormalizeFailureCategory(exception.Category.ToString());
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            return "timed-out";
        }
        catch
        {
            return "client-failure";
        }
    }

    private void PublishFailureDiagnostic(
        string eventName,
        string? failureCategory)
    {
        diagnostics.Publish(
            ClientDiagnosticLevel.Operational,
            () => new ClientDiagnosticEvent(
                ClientDiagnosticLevel.Operational,
                ClientDiagnosticCategory.ClientPresentation,
                eventName,
                ClientDiagnosticSeverity.Warning,
                outcome: ClientDiagnosticOutcome.Failed,
                metadata: new Dictionary<string, string>
                {
                    ["failureCategory"] =
                        NormalizeFailureCategory(failureCategory)
                }));
    }

    private static string FailureCategory(Exception exception) =>
        exception is RuntimeHostClientException clientException
            ? NormalizeFailureCategory(clientException.Category.ToString())
            : "client-failure";

    private static string NormalizeFailureCategory(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 64)
        {
            return "unspecified";
        }

        foreach (char character in value)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character != '-')
            {
                return "unspecified";
            }
        }

        return value;
    }

    private void Publish(
        RemoteMediaSessionSnapshot? value,
        string statusText,
        RemoteMediaTerminalReason terminalReason =
            RemoteMediaTerminalReason.None)
    {
        uiContext.Post(_ => SessionChanged?.Invoke(this,
            new RemoteMediaSessionChangedEventArgs(
                value,
                statusText,
                terminalReason)), null);
    }
}
