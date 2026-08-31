namespace Hase.Mcnf;

/// <summary>
/// One serialized command-response session with an MCNF node. All framed
/// exchanges and connectivity tests pass through the same gate; nothing
/// overlaps on the wire.
/// </summary>
public interface IMcnfSession : IAsyncDisposable
{
    McnfSessionState State { get; }

    /// <summary>
    /// Transmits one framed request and reads its complete response. A
    /// response reporting an application error byte is returned, not thrown;
    /// transport-level failures after transmission began fault the session
    /// and surface as <see cref="McnfExchangeException"/> with uncertain
    /// execution.
    /// </summary>
    Task<McnfResponseFrame> ExchangeAsync(
        McnfRequestFrame request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs the single-byte MCNF connectivity test and verifies the
    /// node's fixed response byte.
    /// </summary>
    Task ConnectivityTestAsync(CancellationToken cancellationToken = default);
}
