using Hase.Client;
using Hase.Client.Wpf.RfLab.ViewModels;
using Hase.Client.Wpf.Services;

namespace Hase.Client.Wpf.RfLab.Tests;

public sealed class RfLabPanelViewModelTests
{
    [Fact]
    public async Task InitializeAsync_ShouldReadIdentityStateAndDetector()
    {
        (RfLabPanelViewModel panel, RecordingInstrumentOperations operations) =
            CreatePanel();
        operations.SetRead("product-identity", RemoteValue.FromString("RF-Lab"));
        operations.SetRead("node-type", RemoteValue.FromString("AE.70.10.80"));
        operations.SetRead("clock-generator-present", RemoteValue.FromBoolean(true));
        operations.SetRead("indicator-enabled", RemoteValue.FromBoolean(true));
        operations.SetRead("sensor-level", RemoteValue.FromNumeric(-42.5));

        await panel.InitializeAsync();

        Assert.Equal("RF-Lab", panel.ProductIdentity);
        Assert.Equal("AE.70.10.80", panel.NodeType);
        Assert.Equal("Clock generator: present", panel.ClockGeneratorState);
        Assert.True(panel.LED);
        Assert.Equal("-42,5", panel.SensorValueString.Replace('.', ','));
        Assert.False(panel.ErrorStatus);
        Assert.Single(panel.MeasurementData);
    }

    [Fact]
    public async Task SelectingCarrierMode_ShouldStageTargetsThenApplyOnce()
    {
        (RfLabPanelViewModel panel, RecordingInstrumentOperations operations) =
            CreatePanel();
        // A dial change applies at once, as on the original panel.
        panel.Frequency = 145_000_000;
        await WaitForExecutionAsync(operations);
        panel.Amplitude = 30;
        await WaitForExecutionAsync(operations, expected: 2);

        Assert.Equal(
            [("target-frequency", 145_000_000d), ("target-attenuation", 30d)],
            operations.Writes.TakeLast(2));
        Assert.All(
            operations.Executions,
            execution => Assert.Equal("Signal.ApplyCarrier", execution));
        Assert.False(panel.ErrorStatus);
    }

    [Fact]
    public async Task SelectingAmplitudeModulation_ShouldStageItsFourTargets()
    {
        (RfLabPanelViewModel panel, RecordingInstrumentOperations operations) =
            CreatePanel();
        panel.Frequency = 10_000_000;
        panel.Amplitude = 20;
        panel.ModulationFrequency = 1_000;
        panel.AmplitudeModulationDepth = 80;

        panel.DDS_ModMode = (int)RfLabSignalMode.AmplitudeModulation;
        await WaitForExecutionAsync(operations);

        Assert.Equal(
            [
                ("target-frequency", 10_000_000d),
                ("target-attenuation", 20d),
                ("modulation-frequency", 1_000d),
                ("am-depth", 80d)
            ],
            operations.Writes);
        Assert.Equal(["Signal.ApplyAmplitudeModulation"], operations.Executions);
        Assert.True(panel.IsModeAM);
        Assert.True(panel.IsFModEnabled);
    }

    [Fact]
    public async Task StartingASweep_ShouldUseTheSelectedRampCommand()
    {
        (RfLabPanelViewModel panel, RecordingInstrumentOperations operations) =
            CreatePanel();
        panel.DDS_ModMode = (int)RfLabSignalMode.Sweep;
        panel.SelectedSweepMode = "SingleRamp";
        panel.SweepStartFrequency = 10_000_000;
        panel.SweepStopFrequency = 30_000_000;
        panel.SweepTime = 2_000;
        panel.Amplitude = 25;

        panel.IsSweepActive = true;
        await WaitForExecutionAsync(operations);

        Assert.Equal(["Signal.StartSweepSingleRamp"], operations.Executions);
        Assert.Equal(
            [
                ("sweep-start-frequency", 10_000_000d),
                ("sweep-stop-frequency", 30_000_000d),
                ("sweep-time", 2_000d),
                ("target-attenuation", 25d)
            ],
            operations.Writes);
        Assert.False(panel.IsSweepInactive);
    }

    [Fact]
    public async Task ARejectedCommand_ShouldRaiseTheErrorStatusWithoutThrowing()
    {
        (RfLabPanelViewModel panel, RecordingInstrumentOperations operations) =
            CreatePanel();
        operations.FailNextCommand = true;

        panel.Frequency = 20_000_000;
        await WaitForExecutionAsync(operations);

        Assert.True(panel.ErrorStatus);
        Assert.Contains("EndpointRejected", panel.StatusInfo, StringComparison.Ordinal);
        Assert.True(panel.IsUIEnabled);
    }

