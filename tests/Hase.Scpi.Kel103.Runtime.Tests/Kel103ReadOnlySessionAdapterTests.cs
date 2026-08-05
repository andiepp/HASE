using Hase.Scpi;

namespace Hase.Scpi.Kel103.Runtime.Tests;

public sealed class Kel103ReadOnlySessionAdapterTests
{
    [Fact]
    public async Task Synchronize_QueriesExactOrderAndReturnsOneTimestamp()
    {
        var session = new FakeSession("RND 320-KEL103 V3.30 SN:REDACTED", "9.8864V", "0.1000A", "0.9893W");
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero));
        await using var adapter = new Kel103ReadOnlySessionAdapter(session, time);

        Kel103SynchronizationSnapshot result = await adapter.VerifyAndSynchronizeAsync();

        Assert.Equal(new[] { "*IDN?", ":MEASure:VOLTage?", ":MEASure:CURRent?", ":MEASure:POWer?" }, session.Queries);
        Assert.Equal("KEL-103", result.Identity.ProductIdentity);
        Assert.Equal(9.8864m, result.Voltage);
        Assert.Equal(0.1000m, result.Current);
        Assert.Equal(0.9893m, result.Power);
        Assert.Equal(time.GetUtcNow(), result.TimestampUtc);
    }

    [Theory]
    [InlineData(0, "1.25V", "1.25")]
    [InlineData(1, "0.10A", "0.10")]
    [InlineData(2, "0.125W", "0.125")]
    public async Task ReadMeasurement_UsesSelectedProductionMapping(int index, string response, string expected)
    {
        var session = new FakeSession(response);
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);

        Kel103MeasurementObservation result = await adapter.ReadMeasurementAsync(
            Kel103MeasurementMapping.All[index]);

        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), result.Value);
        Assert.Equal((Kel103Measurement)index, result.Measurement);
        Assert.Equal(Kel103MeasurementMapping.All[index].Query, Assert.Single(session.Queries));
        Assert.Equal(TimeSpan.Zero, result.TimestampUtc.Offset);
    }

    [Fact]
    public async Task ReadIdentity_ReturnsSanitizedIdentity()
    {
        var session = new FakeSession("RND 320-KEL103 V3.30 SN:REDACTED");
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);
        Kel103Identity identity = await adapter.ReadIdentityAsync();
        Assert.Equal("KEL-103 V3.30", identity.ToString());
        Assert.DoesNotContain("REDACTED", identity.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SynchronizeOperatingState_QueriesExactOrderAndReturnsOneTimestamp()
    {
        var session = new FakeSession(
            "RND 320-KEL103 V3.30 SN:REDACTED",
            "9.8864V",
            "0.1000A",
            "0.9893W",
            "CR",
            "OFF",
            "10.000V",
            "0.1000A",
            "100.00OHM",
            "1.000W");
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero));
        await using var adapter = new Kel103ReadOnlySessionAdapter(session, time);

        Kel103OperatingStateSynchronizationSnapshot result =
            await adapter.VerifyAndSynchronizeOperatingStateAsync();

        Assert.Equal(
            new[]
            {
                "*IDN?",
                ":MEASure:VOLTage?",
                ":MEASure:CURRent?",
                ":MEASure:POWer?",
                ":FUNCtion?",
                ":INPut?",
                ":VOLTage?",
                ":CURRent?",
                ":RESistance?",
                ":POWer?"
            },
            session.Queries);
        Assert.Equal("KEL-103", result.Identity.ProductIdentity);
        Assert.Equal(9.8864m, result.Voltage);
        Assert.Equal(0.1000m, result.Current);
        Assert.Equal(0.9893m, result.Power);
        Assert.Equal(Kel103OperatingMode.ConstantResistance, result.OperatingMode);
        Assert.False(result.InputEnabled);
        Assert.Equal(10.000m, result.TargetVoltage);
        Assert.Equal(0.1000m, result.TargetCurrent);
        Assert.Equal(100.00m, result.TargetResistance);
        Assert.Equal(1.000m, result.TargetPower);
        Assert.Equal(time.GetUtcNow(), result.TimestampUtc);
    }

    [Fact]
    public async Task ReadOperatingMode_UsesExactQueryAndReturnsTimestamp()
    {
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero));
        var session = new FakeSession("SHORt");
        await using var adapter = new Kel103ReadOnlySessionAdapter(session, time);

        Kel103OperatingModeObservation result = await adapter.ReadOperatingModeAsync();

        Assert.Equal(Kel103OperatingMode.ShortCircuit, result.Mode);
        Assert.Equal(time.GetUtcNow(), result.TimestampUtc);
        Assert.Equal(new[] { ":FUNCtion?" }, session.Queries);
    }

    [Fact]
    public async Task ReadInputState_UsesExactQueryAndReturnsTimestamp()
    {
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero));
        var session = new FakeSession("ON");
        await using var adapter = new Kel103ReadOnlySessionAdapter(session, time);

        Kel103InputStateObservation result = await adapter.ReadInputStateAsync();

        Assert.True(result.InputEnabled);
        Assert.Equal(time.GetUtcNow(), result.TimestampUtc);
        Assert.Equal(new[] { ":INPut?" }, session.Queries);
    }

    [Theory]
    [InlineData(0, "1.25V", "1.25")]
    [InlineData(1, "0.10A", "0.10")]
    [InlineData(2, "100.0OHM", "100.0")]
    [InlineData(3, "0.125W", "0.125")]
    public async Task ReadSetpoint_UsesSelectedProductionMapping(
        int index,
        string response,
        string expected)
    {
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero));
        var session = new FakeSession(response);
        await using var adapter = new Kel103ReadOnlySessionAdapter(session, time);

        Kel103SetpointObservation result = await adapter.ReadSetpointAsync(
            Kel103SetpointMapping.All[index]);

        Assert.Equal((Kel103Setpoint)index, result.Setpoint);
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), result.Value);
        Assert.Equal(time.GetUtcNow(), result.TimestampUtc);
        Assert.Equal(Kel103SetpointMapping.All[index].Query, Assert.Single(session.Queries));
    }

    [Fact]
    public async Task InvalidOperatingState_FaultsAndPreventsLaterQuery()
    {
        var session = new FakeSession("WRONG");
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);

        await Assert.ThrowsAsync<InvalidDataException>(() => adapter.ReadOperatingModeAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.ReadInputStateAsync());

        Assert.True(adapter.IsFaulted);
        Assert.Equal(new[] { ":FUNCtion?" }, session.Queries);
    }

    [Fact]
    public async Task PreCanceledOperatingStateSynchronization_SendsNoQuery()
    {
        var session = new FakeSession();
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            adapter.VerifyAndSynchronizeOperatingStateAsync(cancellation.Token));

        Assert.Empty(session.Queries);
        Assert.False(adapter.IsFaulted);
    }

    [Fact]
    public async Task OperatingStateSynchronization_DoesNotInterleaveWithConcurrentRead()
    {
        var session = new OperatingStateBlockingSession();
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);

        Task<Kel103OperatingStateSynchronizationSnapshot> synchronization =
            adapter.VerifyAndSynchronizeOperatingStateAsync();
        await session.FirstQueryStarted.Task;
        Task<Kel103InputStateObservation> concurrentRead = adapter.ReadInputStateAsync();

        Assert.Equal(new[] { "*IDN?" }, session.Queries);
        session.ReleaseFirstQuery.SetResult();
        await synchronization;
        await concurrentRead;

        Assert.Equal(
            new[]
            {
                "*IDN?",
                ":MEASure:VOLTage?",
                ":MEASure:CURRent?",
                ":MEASure:POWer?",
                ":FUNCtion?",
                ":INPut?",
                ":VOLTage?",
                ":CURRent?",
                ":RESistance?",
                ":POWer?",
                ":INPut?"
            },
            session.Queries);
    }

    [Theory]
    [InlineData(0, "1.25V", "CV", ":VOLTage 1.25V", "1.25", Kel103OperatingMode.ConstantVoltage)]
    [InlineData(1, "0.1A", "CC", ":CURRent 0.1A", "0.1", Kel103OperatingMode.ConstantCurrent)]
    [InlineData(2, "100OHM", "CR", ":RESistance 100OHM", "100", Kel103OperatingMode.ConstantResistance)]
    [InlineData(3, "0.125W", "CW", ":POWer 0.125W", "0.125", Kel103OperatingMode.ConstantPower)]
    public async Task WriteSetpoint_UsesExactSequenceAndReturnsAuthoritativeResult(
        int index,
        string readback,
        string modeReadback,
        string expectedCommand,
        string expectedValue,
        Kel103OperatingMode expectedMode)
    {
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero));
        var session = new FakeSession("OFF", readback, modeReadback);
        await using var adapter = new Kel103ReadOnlySessionAdapter(session, time);

        Kel103SetpointMutationResult result = await adapter.WriteSetpointAsync(
            Kel103SetpointMapping.All[index],
            decimal.Parse(expectedValue, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal((Kel103Setpoint)index, result.Setpoint);
        Assert.Equal(
            decimal.Parse(expectedValue, System.Globalization.CultureInfo.InvariantCulture),
            result.Value);
        Assert.Equal(expectedMode, result.OperatingMode);
        Assert.Equal(time.GetUtcNow(), result.TimestampUtc);
        Assert.Equal(new[] { ":INPut?", Kel103SetpointMapping.All[index].Query, ":FUNCtion?" }, session.Queries);
        Assert.Equal(new[] { expectedCommand }, session.Commands);
        Assert.False(adapter.IsFaulted);
    }

    [Fact]
    public async Task WriteSetpoint_InputOnRejectsWithoutSetterAndSessionRemainsUsable()
    {
        var session = new FakeSession("ON", "OFF");
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adapter.WriteSetpointAsync(Kel103SetpointMapping.Current, 0.1m));

        Assert.Equal(new[] { ":INPut?" }, session.Queries);
        Assert.Empty(session.Commands);
        Assert.False(adapter.IsFaulted);

        Kel103InputStateObservation later = await adapter.ReadInputStateAsync();
        Assert.False(later.InputEnabled);
        Assert.Equal(new[] { ":INPut?", ":INPut?" }, session.Queries);
    }

    [Fact]
    public async Task WriteSetpoint_InvalidInputUsesNoScpiAndSessionRemainsUsable()
    {
        var session = new FakeSession("OFF");
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            adapter.WriteSetpointAsync(null!, 1m));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            adapter.WriteSetpointAsync(Kel103SetpointMapping.Voltage, 0.09m));

        Assert.Empty(session.Queries);
        Assert.Empty(session.Commands);
        Assert.False(adapter.IsFaulted);
        Assert.False((await adapter.ReadInputStateAsync()).InputEnabled);
    }

    [Fact]
    public async Task WriteSetpoint_PreCanceledUsesNoScpiAndDoesNotFault()
    {
        var session = new FakeSession();
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            adapter.WriteSetpointAsync(
                Kel103SetpointMapping.Current,
                0.1m,
                cancellation.Token));

        Assert.Empty(session.Queries);
        Assert.Empty(session.Commands);
        Assert.False(adapter.IsFaulted);
    }

    [Fact]
    public async Task WriteSetpoint_TransmissionUncertaintyIsPreservedAndFaultsWithoutRetry()
    {
        var transmission = new ScpiCommandTransmissionException(
            "uncertain",
            true,
            new IOException("write failed"));
        var session = new FakeSession("OFF") { SendException = transmission };
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);

        ScpiCommandTransmissionException actual =
            await Assert.ThrowsAsync<ScpiCommandTransmissionException>(() =>
                adapter.WriteSetpointAsync(Kel103SetpointMapping.Current, 0.1m));

        Assert.Same(transmission, actual);
        Assert.True(actual.ExecutionMayHaveOccurred);
        Assert.True(adapter.IsFaulted);
        Assert.Single(session.Commands);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adapter.WriteSetpointAsync(Kel103SetpointMapping.Current, 0.1m));
        Assert.Single(session.Commands);
    }

    [Fact]
    public async Task WriteSetpoint_TargetQueryFailureAfterTransmissionIsUncertain()
    {
        var session = new FakeSession("OFF")
        {
            FailingQueryNumber = 2,
            QueryException = new TimeoutException("readback timeout")
        };
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);

        Kel103MutationOutcomeUncertainException actual =
            await Assert.ThrowsAsync<Kel103MutationOutcomeUncertainException>(() =>
                adapter.WriteSetpointAsync(Kel103SetpointMapping.Current, 0.1m));

        Assert.True(actual.ExecutionMayHaveOccurred);
        Assert.IsType<TimeoutException>(actual.InnerException);
        Assert.True(adapter.IsFaulted);
        Assert.Single(session.Commands);
    }

    [Fact]
    public async Task WriteSetpoint_ModeParsingFailureAfterTransmissionIsUncertain()
    {
        var session = new FakeSession("OFF", "0.1A", "WRONG");
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);

        Kel103MutationOutcomeUncertainException actual =
            await Assert.ThrowsAsync<Kel103MutationOutcomeUncertainException>(() =>
                adapter.WriteSetpointAsync(Kel103SetpointMapping.Current, 0.1m));

        Assert.IsType<InvalidDataException>(actual.InnerException);
        Assert.True(adapter.IsFaulted);
        Assert.Single(session.Commands);
    }

    [Fact]
    public async Task WriteSetpoint_TargetMismatchAfterTransmissionIsUncertain()
    {
        var session = new FakeSession("OFF", "0.2A", "CC");
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);

        await Assert.ThrowsAsync<Kel103MutationOutcomeUncertainException>(() =>
            adapter.WriteSetpointAsync(Kel103SetpointMapping.Current, 0.1m));

        Assert.True(adapter.IsFaulted);
        Assert.Single(session.Commands);
    }

    [Fact]
    public async Task WriteSetpoint_ModeMismatchAfterTransmissionIsUncertain()
    {
        var session = new FakeSession("OFF", "0.1A", "CV");
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);

        await Assert.ThrowsAsync<Kel103MutationOutcomeUncertainException>(() =>
            adapter.WriteSetpointAsync(Kel103SetpointMapping.Current, 0.1m));

        Assert.True(adapter.IsFaulted);
        Assert.Single(session.Commands);
    }

    [Fact]
    public async Task WriteSetpoint_CancellationAfterTransmissionIsUncertain()
    {
        using var cancellation = new CancellationTokenSource();
        var session = new FakeSession("OFF")
        {
            CommandSent = cancellation.Cancel
        };
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);

        Kel103MutationOutcomeUncertainException actual =
            await Assert.ThrowsAsync<Kel103MutationOutcomeUncertainException>(() =>
                adapter.WriteSetpointAsync(
                    Kel103SetpointMapping.Current,
                    0.1m,
                    cancellation.Token));

        Assert.IsAssignableFrom<OperationCanceledException>(actual.InnerException);
        Assert.True(adapter.IsFaulted);
        Assert.Single(session.Commands);
    }

    [Fact]
    public async Task WriteSetpoint_DoesNotInterleaveWithConcurrentRead()
    {
        var session = new BlockingMutationSession();
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);

        Task<Kel103SetpointMutationResult> mutation = adapter.WriteSetpointAsync(
            Kel103SetpointMapping.Current,
            0.1m);
        await session.FirstQueryStarted.Task;
        Task<Kel103Identity> concurrentRead = adapter.ReadIdentityAsync();

        Assert.Equal(new[] { ":INPut?" }, session.Queries);
        session.ReleaseFirstQuery.SetResult();
        await mutation;
        await concurrentRead;

        Assert.Equal(
            new[] { ":INPut?", ":CURRent?", ":FUNCtion?", "*IDN?" },
            session.Queries);
        Assert.Equal(new[] { ":CURRent 0.1A" }, session.Commands);
    }

    [Theory]
    [InlineData(0, "CC")]
    [InlineData(1, "CV")]
    [InlineData(2, "CR")]
    [InlineData(3, "CW")]
    [InlineData(4, "SHORt")]
    public async Task SelectOperatingMode_UsesExactSequenceAndReturnsAuthoritativeResult(
        int index,
        string modeReadback)
    {
        var time = new FixedTimeProvider(
            new DateTimeOffset(2026, 8, 5, 12, 0, 0, TimeSpan.Zero));
        var session = new FakeSession("OFF", "OFF", modeReadback);
        await using var adapter = new Kel103ReadOnlySessionAdapter(session, time);

        Kel103ModeSelectionMapping mapping = Kel103ModeSelectionMapping.All[index];
        Kel103ModeSelectionMutationResult result =
            await adapter.SelectOperatingModeAsync(mapping);

        Assert.Equal(mapping.Mode, result.OperatingMode);
        Assert.False(result.InputEnabled);
        Assert.Equal(time.GetUtcNow(), result.TimestampUtc);
        Assert.Equal(
            new[] { ":INPut?", ":INPut?", ":FUNCtion?" },
            session.Queries);
        Assert.Equal(new[] { mapping.Command }, session.Commands);
        Assert.False(adapter.IsFaulted);
    }

    [Fact]
    public async Task SelectOperatingMode_InputOnRejectsWithoutMutationAndSessionRemainsUsable()
    {
        var session = new FakeSession("ON", "OFF");
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adapter.SelectOperatingModeAsync(
                Kel103ModeSelectionMapping.ConstantVoltage));

        Assert.Equal(new[] { ":INPut?" }, session.Queries);
        Assert.Empty(session.Commands);
        Assert.False(adapter.IsFaulted);

        Kel103InputStateObservation later = await adapter.ReadInputStateAsync();
        Assert.False(later.InputEnabled);
        Assert.Equal(new[] { ":INPut?", ":INPut?" }, session.Queries);
    }

    [Fact]
    public async Task SelectOperatingMode_NullMappingUsesNoScpiAndSessionRemainsUsable()
    {
        var session = new FakeSession("OFF");
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            adapter.SelectOperatingModeAsync(null!));

        Assert.Empty(session.Queries);
        Assert.Empty(session.Commands);
        Assert.False(adapter.IsFaulted);
        Assert.False((await adapter.ReadInputStateAsync()).InputEnabled);
    }

    [Fact]
    public async Task SelectOperatingMode_PreCanceledUsesNoScpiAndDoesNotFault()
    {
        var session = new FakeSession();
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            adapter.SelectOperatingModeAsync(
                Kel103ModeSelectionMapping.ConstantResistance,
                cancellation.Token));

        Assert.Empty(session.Queries);
        Assert.Empty(session.Commands);
        Assert.False(adapter.IsFaulted);
    }

    [Fact]
    public async Task SelectOperatingMode_TransmissionUncertaintyIsPreservedWithoutRetry()
    {
        var transmission = new ScpiCommandTransmissionException(
            "uncertain",
            true,
            new IOException("write failed"));
        var session = new FakeSession("OFF") { SendException = transmission };
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);

        ScpiCommandTransmissionException actual =
            await Assert.ThrowsAsync<ScpiCommandTransmissionException>(() =>
                adapter.SelectOperatingModeAsync(
                    Kel103ModeSelectionMapping.ConstantPower));

        Assert.Same(transmission, actual);
        Assert.True(actual.ExecutionMayHaveOccurred);
        Assert.True(adapter.IsFaulted);
        Assert.Single(session.Commands);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adapter.SelectOperatingModeAsync(
                Kel103ModeSelectionMapping.ConstantPower));
        Assert.Single(session.Commands);
    }

    [Fact]
    public async Task SelectOperatingMode_InputQueryFailureAfterTransmissionIsUncertain()
    {
        var session = new FakeSession("OFF")
        {
            FailingQueryNumber = 2,
            QueryException = new TimeoutException("readback timeout")
        };
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);

        Kel103MutationOutcomeUncertainException actual =
            await Assert.ThrowsAsync<Kel103MutationOutcomeUncertainException>(() =>
                adapter.SelectOperatingModeAsync(
                    Kel103ModeSelectionMapping.ConstantVoltage));

        Assert.True(actual.ExecutionMayHaveOccurred);
        Assert.IsType<TimeoutException>(actual.InnerException);
        Assert.True(adapter.IsFaulted);
        Assert.Single(session.Commands);
    }

    [Fact]
    public async Task SelectOperatingMode_InputOnAfterTransmissionIsUncertain()
    {
        var session = new FakeSession("OFF", "ON", "CV");
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);

        await Assert.ThrowsAsync<Kel103MutationOutcomeUncertainException>(() =>
            adapter.SelectOperatingModeAsync(
                Kel103ModeSelectionMapping.ConstantVoltage));

        Assert.True(adapter.IsFaulted);
        Assert.Single(session.Commands);
    }

    [Fact]
    public async Task SelectOperatingMode_ModeParsingFailureAfterTransmissionIsUncertain()
    {
        var session = new FakeSession("OFF", "OFF", "WRONG");
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);

        Kel103MutationOutcomeUncertainException actual =
            await Assert.ThrowsAsync<Kel103MutationOutcomeUncertainException>(() =>
                adapter.SelectOperatingModeAsync(
                    Kel103ModeSelectionMapping.ShortCircuit));

        Assert.IsType<InvalidDataException>(actual.InnerException);
        Assert.True(adapter.IsFaulted);
        Assert.Single(session.Commands);
    }

    [Fact]
    public async Task SelectOperatingMode_ModeMismatchAfterTransmissionIsUncertain()
    {
        var session = new FakeSession("OFF", "OFF", "CR");
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);

        await Assert.ThrowsAsync<Kel103MutationOutcomeUncertainException>(() =>
            adapter.SelectOperatingModeAsync(
                Kel103ModeSelectionMapping.ConstantVoltage));

        Assert.True(adapter.IsFaulted);
        Assert.Single(session.Commands);
    }

    [Fact]
    public async Task SelectOperatingMode_CancellationAfterTransmissionIsUncertain()
    {
        using var cancellation = new CancellationTokenSource();
        var session = new FakeSession("OFF")
        {
            CommandSent = cancellation.Cancel
        };
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);

        Kel103MutationOutcomeUncertainException actual =
            await Assert.ThrowsAsync<Kel103MutationOutcomeUncertainException>(() =>
                adapter.SelectOperatingModeAsync(
                    Kel103ModeSelectionMapping.ConstantCurrent,
                    cancellation.Token));

        Assert.IsAssignableFrom<OperationCanceledException>(actual.InnerException);
        Assert.True(adapter.IsFaulted);
        Assert.Single(session.Commands);
    }

    [Fact]
    public async Task SelectOperatingMode_DoesNotInterleaveWithConcurrentRead()
    {
        var session = new BlockingModeSelectionSession();
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);

        Task<Kel103ModeSelectionMutationResult> mutation =
            adapter.SelectOperatingModeAsync(
                Kel103ModeSelectionMapping.ConstantVoltage);
        await session.FirstQueryStarted.Task;
        Task<Kel103Identity> concurrentRead = adapter.ReadIdentityAsync();

        Assert.Equal(new[] { ":INPut?" }, session.Queries);
        session.ReleaseFirstQuery.SetResult();
        await mutation;
        await concurrentRead;

        Assert.Equal(
            new[] { ":INPut?", ":INPut?", ":FUNCtion?", "*IDN?" },
            session.Queries);
        Assert.Equal(new[] { ":FUNCtion CV" }, session.Commands);
    }

    [Fact]
    public async Task InvalidMeasurement_FaultsAndPreventsLaterQuery()
    {
        var session = new FakeSession("1.0A");
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            adapter.ReadMeasurementAsync(Kel103MeasurementMapping.Voltage));
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.ReadIdentityAsync());
        Assert.True(adapter.IsFaulted);
        Assert.Single(session.Queries);
    }

    [Fact]
    public async Task WrongIdentity_FaultsWithoutMeasurementQueries()
    {
        var session = new FakeSession("RND OTHER V3.30 SN:REDACTED");
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);
        await Assert.ThrowsAsync<InvalidDataException>(() => adapter.VerifyAndSynchronizeAsync());
        Assert.Equal(new[] { "*IDN?" }, session.Queries);
        Assert.True(adapter.IsFaulted);
    }

    [Fact]
    public async Task PreCanceledOperation_SendsNoQuery()
    {
        var session = new FakeSession();
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            adapter.ReadIdentityAsync(cancellation.Token));
        Assert.Empty(session.Queries);
        Assert.False(adapter.IsFaulted);
    }

    [Fact]
    public async Task Dispose_DisposesSessionOnceAndRejectsReuse()
    {
        var session = new FakeSession();
        var adapter = new Kel103ReadOnlySessionAdapter(session);
        await adapter.DisposeAsync();
        await adapter.DisposeAsync();
        Assert.Equal(1, session.DisposeCount);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => adapter.ReadIdentityAsync());
    }

    [Fact]
    public async Task Synchronization_DoesNotInterleaveWithConcurrentRead()
    {
        var session = new BlockingSession();
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);

        Task<Kel103SynchronizationSnapshot> synchronization = adapter.VerifyAndSynchronizeAsync();
        await session.FirstQueryStarted.Task;
        Task<Kel103Identity> concurrentRead = adapter.ReadIdentityAsync();

        Assert.Equal(new[] { "*IDN?" }, session.Queries);
        session.ReleaseFirstQuery.SetResult();
        await synchronization;
        await concurrentRead;

        Assert.Equal(
            new[] { "*IDN?", ":MEASure:VOLTage?", ":MEASure:CURRent?", ":MEASure:POWer?", "*IDN?" },
            session.Queries);
    }

    [Fact]
    public async Task NullDependencies_AreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new Kel103ReadOnlySessionAdapter(null!));
        var session = new FakeSession();
        await using var adapter = new Kel103ReadOnlySessionAdapter(session);
        await Assert.ThrowsAsync<ArgumentNullException>(() => adapter.ReadMeasurementAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => adapter.ReadSetpointAsync(null!));
    }

    [Fact]
    public void Assembly_DoesNotReferenceDeferredLayers()
    {
        string[] references = typeof(Kel103ReadOnlySessionAdapter).Assembly
            .GetReferencedAssemblies().Select(value => value.Name ?? string.Empty).ToArray();
        Assert.DoesNotContain(references, name => name == "Hase.Transport");
        Assert.DoesNotContain(references, name => name == "Hase.Runtime.Transport");
        Assert.DoesNotContain(references, name => name.Contains("Grpc", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.Contains("Wpf", StringComparison.Ordinal));
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class FakeSession(params string[] responses) : IScpiTextSession
    {
        private readonly Queue<string> pending = new(responses);
        public List<string> Queries { get; } = [];
        public List<string> Commands { get; } = [];
        public int DisposeCount { get; private set; }
        public int? FailingQueryNumber { get; init; }
        public Exception? QueryException { get; init; }
        public Exception? SendException { get; init; }
        public Action? CommandSent { get; init; }
        public ScpiTextSessionState State => DisposeCount == 0 ? ScpiTextSessionState.Open : ScpiTextSessionState.Disposed;
        public Task<string> QueryAsync(string query, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Queries.Add(query);
            if (Queries.Count == FailingQueryNumber)
            {
                return Task.FromException<string>(
                    QueryException ?? new IOException("query failed"));
            }
            return Task.FromResult(pending.Dequeue());
        }
        public Task SendCommandAsync(string command, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Commands.Add(command);
            CommandSent?.Invoke();
            return SendException is null
                ? Task.CompletedTask
                : Task.FromException(SendException);
        }
        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class BlockingSession : IScpiTextSession
    {
        private readonly Queue<string> responses = new(
            ["RND 320-KEL103 V3.30 SN:REDACTED", "1V", "0.1A", "0.1W", "RND 320-KEL103 V3.30 SN:REDACTED"]);
        public TaskCompletionSource FirstQueryStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFirstQuery { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<string> Queries { get; } = [];
        public ScpiTextSessionState State => ScpiTextSessionState.Open;
        public async Task<string> QueryAsync(string query, CancellationToken cancellationToken = default)
        {
            Queries.Add(query);
            if (Queries.Count == 1)
            {
                FirstQueryStarted.SetResult();
                await ReleaseFirstQuery.Task.WaitAsync(cancellationToken);
            }
            return responses.Dequeue();
        }
        public Task SendCommandAsync(string command, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class OperatingStateBlockingSession : IScpiTextSession
    {
        private readonly Queue<string> responses = new(
        [
            "RND 320-KEL103 V3.30 SN:REDACTED",
            "1V",
            "0.1A",
            "0.1W",
            "CC",
            "OFF",
            "1V",
            "0.1A",
            "1OHM",
            "0.1W",
            "OFF"
        ]);

        public TaskCompletionSource FirstQueryStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseFirstQuery { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<string> Queries { get; } = [];

        public ScpiTextSessionState State => ScpiTextSessionState.Open;

        public async Task<string> QueryAsync(
            string query,
            CancellationToken cancellationToken = default)
        {
            Queries.Add(query);
            if (Queries.Count == 1)
            {
                FirstQueryStarted.SetResult();
                await ReleaseFirstQuery.Task.WaitAsync(cancellationToken);
            }

            return responses.Dequeue();
        }

        public Task SendCommandAsync(
            string command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class BlockingMutationSession : IScpiTextSession
    {
        private readonly Queue<string> responses = new(
        [
            "OFF",
            "0.1A",
            "CC",
            "RND 320-KEL103 V3.30 SN:REDACTED"
        ]);

        public TaskCompletionSource FirstQueryStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseFirstQuery { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<string> Queries { get; } = [];

        public List<string> Commands { get; } = [];

        public ScpiTextSessionState State => ScpiTextSessionState.Open;

        public async Task<string> QueryAsync(
            string query,
            CancellationToken cancellationToken = default)
        {
            Queries.Add(query);
            if (Queries.Count == 1)
            {
                FirstQueryStarted.SetResult();
                await ReleaseFirstQuery.Task.WaitAsync(cancellationToken);
            }

            return responses.Dequeue();
        }

        public Task SendCommandAsync(
            string command,
            CancellationToken cancellationToken = default)
        {
            Commands.Add(command);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class BlockingModeSelectionSession : IScpiTextSession
    {
        private readonly Queue<string> responses = new(
        [
            "OFF",
            "OFF",
            "CV",
            "RND 320-KEL103 V3.30 SN:REDACTED"
        ]);

        public TaskCompletionSource FirstQueryStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseFirstQuery { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<string> Queries { get; } = [];

        public List<string> Commands { get; } = [];

        public ScpiTextSessionState State => ScpiTextSessionState.Open;

        public async Task<string> QueryAsync(
            string query,
            CancellationToken cancellationToken = default)
        {
            Queries.Add(query);
            if (Queries.Count == 1)
            {
                FirstQueryStarted.SetResult();
                await ReleaseFirstQuery.Task.WaitAsync(cancellationToken);
            }

            return responses.Dequeue();
        }

        public Task SendCommandAsync(
            string command,
            CancellationToken cancellationToken = default)
        {
            Commands.Add(command);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
