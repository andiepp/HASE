namespace Hase.Mcnf.RfLab.Runtime;

/// <summary>
/// Turns MCNF exchanges with the RF-Lab node into typed observations and
/// acknowledged mutations. One serialized gate covers every operation; a
/// node-rejected request surfaces as <see cref="McnfDeviceErrorException"/>
/// without faulting the adapter, while transport-level failures fault it.
/// </summary>
public sealed class RfLabSessionAdapter : IAsyncDisposable
{
    private readonly IMcnfSession session;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim operationGate = new(1, 1);
    private readonly object disposalLock = new();
    private int state;
    private Task? disposalTask;

    public RfLabSessionAdapter(
        IMcnfSession session,
        TimeProvider? timeProvider = null)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool IsFaulted => Volatile.Read(ref state) == 1;

    public async Task ProbeHealthAsync(CancellationToken cancellationToken = default)
    {
        await ExecuteAsync<object?>(async token =>
        {
            await session.ConnectivityTestAsync(token).ConfigureAwait(false);
            return null;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<RfLabIdentity> VerifyIdentityAsync(
        CancellationToken cancellationToken = default) =>
        await ExecuteAsync(
            VerifyIdentityCoreAsync,
            cancellationToken).ConfigureAwait(false);

    public async Task<RfLabSynchronizationSnapshot> VerifyAndSynchronizeAsync(
        CancellationToken cancellationToken = default) =>
        await ExecuteAsync(async token =>
        {
            await session.ConnectivityTestAsync(token).ConfigureAwait(false);
            RfLabIdentity identity = await VerifyIdentityCoreAsync(token).ConfigureAwait(false);
            RfLabConfiguration configuration =
                await ReadConfigurationCoreAsync(token).ConfigureAwait(false);
            RfLabSensorObservation sensor =
                await ReadSensorCoreAsync(token).ConfigureAwait(false);
            return new RfLabSynchronizationSnapshot(
                identity,
                configuration,
                sensor,
                timeProvider.GetUtcNow());
        }, cancellationToken).ConfigureAwait(false);

    public async Task<RfLabConfigurationObservation> ReadConfigurationAsync(
        CancellationToken cancellationToken = default) =>
        await ExecuteAsync(async token =>
        {
            RfLabConfiguration configuration =
                await ReadConfigurationCoreAsync(token).ConfigureAwait(false);
            return new RfLabConfigurationObservation(configuration, timeProvider.GetUtcNow());
        }, cancellationToken).ConfigureAwait(false);

    public async Task<RfLabSensorObservation> ReadSensorAsync(
        CancellationToken cancellationToken = default) =>
        await ExecuteAsync(
            ReadSensorCoreAsync,
            cancellationToken).ConfigureAwait(false);

    public async Task<RfLabIndicatorObservation> ReadIndicatorAsync(
        CancellationToken cancellationToken = default) =>
        await ExecuteAsync(async token =>
        {
            McnfResponseFrame response = await ExchangeSuccessfullyAsync(
                RfLabProtocol.CreateIndicatorStateRequest(),
                token).ConfigureAwait(false);
            return new RfLabIndicatorObservation(
                RfLabProtocol.ParseIndicatorPayload(response.Payload.Span),
                timeProvider.GetUtcNow());
        }, cancellationToken).ConfigureAwait(false);

    public async Task<RfLabIndicatorObservation> SetIndicatorAsync(
        bool enable,
        CancellationToken cancellationToken = default) =>
        await ExecuteAsync(async token =>
        {
            McnfResponseFrame response = await MutateAsync(
                RfLabProtocol.CreateIndicatorRequest(enable),
                "indicator control",
                token).ConfigureAwait(false);
            bool readback = RfLabProtocol.ParseIndicatorPayload(response.Payload.Span);
            if (readback != enable)
            {
                throw new InvalidDataException(
                    "The RF-Lab indicator readback did not confirm the requested state.");
            }

            return new RfLabIndicatorObservation(readback, timeProvider.GetUtcNow());
        }, cancellationToken).ConfigureAwait(false);

    public async Task ApplyCarrierAsync(
        uint frequencyHertz,
        int attenuationDecibel,
        CancellationToken cancellationToken = default) =>
        await ExecuteMutationAsync(
            RfLabProtocol.CreateCarrierRequest(frequencyHertz, attenuationDecibel),
            "carrier",
            cancellationToken).ConfigureAwait(false);

    public async Task ApplyAmplitudeModulationAsync(
        uint carrierFrequencyHertz,
        int attenuationDecibel,
        uint modulationFrequencyHertz,
        int depthPercent,
        CancellationToken cancellationToken = default) =>
        await ExecuteMutationAsync(
            RfLabProtocol.CreateAmplitudeModulationRequest(
                carrierFrequencyHertz,
                attenuationDecibel,
                modulationFrequencyHertz,
                depthPercent),
            "amplitude modulation",
            cancellationToken).ConfigureAwait(false);

    public async Task ApplyFrequencyModulationAsync(
        uint carrierFrequencyHertz,
        int attenuationDecibel,
        uint modulationFrequencyHertz,
        uint deviationHertz,
        CancellationToken cancellationToken = default) =>
        await ExecuteMutationAsync(
            RfLabProtocol.CreateFrequencyModulationRequest(
                carrierFrequencyHertz,
                attenuationDecibel,
                modulationFrequencyHertz,
                deviationHertz),
            "frequency modulation",
            cancellationToken).ConfigureAwait(false);

    public async Task ApplySweepAsync(
        uint startFrequencyHertz,
        uint stopFrequencyHertz,
        int sweepTimeMilliseconds,
        int attenuationDecibel,
        RfLabSweepMode sweepMode,
        CancellationToken cancellationToken = default) =>
        await ExecuteMutationAsync(
            RfLabProtocol.CreateSweepRequest(
                startFrequencyHertz,
                stopFrequencyHertz,
                sweepTimeMilliseconds,
                attenuationDecibel,
                sweepMode),
            "sweep",
            cancellationToken).ConfigureAwait(false);

    public async Task ApplyClockAsync(
        int clockChannel,
        uint frequencyHertz,
        CancellationToken cancellationToken = default) =>
        await ExecuteMutationAsync(
            RfLabProtocol.CreateClockRequest(clockChannel, frequencyHertz),
            "clock output",
            cancellationToken).ConfigureAwait(false);

    public ValueTask DisposeAsync()
    {
        lock (disposalLock)
        {
            disposalTask ??= DisposeCoreAsync();
            return new ValueTask(disposalTask);
        }
    }

    private async Task<RfLabIdentity> VerifyIdentityCoreAsync(
        CancellationToken cancellationToken)
    {
        McnfResponseFrame response = await ExchangeSuccessfullyAsync(
            McnfNodeAdminRequests.CreateNodeTypeInfoRequest(),
            cancellationToken).ConfigureAwait(false);
        return RfLabIdentity.ParseNodeTypeInfo(response.Payload.Span);
    }

    private async Task<RfLabConfiguration> ReadConfigurationCoreAsync(
        CancellationToken cancellationToken)
    {
        McnfResponseFrame response = await ExchangeSuccessfullyAsync(
            RfLabProtocol.CreateReadConfigurationRequest(),
            cancellationToken).ConfigureAwait(false);
        return RfLabProtocol.ParseConfigurationPayload(response.Payload.Span);
    }

    private async Task<RfLabSensorObservation> ReadSensorCoreAsync(
        CancellationToken cancellationToken)
    {
        McnfResponseFrame response = await ExchangeSuccessfullyAsync(
            RfLabProtocol.CreateReadSensorRequest(),
            cancellationToken).ConfigureAwait(false);
        int adcValue = RfLabProtocol.ParseSensorPayload(response.Payload.Span);
        double millivolts = RfLabSensorConversion.MillivoltsFromAdcValue(adcValue);
        return new RfLabSensorObservation(
            adcValue,
            millivolts,
            RfLabSensorConversion.LevelFromMillivolts(millivolts),
            timeProvider.GetUtcNow());
    }

    private async Task ExecuteMutationAsync(
        McnfRequestFrame request,
        string operationName,
        CancellationToken cancellationToken)
    {
        _ = await ExecuteAsync(
            token => MutateAsync(request, operationName, token),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Transmits one mutating request exactly once. A missing acknowledged
    /// response surfaces as an explicitly uncertain outcome; the mutation is
    /// never replayed.
    /// </summary>
    private async Task<McnfResponseFrame> MutateAsync(
        McnfRequestFrame request,
        string operationName,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ExchangeSuccessfullyAsync(request, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (McnfExchangeException exception) when (exception.ExecutionMayHaveOccurred)
        {
            throw new RfLabMutationOutcomeUncertainException(
                $"The RF-Lab {operationName} outcome is uncertain because no acknowledged response was established.",
                exception);
        }
    }

    private async Task<McnfResponseFrame> ExchangeSuccessfullyAsync(
        McnfRequestFrame request,
        CancellationToken cancellationToken)
    {
        McnfResponseFrame response = await session
            .ExchangeAsync(request, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccess)
        {
            throw new McnfDeviceErrorException(
                response.ErrorCode,
                $"The RF-Lab node rejected the request: {RfLabDeviceErrorCode.Describe(response.ErrorCode)}.");
        }

        return response;
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
            catch (McnfDeviceErrorException)
            {
                // The node completed the exchange and rejected the request;
                // the session remains healthy.
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
                throw new InvalidOperationException("The RF-Lab session adapter is faulted.");
            default:
                throw new ObjectDisposedException(nameof(RfLabSessionAdapter));
        }
    }
}