    [Fact]
    public async Task ToggleIndicator_ShouldSwitchTheOppositeWayAndTrackState()
    {
        (RfLabPanelViewModel panel, RecordingInstrumentOperations operations) =
            CreatePanel();
        operations.SetRead("indicator-enabled", RemoteValue.FromBoolean(false));
        operations.SetRead("product-identity", RemoteValue.FromString("RF-Lab"));
        operations.SetRead("node-type", RemoteValue.FromString("AE.70.10.80"));
        operations.SetRead("clock-generator-present", RemoteValue.FromBoolean(false));
        operations.SetRead("sensor-level", RemoteValue.FromNumeric(-70));
        await panel.InitializeAsync();

        panel.ToggleIndicatorCommand.Execute(null);
        await WaitForExecutionAsync(operations);

        Assert.Equal(["Indicator.SwitchOn"], operations.Executions);
        Assert.True(panel.LED);
    }

    [Fact]
    public void MeasureMode_ShouldStartAndStopThePeriodicRead()
    {
        var scheduler = new RecordingScheduler();
        (RfLabPanelViewModel panel, _) = CreatePanel(scheduler);

        panel.DDS_ModMode = (int)RfLabSignalMode.Measure;

        Assert.True(panel.IsMeasurementActive);
        Assert.Equal(1, scheduler.ScheduleCount);
        Assert.False(scheduler.LastSubscriptionDisposed);

        panel.DDS_ModMode = (int)RfLabSignalMode.Off;

        Assert.False(panel.IsMeasurementActive);
        Assert.True(scheduler.LastSubscriptionDisposed);
    }

    [Fact]
    public void Dispose_ShouldStopThePeriodicRead()
    {
        var scheduler = new RecordingScheduler();
        (RfLabPanelViewModel panel, _) = CreatePanel(scheduler);
        panel.IsMeasurementActive = true;

        panel.Dispose();

        Assert.True(scheduler.LastSubscriptionDisposed);
    }

    [Fact]
    public void AxisBounds_ShouldFollowTheSelectedMode()
    {
        (RfLabPanelViewModel panel, _) = CreatePanel();
        panel.SweepStartFrequency = 10_000_000;
        panel.SweepStopFrequency = 30_000_000;

        panel.DDS_ModMode = (int)RfLabSignalMode.Measure;
        Assert.Equal("n", panel.Xlabel);
        Assert.Equal(0, panel.Xmin);
        Assert.Equal(500, panel.Xmax);

        panel.DDS_ModMode = (int)RfLabSignalMode.Sweep;
        Assert.Equal("f", panel.Xlabel);
        Assert.Equal(10_000_000, panel.Xmin);
        Assert.Equal(30_000_000, panel.Xmax);
    }

    [Fact]
    public void Sensors_ShouldOfferTheHostConvertedReadings()
    {
        (RfLabPanelViewModel panel, _) = CreatePanel();

        Assert.Equal(2, panel.Sensors.Count);
        Assert.Equal("sensor-level", panel.Sensors[0].PropertyId);
        Assert.Equal("dB", panel.Sensors[0].Units);
        Assert.Equal("sensor-voltage", panel.Sensors[1].PropertyId);
        Assert.All(panel.Sensors, sensor => Assert.False(sensor.NeedToBeCalibrated));
        Assert.Equal(
            ["Bidirectional", "Ramp", "SingleRamp"],
            panel.SweepModes);
    }

    private static (RfLabPanelViewModel Panel, RecordingInstrumentOperations Operations)
        CreatePanel(IRfLabPanelScheduler? scheduler = null)
    {
        var operations = new RecordingInstrumentOperations();
        var panel = new RfLabPanelViewModel(
            new ClientInstrumentPanelContext(
                "rf-lab-signal-lab",
                "rf-minilab-01",
                "rf-minilab-01",
                "RF Signal Lab",
                operations),
            scheduler ?? new RecordingScheduler());

        return (panel, operations);
    }

    private static async Task WaitForExecutionAsync(
        RecordingInstrumentOperations operations,
        int expected = 1)
    {
        for (int attempt = 0;
            attempt < 50 && operations.Executions.Count < expected;
            attempt++)
        {
            await Task.Delay(10);
        }
    }

    private sealed class RecordingScheduler : IRfLabPanelScheduler
    {
        public int ScheduleCount { get; private set; }

        public bool LastSubscriptionDisposed { get; private set; }

        public IDisposable Schedule(TimeSpan interval, Func<Task> operation)
        {
            ScheduleCount++;
            LastSubscriptionDisposed = false;
            return new Subscription(() => LastSubscriptionDisposed = true);
        }

        private sealed class Subscription(Action onDispose) : IDisposable
        {
            public void Dispose() => onDispose();
        }
    }
}
