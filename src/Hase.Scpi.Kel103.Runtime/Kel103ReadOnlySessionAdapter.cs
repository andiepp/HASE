using Hase.Scpi;

namespace Hase.Scpi.Kel103.Runtime;

public sealed class Kel103ReadOnlySessionAdapter : IAsyncDisposable
{
    private readonly IScpiTextSession session;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim operationGate = new(1, 1);
    private readonly object disposalLock = new();
    private int state;
    private Task? disposalTask;

    public Kel103ReadOnlySessionAdapter(
        IScpiTextSession session,
        TimeProvider? timeProvider = null)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool IsFaulted => Volatile.Read(ref state) == 1;

    public async Task<Kel103SynchronizationSnapshot> VerifyAndSynchronizeAsync(
        CancellationToken cancellationToken = default) =>
        await ExecuteAsync(async token =>
        {
            Kel103Identity identity = Kel103IdentityQuery.ParseResponse(
                await session.QueryAsync(Kel103IdentityQuery.CommandText, token).ConfigureAwait(false));
            decimal voltage = await QueryAsync(Kel103MeasurementMapping.Voltage, token).ConfigureAwait(false);
            decimal current = await QueryAsync(Kel103MeasurementMapping.Current, token).ConfigureAwait(false);
            decimal power = await QueryAsync(Kel103MeasurementMapping.Power, token).ConfigureAwait(false);
            DateTimeOffset timestamp = timeProvider.GetUtcNow();
            return new Kel103SynchronizationSnapshot(identity, voltage, current, power, timestamp);
        }, cancellationToken).ConfigureAwait(false);

    public async Task<Kel103Identity> ReadIdentityAsync(
        CancellationToken cancellationToken = default) =>
        await ExecuteAsync(async token =>
            Kel103IdentityQuery.ParseResponse(
                await session.QueryAsync(Kel103IdentityQuery.CommandText, token).ConfigureAwait(false)),
            cancellationToken).ConfigureAwait(false);

    public async Task<Kel103MeasurementObservation> ReadMeasurementAsync(
        Kel103MeasurementMapping mapping,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        return await ExecuteAsync(async token =>
        {
            decimal value = await QueryAsync(mapping, token).ConfigureAwait(false);
            return new Kel103MeasurementObservation(
                mapping.Measurement,
                value,
                timeProvider.GetUtcNow());
        }, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync()
    {
        lock (disposalLock)
        {
            disposalTask ??= DisposeCoreAsync();
            return new ValueTask(disposalTask);
        }
    }

    private async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        EnsureOpen();
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureOpen();
            try
            {
                return await operation(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                Interlocked.CompareExchange(ref state, 1, 0);
                throw;
            }
            catch (OperationCanceledException)
            {
                Interlocked.CompareExchange(ref state, 1, 0);
                throw;
            }
            catch
            {
                Interlocked.CompareExchange(ref state, 1, 0);
                throw;
            }
        }
        finally
        {
            operationGate.Release();
        }
    }

    private async Task<decimal> QueryAsync(
        Kel103MeasurementMapping mapping,
        CancellationToken cancellationToken) =>
        mapping.ParseResponse(
            await session.QueryAsync(mapping.Query, cancellationToken).ConfigureAwait(false));

    private async Task DisposeCoreAsync()
    {
        Interlocked.Exchange(ref state, 2);
        await operationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            operationGate.Release();
        }
    }

    private void EnsureOpen()
    {
        switch (Volatile.Read(ref state))
        {
            case 0:
                return;
            case 1:
                throw new InvalidOperationException("The KEL-103 session adapter is faulted.");
            default:
                throw new ObjectDisposedException(nameof(Kel103ReadOnlySessionAdapter));
        }
    }
}
