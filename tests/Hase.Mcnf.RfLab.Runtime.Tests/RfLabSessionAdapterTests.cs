using Hase.Mcnf;

namespace Hase.Mcnf.RfLab.Runtime.Tests;

public sealed class RfLabSessionAdapterTests
{
    [Fact]
    public async Task VerifyIdentityAsync_ParsesTheCharacterizedNodeType()
    {
        var session = new ScriptedMcnfSession();
        session.EnqueueSuccess(0xAE, 0x70, 0x10, 0x80);
        await using var adapter = new RfLabSessionAdapter(session);

        RfLabIdentity identity = await adapter.VerifyIdentityAsync();

        Assert.Equal("AE.70.10.80", identity.NodeType);
        McnfRequestFrame request = Assert.Single(session.Requests);
        Assert.Equal(0xA4, request.Channel);
        Assert.Equal(220, request.Function);
    }

    [Fact]
    public async Task VerifyIdentityAsync_FaultsTheAdapterOnForeignNodeTypes()
    {
        var session = new ScriptedMcnfSession();
        session.EnqueueSuccess(0xAE, 0x63, 0x05, 0x80);
        await using var adapter = new RfLabSessionAdapter(session);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => adapter.VerifyIdentityAsync());

        Assert.True(adapter.IsFaulted);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => adapter.ReadSensorAsync());
    }

    [Fact]
    public async Task VerifyAndSynchronizeAsync_RunsTheCompleteHandshakeInOrder()
    {
        var session = new ScriptedMcnfSession();
        session.EnqueueSuccess(0xAE, 0x70, 0x10, 0x80);
        session.EnqueueSuccess(0x00, 0x00, 0x00, 0b11);
        session.EnqueueSuccess(0x02, 0x9A);
        await using var adapter = new RfLabSessionAdapter(session);

        RfLabSynchronizationSnapshot snapshot = await adapter.VerifyAndSynchronizeAsync();

        Assert.Equal(1, session.ConnectivityTestCount);
        Assert.Equal(3, session.Requests.Count);
        Assert.Equal(220, session.Requests[0].Function);
        Assert.Equal(201, session.Requests[1].Function);
        Assert.Equal(0x20, session.Requests[2].Function);
        Assert.True(snapshot.Configuration.Si5351Present);
        Assert.True(snapshot.Configuration.LedOn);
        Assert.Equal(0x029A, snapshot.Sensor.AdcValue);
    }

    [Fact]
    public async Task ReadSensorAsync_ConvertsTheReading()
    {
        var session = new ScriptedMcnfSession();
        session.EnqueueSuccess(0x03, 0xE8);
        await using var adapter = new RfLabSessionAdapter(session);

        RfLabSensorObservation observation = await adapter.ReadSensorAsync();

        Assert.Equal(1000, observation.AdcValue);
        Assert.Equal(2500.0, observation.Millivolts);
        Assert.Equal(
            RfLabSensorConversion.LevelFromMillivolts(2500.0),
            observation.Level);
    }

    [Fact]
    public async Task SetIndicatorAsync_VerifiesTheAcknowledgedReadback()
    {
        var session = new ScriptedMcnfSession();
        session.EnqueueSuccess(0x01);
        await using var adapter = new RfLabSessionAdapter(session);

        RfLabIndicatorObservation observation = await adapter.SetIndicatorAsync(true);

        Assert.True(observation.Enabled);
        Assert.Equal(0x01, Assert.Single(session.Requests).Function);
    }

    [Fact]
    public async Task SetIndicatorAsync_FaultsWhenTheReadbackContradictsTheRequest()
    {
        var session = new ScriptedMcnfSession();
        session.EnqueueSuccess(0x00);
        await using var adapter = new RfLabSessionAdapter(session);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => adapter.SetIndicatorAsync(true));
        Assert.True(adapter.IsFaulted);
    }

    [Fact]
    public async Task DeviceErrors_SurfaceWithoutFaultingTheAdapter()
    {
        var session = new ScriptedMcnfSession();
        session.EnqueueDeviceError(RfLabDeviceErrorCode.Si5351Disconnected);
        session.EnqueueSuccess(0x02, 0x9A);
        await using var adapter = new RfLabSessionAdapter(session);

        var failure = await Assert.ThrowsAsync<McnfDeviceErrorException>(
            () => adapter.ApplyClockAsync(0, 1_000_000));

        Assert.Equal(RfLabDeviceErrorCode.Si5351Disconnected, failure.ErrorCode);
        Assert.Contains("Si5351Disconnected", failure.Message, StringComparison.Ordinal);
        Assert.False(adapter.IsFaulted);

        RfLabSensorObservation observation = await adapter.ReadSensorAsync();
        Assert.Equal(0x029A, observation.AdcValue);
    }

    [Fact]
    public async Task Mutations_ReportUncertainOutcomesExplicitlyAndFault()
    {
        var session = new ScriptedMcnfSession();
        session.EnqueueFailure(new McnfExchangeException(
            "The MCNF exchange outcome is uncertain because it timed out after transmission began.",
            executionMayHaveOccurred: true,
            new TimeoutException()));
        await using var adapter = new RfLabSessionAdapter(session);

        await Assert.ThrowsAsync<RfLabMutationOutcomeUncertainException>(
            () => adapter.ApplyCarrierAsync(10_000_000, 20));

        Assert.True(adapter.IsFaulted);
        Assert.Single(session.Requests);
    }

    [Fact]
    public async Task Mutations_PropagateUntransmittedFailuresWithoutUncertainty()
    {
        var session = new ScriptedMcnfSession();
        session.EnqueueFailure(new McnfExchangeException(
            "The MCNF exchange was not transmitted before its timeout expired.",
            executionMayHaveOccurred: false,
            new TimeoutException()));
        await using var adapter = new RfLabSessionAdapter(session);

        var failure = await Assert.ThrowsAsync<McnfExchangeException>(
            () => adapter.ApplyCarrierAsync(10_000_000, 20));

        Assert.False(failure.ExecutionMayHaveOccurred);
        Assert.True(adapter.IsFaulted);
    }

    [Fact]
    public async Task ApplyCarrierAsync_TransmitsTheCharacterizedFrame()
    {
        var session = new ScriptedMcnfSession();
        session.EnqueueSuccess();
        await using var adapter = new RfLabSessionAdapter(session);

        await adapter.ApplyCarrierAsync(10_000_000, 10);

        Assert.Equal(
            RfLabProtocol.CreateCarrierRequest(10_000_000, 10).Bytes.ToArray(),
            Assert.Single(session.Requests).Bytes.ToArray());
    }

    [Fact]
    public async Task ProbeHealthAsync_FaultsTheAdapterWhenConnectivityFails()
    {
        var session = new ScriptedMcnfSession
        {
            ConnectivityTestFailure = new InvalidDataException(
                "The MCNF connectivity test received an unexpected response byte.")
        };
        await using var adapter = new RfLabSessionAdapter(session);

        await Assert.ThrowsAsync<InvalidDataException>(() => adapter.ProbeHealthAsync());
        Assert.True(adapter.IsFaulted);
    }

    [Fact]
    public async Task DisposeAsync_DisposesTheSessionAndRejectsFurtherOperations()
    {
        var session = new ScriptedMcnfSession();
        var adapter = new RfLabSessionAdapter(session);

        await adapter.DisposeAsync();

        Assert.True(session.Disposed);
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => adapter.ReadSensorAsync());
    }
}
