using Hase.Runtime.Diagnostics;
using Hase.Transport;

namespace Hase.Runtime.Transport;

/// <summary>
/// Owns the runtime protocol connection associated with one transport
/// connection.
/// </summary>
/// <remarks>
/// Legacy transports require only a protocol adapter. Duplex transports also
/// require a protocol session, a receive pump, and a lifetime cancellation
/// source.
///
/// The binding owns protocol-session lifetime but does not own or dispose the
/// underlying transport connection.
/// </remarks>
internal sealed class RuntimeProtocolConnectionBinding
    : IAsyncDisposable
{
    private readonly CancellationTokenSource?
        _receivePumpCancellationSource;

    private readonly Task _receivePumpCompletion;
    private readonly ITransportByteTraceSource? _byteTraceSource;
    private readonly ITransportByteTraceObserver? _byteTraceObserver;

    private int _disposed;

    private RuntimeProtocolConnectionBinding(
        ITransportConnection transportConnection,
        IRuntimeProtocolConnection protocolConnection,
        ProtocolDuplexSession? duplexSession,
        CancellationTokenSource? receivePumpCancellationSource,
        Task receivePumpCompletion,
        ITransportByteTraceSource? byteTraceSource = null,
        ITransportByteTraceObserver? byteTraceObserver = null)
    {
        TransportConnection =
            transportConnection
            ?? throw new ArgumentNullException(
                nameof(transportConnection));

        ProtocolConnection =
            protocolConnection
            ?? throw new ArgumentNullException(
                nameof(protocolConnection));

        DuplexSession =
            duplexSession;

        _receivePumpCancellationSource =
            receivePumpCancellationSource;

        _receivePumpCompletion =
            receivePumpCompletion
            ?? throw new ArgumentNullException(
                nameof(receivePumpCompletion));

        _byteTraceSource =
            byteTraceSource;

        _byteTraceObserver =
            byteTraceObserver;
    }

    /// <summary>
    /// Gets the underlying transport connection.
    /// </summary>
    public ITransportConnection TransportConnection
    {
        get;
    }

    /// <summary>
    /// Gets the protocol connection used by runtime operations.
    /// </summary>
    public IRuntimeProtocolConnection ProtocolConnection
    {
        get;
    }

    /// <summary>
    /// Gets the duplex protocol session, or <see langword="null"/> for a
    /// legacy transport.
    /// </summary>
    public ProtocolDuplexSession? DuplexSession
    {
        get;
    }

    /// <summary>
    /// Gets the receive-pump completion task.
    /// </summary>
    internal Task ReceivePumpCompletion =>
        _receivePumpCompletion;

    /// <summary>
    /// Creates and starts the protocol binding for a transport connection.
    /// </summary>
    public static RuntimeProtocolConnectionBinding Create(
        ITransportConnection transportConnection)
    {
        return CreateCore(
            transportConnection,
            endpointId: null,
            diagnostics: null);
    }

    /// <summary>
    /// Creates and starts a diagnostically decorated protocol binding for one
    /// production endpoint generation.
    /// </summary>
    internal static RuntimeProtocolConnectionBinding Create(
        ITransportConnection transportConnection,
        string endpointId,
        RuntimeDiagnosticPublisher diagnostics)
    {
        ArgumentNullException.ThrowIfNull(
            diagnostics);

        if (string.IsNullOrWhiteSpace(
                endpointId))
        {
            throw new ArgumentException(
                "Endpoint identity must not be empty.",
                nameof(endpointId));
        }

        return CreateCore(
            transportConnection,
            endpointId.Trim(),
            diagnostics);
    }

    private static RuntimeProtocolConnectionBinding CreateCore(
        ITransportConnection transportConnection,
        string? endpointId,
        RuntimeDiagnosticPublisher? diagnostics)
    {
        ArgumentNullException.ThrowIfNull(
            transportConnection);

        if (transportConnection
            is not ITransportDuplexConnection duplexConnection)
        {
            return new RuntimeProtocolConnectionBinding(
                transportConnection,
                DecorateIfRequested(
                    new LegacyRuntimeProtocolConnection(
                        transportConnection),
                    endpointId,
                    diagnostics),
                duplexSession:
                    null,
                receivePumpCancellationSource:
                    null,
                receivePumpCompletion:
                    Task.CompletedTask);
        }

        var session =
            new ProtocolDuplexSession(
                duplexConnection);

        var cancellationSource =
            new CancellationTokenSource();

        NativeTransportByteDiagnosticObserver? byteTraceObserver =
            diagnostics is not null
            && diagnostics.IsEnabled(
                RuntimeDiagnosticLevel.Bytes)
                ? new NativeTransportByteDiagnosticObserver(
                    endpointId!,
                    diagnostics)
                : null;

        if (byteTraceObserver is not null)
        {
            session.SubscribeByteTrace(
                byteTraceObserver);
        }

        Task receivePumpCompletion =
            session.RunAsync(
                cancellationSource.Token);

        return new RuntimeProtocolConnectionBinding(
            transportConnection,
            DecorateIfRequested(
                new DuplexRuntimeProtocolConnection(
                    session),
                endpointId,
                diagnostics),
            session,
            cancellationSource,
            receivePumpCompletion,
            byteTraceObserver is null
                ? null
                : session,
            byteTraceObserver);
    }

    private static IRuntimeProtocolConnection DecorateIfRequested(
        IRuntimeProtocolConnection connection,
        string? endpointId,
        RuntimeDiagnosticPublisher? diagnostics)
    {
        return diagnostics is null
            ? connection
            : NativeRuntimeProtocolDiagnosticConnection.Create(
                connection,
                endpointId!,
                diagnostics);
    }

    /// <summary>
    /// Stops and observes the duplex receive pump.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(
                ref _disposed,
                1)
            != 0)
        {
            return;
        }

        CancellationTokenSource? cancellationSource =
            _receivePumpCancellationSource;

        if (_byteTraceSource is not null
            && _byteTraceObserver is not null)
        {
            _byteTraceSource.UnsubscribeByteTrace(
                _byteTraceObserver);
        }

        if (cancellationSource is null)
        {
            return;
        }

        try
        {
            cancellationSource.Cancel();

            try
            {
                await _receivePumpCompletion.ConfigureAwait(
                    false);
            }
            catch (Exception)
                when (cancellationSource.IsCancellationRequested)
            {
                // The receive pump has been observed. Its transport failure
                // is represented by the transport lifecycle and must not
                // prevent binding replacement or coordinator disposal.
            }
        }
        finally
        {
            cancellationSource.Dispose();
        }
    }
}
