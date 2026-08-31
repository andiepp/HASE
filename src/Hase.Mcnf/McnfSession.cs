namespace Hase.Mcnf;

/// <summary>
/// The serialized MCNF command-response session. Every exchange transmits
/// one framed request and reads the fixed-length response through one gate;
/// any failure after transmission began faults the session because the node
/// may have executed the function and the wire may be desynchronized.
/// </summary>
public sealed class McnfSession : IMcnfSession
{
    private readonly IMcnfByteStream stream;
    private readonly McnfFramingOptions options;
    private readonly IMcnfDiagnosticObserver? diagnosticObserver;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim exchangeGate = new(1, 1);
    private readonly CancellationTokenSource lifetimeCancellation = new();
    private readonly object disposalLock = new();
    private int state = (int)McnfSessionState.Open;
    private Task? disposalTask;

    public McnfSession(IMcnfByteStream stream, McnfFramingOptions options)
        : this(stream, options, diagnosticObserver: null, timeProvider: null)
    {
    }

    public McnfSession(
        IMcnfByteStream stream,
        McnfFramingOptions options,
        IMcnfDiagnosticObserver? diagnosticObserver,
        TimeProvider? timeProvider = null)
    {
        this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.diagnosticObserver = diagnosticObserver;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public McnfSessionState State => (McnfSessionState)Volatile.Read(ref state);

    public async Task<McnfResponseFrame> ExchangeAsync(
        McnfRequestFrame request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.FrameLength > options.NodeBufferSize)
        {
            throw new ArgumentException(
                "The MCNF request frame exceeds the node buffer size.",
                nameof(request));
        }

        if (request.ResponseLength > options.NodeBufferSize)
        {
            throw new ArgumentException(
                "The expected MCNF response exceeds the node buffer size.",
                nameof(request));
        }

        var responseBuffer = new byte[request.ResponseLength];
        await RunExchangeAsync(
            McnfDiagnosticExchangeKind.Exchange,
            request.Bytes,
            responseBuffer,
            cancellationToken).ConfigureAwait(false);

        McnfResponseFrame response;
        try
        {
            response = McnfResponseFrame.Parse(responseBuffer);
        }
        catch (InvalidDataException exception)
        {
            TransitionToFaulted();
            throw new McnfExchangeException(
                "The MCNF exchange outcome is uncertain because the response failed verification.",
                executionMayHaveOccurred: true,
                exception);
        }

        return response;
    }

    public async Task ConnectivityTestAsync(CancellationToken cancellationToken = default)
    {
        var responseBuffer = new byte[1];
        await RunExchangeAsync(
            McnfDiagnosticExchangeKind.ConnectivityTest,
            new[] { McnfConstants.ConnectivityTestChannel },
            responseBuffer,
            cancellationToken).ConfigureAwait(false);

        if (responseBuffer[0] != McnfConstants.ConnectivityTestResponse)
        {
            TransitionToFaulted();
            throw new InvalidDataException(
                "The MCNF connectivity test received an unexpected response byte.");
        }
    }

    private async Task RunExchangeAsync(
        McnfDiagnosticExchangeKind exchangeKind,
        ReadOnlyMemory<byte> requestBytes,
        byte[] responseBuffer,
        CancellationToken cancellationToken)
    {
        EnsureOpen();
        cancellationToken.ThrowIfCancellationRequested();

        using var timeoutCancellation = new CancellationTokenSource();
        using var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            lifetimeCancellation.Token,
            timeoutCancellation.Token);

