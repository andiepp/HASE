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

    private int _disposed;

    private RuntimeProtocolConnectionBinding(
        ITransportConnection transportConnection,
        IRuntimeProtocolConnection protocolConnection,
        ProtocolDuplexSession? duplexSession,
        CancellationTokenSource? receivePumpCancellationSource,
        Task receivePumpCompletion)
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
        ArgumentNullException.ThrowIfNull(
            transportConnection);

        if (transportConnection
            is not ITransportDuplexConnection duplexConnection)
        {
            return new RuntimeProtocolConnectionBinding(
                transportConnection,
                new LegacyRuntimeProtocolConnection(
                    transportConnection),
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

        Task receivePumpCompletion =
            session.RunAsync(
                cancellationSource.Token);

        return new RuntimeProtocolConnectionBinding(
            transportConnection,
            new DuplexRuntimeProtocolConnection(
                session),
            session,
            cancellationSource,
            receivePumpCompletion);
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