namespace Hase.Scpi;

public sealed class ScpiTextSession : IScpiTextSession
{
    private readonly IScpiByteStream stream;
    private readonly ScpiTextFramingOptions options;
    private readonly ScpiTextRequestFormatter formatter;
    private readonly SemaphoreSlim exchangeGate = new(1, 1);
    private readonly CancellationTokenSource lifetimeCancellation = new();
    private int state = (int)ScpiTextSessionState.Open;
    private int disposeStarted;

    public ScpiTextSession(IScpiByteStream stream, ScpiTextFramingOptions options)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);

        this.stream = stream;
        this.options = options;
        formatter = new ScpiTextRequestFormatter(options);
    }

    public ScpiTextSessionState State => (ScpiTextSessionState)Volatile.Read(ref state);

    public async Task SendCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        EnsureOpen();
        var request = formatter.Format(command);
        cancellationToken.ThrowIfCancellationRequested();

        using var timeoutCancellation = new CancellationTokenSource(options.TotalExchangeTimeout);
        using var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            lifetimeCancellation.Token,
            timeoutCancellation.Token);

        var gateEntered = false;
        var writeStarted = false;
        try
        {
            await exchangeGate.WaitAsync(operationCancellation.Token).ConfigureAwait(false);
            gateEntered = true;
            EnsureOpen();

            writeStarted = true;
            await stream
                .WriteAsync(request, operationCancellation.Token)
                .AsTask()
                .WaitAsync(operationCancellation.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
        {
            if (writeStarted)
            {
                throw CreateCommandFailure(
                    "The SCPI command outcome is uncertain because the session was disposed during transmission.",
                    true,
                    new ObjectDisposedException(
                        nameof(ScpiTextSession),
                        "The SCPI text session was disposed during command transmission."));
            }

            throw new ObjectDisposedException(
                nameof(ScpiTextSession),
                "The SCPI text session was disposed before command transmission began.");
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            if (writeStarted)
            {
                throw CreateCommandFailure(
                    "The SCPI command outcome is uncertain because cancellation occurred during transmission.",
                    true,
                    exception);
            }

            throw;
        }
        catch (OperationCanceledException exception) when (timeoutCancellation.IsCancellationRequested)
        {
            var timeout = new TimeoutException("The SCPI command exceeded its total transmission timeout.", exception);
            if (writeStarted)
            {
                throw CreateCommandFailure(
                    "The SCPI command outcome is uncertain because transmission timed out after it began.",
                    true,
                    timeout);
            }

            throw new ScpiCommandTransmissionException(
                "The SCPI command was not transmitted before its timeout expired.",
                false,
                timeout);
        }
        catch (Exception exception) when (writeStarted)
        {
            throw CreateCommandFailure(
                "The SCPI command outcome is uncertain because transmission failed after it began.",
                true,
                exception);
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

        using var timeoutCancellation = new CancellationTokenSource(options.TotalExchangeTimeout);
        using var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            lifetimeCancellation.Token,
            timeoutCancellation.Token);

        var gateEntered = false;
        try
        {
            await exchangeGate.WaitAsync(operationCancellation.Token).ConfigureAwait(false);
            gateEntered = true;
            EnsureOpen();

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

                framer.Append(buffer.AsSpan(0, readByteCount));
            }

            return framer.Complete();
        }
        catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
        {
            throw new ObjectDisposedException(nameof(ScpiTextSession));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (gateEntered)
            {
                TransitionToFaulted();
            }

            throw;
        }
        catch (OperationCanceledException exception) when (timeoutCancellation.IsCancellationRequested)
        {
            TransitionToFaulted();
            throw new TimeoutException("The SCPI exchange exceeded its total timeout.", exception);
        }
        catch
        {
            TransitionToFaulted();
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

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposeStarted, 1) != 0)
        {
            return;
        }

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