        var gateEntered = false;
        var writeStarted = false;
        McnfDiagnosticExchange? diagnosticExchange = null;
        try
        {
            await exchangeGate.WaitAsync(operationCancellation.Token).ConfigureAwait(false);
            gateEntered = true;
            EnsureOpen();
            timeoutCancellation.CancelAfter(options.TotalExchangeTimeout);
            diagnosticExchange = McnfDiagnosticExchange.Start(
                diagnosticObserver,
                timeProvider,
                exchangeKind);

            writeStarted = true;
            diagnosticExchange?.ObserveBytes(
                McnfDiagnosticDirection.Transmit,
                requestBytes.Span);
            await stream
                .WriteAsync(requestBytes, operationCancellation.Token)
                .AsTask()
                .WaitAsync(operationCancellation.Token)
                .ConfigureAwait(false);

            var receivedByteCount = 0;
            while (receivedByteCount < responseBuffer.Length)
            {
                var readByteCount = await stream
                    .ReadAsync(
                        responseBuffer.AsMemory(receivedByteCount),
                        operationCancellation.Token)
                    .AsTask()
                    .WaitAsync(operationCancellation.Token)
                    .ConfigureAwait(false);

                if (readByteCount == 0)
                {
                    throw new EndOfStreamException(
                        "The MCNF byte stream ended before the complete response was received.");
                }

                if (readByteCount < 0
                    || readByteCount > responseBuffer.Length - receivedByteCount)
                {
                    throw new InvalidDataException(
                        "The MCNF byte stream returned an invalid read byte count.");
                }

                diagnosticExchange?.ObserveBytes(
                    McnfDiagnosticDirection.Receive,
                    responseBuffer.AsSpan(receivedByteCount, readByteCount));
                receivedByteCount += readByteCount;
            }

            diagnosticExchange?.Complete();
        }
        catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
        {
            if (writeStarted)
            {
                McnfExchangeException failure = CreateUncertainFailure(
                    "The MCNF exchange outcome is uncertain because the session was disposed during the exchange.",
                    new ObjectDisposedException(
                        nameof(McnfSession),
                        "The MCNF session was disposed during the exchange."));
                diagnosticExchange?.Fail(
                    McnfDiagnosticOutcome.Uncertain,
                    failure,
                    executionMayHaveOccurred: true);
                throw failure;
            }

            var failureBeforeTransmission = new ObjectDisposedException(
                nameof(McnfSession),
                "The MCNF session was disposed before the exchange began.");
            diagnosticExchange?.Fail(
                McnfDiagnosticOutcome.Disposed,
                failureBeforeTransmission);
            throw failureBeforeTransmission;
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            if (writeStarted)
            {
                McnfExchangeException failure = CreateUncertainFailure(
                    "The MCNF exchange outcome is uncertain because cancellation occurred during the exchange.",
                    exception);
                diagnosticExchange?.Fail(
                    McnfDiagnosticOutcome.Uncertain,
                    failure,
                    executionMayHaveOccurred: true);
                throw failure;
            }

            diagnosticExchange?.Fail(
                McnfDiagnosticOutcome.Canceled,
                exception);
            throw;
        }
        catch (OperationCanceledException exception) when (timeoutCancellation.IsCancellationRequested)
        {
            var timeout = new TimeoutException(
                "The MCNF exchange exceeded its total timeout.",
                exception);
            if (writeStarted)
            {
                McnfExchangeException failure = CreateUncertainFailure(
                    "The MCNF exchange outcome is uncertain because it timed out after transmission began.",
                    timeout);
                diagnosticExchange?.Fail(
                    McnfDiagnosticOutcome.Uncertain,
                    failure,
                    executionMayHaveOccurred: true);
                throw failure;
            }

            TransitionToFaulted();
            var failureBeforeTransmission = new McnfExchangeException(
                "The MCNF exchange was not transmitted before its timeout expired.",
                executionMayHaveOccurred: false,
                timeout);
            diagnosticExchange?.Fail(
                McnfDiagnosticOutcome.TimedOut,
                timeout);
            throw failureBeforeTransmission;
        }
        catch (Exception exception) when (writeStarted)
        {
            McnfExchangeException failure = CreateUncertainFailure(
                "The MCNF exchange outcome is uncertain because it failed after transmission began.",
                exception);
            diagnosticExchange?.Fail(
                McnfDiagnosticOutcome.Uncertain,
                failure,
                executionMayHaveOccurred: true);
            throw failure;
        }
        finally
        {
            if (gateEntered)
            {
                exchangeGate.Release();
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (disposalLock)
        {
            disposalTask ??= DisposeCoreAsync();
            return new ValueTask(disposalTask);
        }
    }

    private async Task DisposeCoreAsync()
    {
        Interlocked.Exchange(ref state, (int)McnfSessionState.Disposed);
        await lifetimeCancellation.CancelAsync().ConfigureAwait(false);

        await exchangeGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await stream.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            exchangeGate.Release();
            lifetimeCancellation.Dispose();
        }
    }

    private McnfExchangeException CreateUncertainFailure(
        string message,
        Exception innerException)
    {
        TransitionToFaulted();
        return new McnfExchangeException(
            message,
            executionMayHaveOccurred: true,
            innerException);
    }

    private void EnsureOpen()
    {
        switch (State)
        {
            case McnfSessionState.Open:
                return;
            case McnfSessionState.Faulted:
                throw new InvalidOperationException("The MCNF session is faulted.");
            case McnfSessionState.Disposed:
                throw new ObjectDisposedException(nameof(McnfSession));
            default:
                throw new InvalidOperationException("The MCNF session has an unknown state.");
        }
    }

    private void TransitionToFaulted()
    {
        Interlocked.CompareExchange(
            ref state,
            (int)McnfSessionState.Faulted,
            (int)McnfSessionState.Open);
    }
}
