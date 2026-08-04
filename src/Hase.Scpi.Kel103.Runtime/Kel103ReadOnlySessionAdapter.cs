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

    public async Task<Kel103OperatingStateSynchronizationSnapshot>
        VerifyAndSynchronizeOperatingStateAsync(
            CancellationToken cancellationToken = default) =>
        await ExecuteAsync(async token =>
        {
            Kel103Identity identity = Kel103IdentityQuery.ParseResponse(
                await session.QueryAsync(Kel103IdentityQuery.CommandText, token).ConfigureAwait(false));
            decimal voltage = await QueryAsync(Kel103MeasurementMapping.Voltage, token).ConfigureAwait(false);
            decimal current = await QueryAsync(Kel103MeasurementMapping.Current, token).ConfigureAwait(false);
            decimal power = await QueryAsync(Kel103MeasurementMapping.Power, token).ConfigureAwait(false);
            Kel103OperatingMode mode = Kel103OperatingModeMapping.ParseResponse(
                await session.QueryAsync(Kel103OperatingModeMapping.Query, token).ConfigureAwait(false));
            bool inputEnabled = Kel103InputStateMapping.ParseResponse(
                await session.QueryAsync(Kel103InputStateMapping.Query, token).ConfigureAwait(false));
            decimal targetVoltage = await QueryAsync(Kel103SetpointMapping.Voltage, token).ConfigureAwait(false);
            decimal targetCurrent = await QueryAsync(Kel103SetpointMapping.Current, token).ConfigureAwait(false);
            decimal targetResistance = await QueryAsync(Kel103SetpointMapping.Resistance, token).ConfigureAwait(false);
            decimal targetPower = await QueryAsync(Kel103SetpointMapping.Power, token).ConfigureAwait(false);
            DateTimeOffset timestamp = timeProvider.GetUtcNow();
            return new Kel103OperatingStateSynchronizationSnapshot(
                identity,
                voltage,
                current,
                power,
                mode,
                inputEnabled,
                targetVoltage,
                targetCurrent,
                targetResistance,
                targetPower,
                timestamp);
        }, cancellationToken).ConfigureAwait(false);

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

    public async Task<Kel103OperatingModeObservation> ReadOperatingModeAsync(
        CancellationToken cancellationToken = default) =>
        await ExecuteAsync(async token =>
        {
            Kel103OperatingMode mode = Kel103OperatingModeMapping.ParseResponse(
                await session.QueryAsync(Kel103OperatingModeMapping.Query, token).ConfigureAwait(false));
            return new Kel103OperatingModeObservation(mode, timeProvider.GetUtcNow());
        }, cancellationToken).ConfigureAwait(false);

    public async Task<Kel103InputStateObservation> ReadInputStateAsync(
        CancellationToken cancellationToken = default) =>
        await ExecuteAsync(async token =>
        {
            bool inputEnabled = Kel103InputStateMapping.ParseResponse(
                await session.QueryAsync(Kel103InputStateMapping.Query, token).ConfigureAwait(false));
            return new Kel103InputStateObservation(inputEnabled, timeProvider.GetUtcNow());
        }, cancellationToken).ConfigureAwait(false);

    public async Task<Kel103SetpointObservation> ReadSetpointAsync(
        Kel103SetpointMapping mapping,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        return await ExecuteAsync(async token =>
        {
            decimal value = await QueryAsync(mapping, token).ConfigureAwait(false);
            return new Kel103SetpointObservation(
                mapping.Setpoint,
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

    private async Task<decimal> QueryAsync(
        Kel103SetpointMapping mapping,
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
