using System.Net.Sockets;
using Hase.Protocol;
using Hase.Transport;
using Hase.Transport.Discovery;
using Hase.Transport.Tcp;

namespace Hase.Runtime.Transport.Discovery;

/// <summary>
/// Verifies network endpoint candidates through framed TCP and the HASE
/// Protocol Version 1 discovery exchange.
/// </summary>
public sealed class TcpProtocolNetworkEndpointCandidateVerifier
    : INetworkEndpointCandidateVerifier
{
    /// <summary>
    /// Gets the default maximum accepted protocol payload length.
    /// </summary>
    public const int DefaultMaximumPayloadLength =
        4096;

    private readonly Func<
        NetworkEndpointCandidate,
        TimeSpan,
        CancellationToken,
        Task<ProtocolMessage>> _exchange;

    /// <summary>
    /// Initializes a TCP Protocol Version 1 candidate verifier.
    /// </summary>
    public TcpProtocolNetworkEndpointCandidateVerifier(
        int maximumPayloadLength =
            DefaultMaximumPayloadLength)
        : this(
            CreateTcpExchange(
                maximumPayloadLength))
    {
    }

    internal TcpProtocolNetworkEndpointCandidateVerifier(
        Func<
            NetworkEndpointCandidate,
            TimeSpan,
            CancellationToken,
            Task<ProtocolMessage>> exchange)
    {
        _exchange =
            exchange
            ?? throw new ArgumentNullException(
                nameof(exchange));
    }

    /// <inheritdoc />
    public async Task<
        NetworkEndpointVerificationResult> VerifyAsync(
            NetworkEndpointCandidate candidate,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            candidate);

        if (timeout != Timeout.InfiniteTimeSpan
            && timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                timeout,
                "The candidate verification timeout must be positive "
                + "or Timeout.InfiniteTimeSpan.");
        }

        cancellationToken
            .ThrowIfCancellationRequested();

        using var timeoutCancellationTokenSource =
            CancellationTokenSource
                .CreateLinkedTokenSource(
                    cancellationToken);

        if (timeout != Timeout.InfiniteTimeSpan)
        {
            timeoutCancellationTokenSource.CancelAfter(
                timeout);
        }

        ProtocolMessage response;

        try
        {
            response =
                await _exchange(
                    candidate,
                    timeout,
                    timeoutCancellationTokenSource.Token);
        }
        catch (OperationCanceledException exception)
            when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(
                "Candidate verification was cancelled.",
                exception,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (timeoutCancellationTokenSource
                .IsCancellationRequested)
        {
            return Reject(
                candidate,
                NetworkEndpointVerificationFailure
                    .TimedOut,
                $"Candidate verification did not complete within "
                + $"{timeout}.");
        }
        catch (TimeoutException exception)
        {
            return Reject(
                candidate,
                NetworkEndpointVerificationFailure
                    .TimedOut,
                exception.Message);
        }
        catch (InvalidDataException exception)
        {
            return Reject(
                candidate,
                NetworkEndpointVerificationFailure
                    .NonHaseEndpoint,
                exception.Message);
        }
        catch (NotSupportedException exception)
        {
            return Reject(
                candidate,
                NetworkEndpointVerificationFailure
                    .NonHaseEndpoint,
                exception.Message);
        }
        catch (SocketException exception)
        {
            return Reject(
                candidate,
                NetworkEndpointVerificationFailure
                    .Unreachable,
                exception.Message);
        }
        catch (IOException exception)
        {
            return Reject(
                candidate,
                NetworkEndpointVerificationFailure
                    .Unreachable,
                exception.Message);
        }

        if (response
            is not DiscoverResponse discoverResponse)
        {
            return Reject(
                candidate,
                NetworkEndpointVerificationFailure
                    .InvalidProtocolResponse,
                $"Candidate verification expected a "
                + $"{nameof(DiscoverResponse)} but received "
                + $"'{response.GetType().Name}'.");
        }

        return new VerifiedNetworkEndpoint(
            candidate,
            discoverResponse.EndpointId);
    }

    private static Func<
        NetworkEndpointCandidate,
        TimeSpan,
        CancellationToken,
        Task<ProtocolMessage>> CreateTcpExchange(
            int maximumPayloadLength)
    {
        if (maximumPayloadLength < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumPayloadLength),
                maximumPayloadLength,
                "The maximum payload length must not be negative.");
        }

        return (
            candidate,
            timeout,
            cancellationToken) =>
                ExchangeOverTcpAsync(
                    candidate,
                    timeout,
                    maximumPayloadLength,
                    cancellationToken);
    }

    private static async Task<ProtocolMessage>
        ExchangeOverTcpAsync(
            NetworkEndpointCandidate candidate,
            TimeSpan timeout,
            int maximumPayloadLength,
            CancellationToken cancellationToken)
    {
        var options =
            new TcpTransportOptions(
                candidate.Address.ToString(),
                candidate.Port,
                timeout);

        ITransportFactory transportFactory =
            new TcpTransportFactory(
                options,
                maximumPayloadLength);

        ITransportConnection connection =
            await transportFactory.ConnectAsync(
                cancellationToken);

        try
        {
            var protocolConnection =
                new LegacyRuntimeProtocolConnection(
                    connection);

            var request =
                new DiscoverRequest(
                    new CorrelationId(
                        1));

            return await protocolConnection.SendAsync(
                request,
                cancellationToken);
        }
        finally
        {
            await DisposeConnectionAsync(
                connection);
        }
    }

    private static async ValueTask DisposeConnectionAsync(
        ITransportConnection connection)
    {
        if (connection
            is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();

            return;
        }

        if (connection
            is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private static RejectedNetworkEndpointCandidate Reject(
        NetworkEndpointCandidate candidate,
        NetworkEndpointVerificationFailure failure,
        string detail)
    {
        if (string.IsNullOrWhiteSpace(
            detail))
        {
            detail =
                $"Candidate verification failed with "
                + $"'{failure}'.";
        }

        return new RejectedNetworkEndpointCandidate(
            candidate,
            failure,
            detail);
    }
}