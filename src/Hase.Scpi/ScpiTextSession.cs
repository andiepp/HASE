namespace Hase.Scpi;

public sealed class ScpiTextSession : IScpiTextSession
{
    private readonly IScpiByteStream stream;
    private readonly ScpiTextFramingOptions options;
    private readonly ScpiTextRequestFormatter formatter;
    private readonly IScpiDiagnosticObserver? diagnosticObserver;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim exchangeGate = new(1, 1);
    private readonly CancellationTokenSource lifetimeCancellation = new();
    private readonly object disposalLock = new();
    private int state = (int)ScpiTextSessionState.Open;
    private Task? disposalTask;

    public ScpiTextSession(IScpiByteStream stream, ScpiTextFramingOptions options)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);

        this.stream = stream;
        this.options = options;
        diagnosticObserver = null;
        timeProvider = TimeProvider.System;
        formatter = new ScpiTextRequestFormatter(options);
    }

    public ScpiTextSession(
        IScpiByteStream stream,
        ScpiTextFramingOptions options,
        IScpiDiagnosticObserver diagnosticObserver,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(diagnosticObserver);

        this.stream = stream;
        this.options = options;
        this.diagnosticObserver = diagnosticObserver;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        formatter = new ScpiTextRequestFormatter(options);
    }

    public ScpiTextSessionState State => (ScpiTextSessionState)Volatile.Read(ref state);

    public async Task SendCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        EnsureOpen();
        var request = formatter.Format(command);
        cancellationToken.ThrowIfCancellationRequested();

        using var timeoutCancellation = new CancellationTokenSource();
        using var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            lifetimeCancellation.Token,
            timeoutCancellation.Token);

        var gateEntered = false;
        var writeStarted = false;
        ScpiDiagnosticExchange? diagnosticExchange = null;
        try
        {
            await exchangeGate.WaitAsync(operationCancellation.Token).ConfigureAwait(false);
            gateEntered = true;
            EnsureOpen();
            timeoutCancellation.CancelAfter(options.TotalExchangeTimeout);
            diagnosticExchange = ScpiDiagnosticExchange.Start(
                diagnosticObserver,
                timeProvider,
                ScpiDiagnosticExchangeKind.Command);

            writeStarted = true;
            diagnosticExchange?.ObserveBytes(
                ScpiDiagnosticDirection.Transmit,
                request);
            await stream
                .WriteAsync(request, operationCancellation.Token)
                .AsTask()
                .WaitAsync(operationCancellation.Token)
                .ConfigureAwait(false);
            diagnosticExchange?.Complete();
        }
        catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
        {
            if (writeStarted)
            {
                ScpiCommandTransmissionException failure = CreateCommandFailure(
                    "The SCPI command outcome is uncertain because the session was disposed during transmission.",
                    true,
                    new ObjectDisposedException(
                        nameof(ScpiTextSession),
                        "The SCPI text session was disposed during command transmission."));
                diagnosticExchange?.Fail(
                    ScpiDiagnosticOutcome.Uncertain,
                    failure,
                    executionMayHaveOccurred: true);
                throw failure;
            }

            var failureBeforeTransmission = new ObjectDisposedException(
                nameof(ScpiTextSession),
                "The SCPI text session was disposed before command transmission began.");
            diagnosticExchange?.Fail(
                ScpiDiagnosticOutcome.Disposed,
                failureBeforeTransmission);
            throw failureBeforeTransmission;
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            if (writeStarted)
            {
                ScpiCommandTransmissionException failure = CreateCommandFailure(
                    "The SCPI command outcome is uncertain because cancellation occurred during transmission.",
                    true,
                    exception);
                diagnosticExchange?.Fail(
                    ScpiDiagnosticOutcome.Uncertain,
                    failure,
                    executionMayHaveOccurred: true);
                throw failure;
            }

            diagnosticExchange?.Fail(
                ScpiDiagnosticOutcome.Canceled,
                exception);
            throw;
        }
        catch (OperationCanceledException exception) when (timeoutCancellation.IsCancellationRequested)
        {
            var timeout = new TimeoutException("The SCPI command exceeded its total transmission timeout.", exception);
            if (writeStarted)
            {
                ScpiCommandTransmissionException failure = CreateCommandFailure(
                    "The SCPI command outcome is uncertain because transmission timed out after it began.",
                    true,
                    timeout);
                diagnosticExchange?.Fail(
                    ScpiDiagnosticOutcome.Uncertain,
                    failure,
                    executionMayHaveOccurred: true);
                throw failure;
            }

            var failureBeforeTransmission = new ScpiCommandTransmissionException(
                "The SCPI command was not transmitted before its timeout expired.",
                false,
                timeout);
            diagnosticExchange?.Fail(
                ScpiDiagnosticOutcome.TimedOut,
                timeout);
            throw failureBeforeTransmission;
        }
        catch (Exception exception) when (writeStarted)
        {
            ScpiCommandTransmissionException failure = CreateCommandFailure(
                "The SCPI command outcome is uncertain because transmission failed after it began.",
                true,
                exception);
            diagnosticExchange?.Fail(
                ScpiDiagnosticOutcome.Uncertain,
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

    public async Task<string> QueryAsync(string query, CancellationToken cancellationToken = default)
    {
        EnsureOpen();
        var request = formatter.Format(query);
        cancellationToken.ThrowIfCancellationRequested();

        using var timeoutCancellation = new CancellationTokenSource();
        using var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            lifetimeCancellation.Token,
            timeoutCancellation.Token);

        var gateEntered = false;
        ScpiDiagnosticExchange? diagnosticExchange = null;
        try
        {
            await exchangeGate.WaitAsync(operationCancellation.Token).ConfigureAwait(false);
            gateEntered = true;
            EnsureOpen();
            timeoutCancellation.CancelAfter(options.TotalExchangeTimeout);
            diagnosticExchange = ScpiDiagnosticExchange.Start(
                diagnosticObserver,
                timeProvider,
                ScpiDiagnosticExchangeKind.Query);

            diagnosticExchange?.ObserveBytes(
                ScpiDiagnosticDirection.Transmit,
                request);
            await stream
                .WriteAsync(request, operationCancellation.Token)
                .AsTask()
                .WaitAsync(operationCancellation.Token)
                .ConfigureAwait(false);

            var framer = new ScpiTextResponseFramer(options);
            var buffer = new byte[Math.Min(options.MaximumResponseBytes, 256)];

            while (!framer.IsComplete)
            {
                var readByteCount = await stream
                    .ReadAsync(buffer, operationCancellation.Token)
                    .AsTask()
                    .WaitAsync(operationCancellation.Token)
                    .ConfigureAwait(false);

                if (readByteCount == 0)
                {
                    throw new EndOfStreamException("The SCPI byte stream ended before the response terminator was received.");
                }

                if (readByteCount < 0 || readByteCount > buffer.Length)
                {
                    throw new InvalidDataException("The SCPI byte stream returned an invalid read byte count.");
                }

                diagnosticExchange?.ObserveBytes(
                    ScpiDiagnosticDirection.Receive,
                    buffer.AsSpan(0, readByteCount));
                framer.Append(buffer.AsSpan(0, readByteCount));
            }

            string response = framer.Complete();
            diagnosticExchange?.Complete();
            return response;
        }
        catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
        {
            var failure = new ObjectDisposedException(
                nameof(ScpiTextSession),
                "The SCPI text session was disposed during the query.");
            diagnosticExchange?.Fail(
                ScpiDiagnosticOutcome.Disposed,
                failure);
            throw failure;
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            if (gateEntered)
            {
                TransitionToFaulted();
            }

            diagnosticExchange?.Fail(
                ScpiDiagnosticOutcome.Canceled,
                exception);
            throw;
        }
        catch (OperationCanceledException exception) when (timeoutCancellation.IsCancellationRequested)
        {
            TransitionToFaulted();
            var failure = new TimeoutException(
                "The SCPI exchange exceeded its total timeout.",
                exception);
            diagnosticExchange?.Fail(
                ScpiDiagnosticOutcome.TimedOut,
                failure);
            throw failure;
        }
        catch (Exception exception)
        {
            TransitionToFaulted();
            diagnosticExchange?.Fail(
                ScpiDiagnosticOutcome.Failed,
                exception);
            throw;
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
        Interlocked.Exchange(ref state, (int)ScpiTextSessionState.Disposed);
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

    private ScpiCommandTransmissionException CreateCommandFailure(
        string message,
        bool executionMayHaveOccurred,
        Exception innerException)
    {
        TransitionToFaulted();
        return new ScpiCommandTransmissionException(message, executionMayHaveOccurred, innerException);
    }

    private void EnsureOpen()
    {
        switch (State)
        {
            case ScpiTextSessionState.Open:
                return;
            case ScpiTextSessionState.Faulted:
                throw new InvalidOperationException("The SCPI text session is faulted.");
            case ScpiTextSessionState.Disposed:
                throw new ObjectDisposedException(nameof(ScpiTextSession));
            default:
                throw new InvalidOperationException("The SCPI text session has an unknown state.");
        }
    }

    private void TransitionToFaulted()
    {
        Interlocked.CompareExchange(
            ref state,
            (int)ScpiTextSessionState.Faulted,
            (int)ScpiTextSessionState.Open);
    }
}
