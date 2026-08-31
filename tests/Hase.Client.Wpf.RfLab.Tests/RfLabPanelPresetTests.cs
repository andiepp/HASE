using Hase.Client.Wpf.RfLab.Presets;
using Hase.Client.Wpf.RfLab.ViewModels;
using Hase.Client.Wpf.Services;

namespace Hase.Client.Wpf.RfLab.Tests;

/// <summary>
/// Selecting a stored setting loads it into the panel and applies it, as
/// the original panel did. What the file does not say leaves the panel's
/// own value alone.
/// </summary>
public sealed class RfLabPanelPresetTests
{
    [Fact]
    public void ThePanel_ShouldOfferWhatTheStoreLists()
    {
        (RfLabPanelViewModel panel, _, _) = CreatePanel(
            ("bench", ["Frequency,21400000"]),
            ("sweep", ["Frequency,10000000"]));

        Assert.Equal(["bench", "sweep"], panel.SettingsFiles);
        Assert.Null(panel.SelectedSettingsFile);
        Assert.False(panel.CanApplyPreset);
    }

    [Fact]
    public void ThePanel_ShouldOpenWithoutAStore()
    {
        var operations = new RecordingInstrumentOperations();
        var panel = new RfLabPanelViewModel(
            Context(operations),
            new RfLabPanelViewModelTests.RecordingScheduler());

        // A client that ships no presets must still open the panel.
        Assert.Empty(panel.SettingsFiles);
        Assert.False(panel.CanApplyPreset);
    }

    [Fact]
    public async Task SelectingAPreset_ShouldLoadItAndApplyOnce()
    {
        (RfLabPanelViewModel panel, RecordingInstrumentOperations operations, _) =
            CreatePanel(
                ("bench",
                [
                    "Mode,0",
                    "Frequency,21400000",
                    "Amplitude,40",
                    "Fstart,1000000",
                    "Fstop,5000000",
                    "Tsweep,4000",
                    "Tmeasure,250",
                    "Nmeasure,300",
                    "SweepMode,Ramp",
                    "Sensor,AD_8"
                ]));

        panel.SelectedSettingsFile = "bench";
        await WaitForAsync(() => operations.Executions.Count > 0);
        await Task.Delay(50);

        Assert.Equal(21_400_000, panel.Frequency);
        Assert.Equal(40, panel.Amplitude);
        Assert.Equal(1_000_000, panel.SweepStartFrequency);
        Assert.Equal(5_000_000, panel.SweepStopFrequency);
        Assert.Equal(4_000, panel.SweepTime);
        Assert.Equal(250, panel.MeasurementInterval);
        Assert.Equal(300, panel.MeasurementCount);
        Assert.Equal("Ramp", panel.SelectedSweepMode);
        Assert.Equal("AD_8", panel.SelectedSensor.Name);
        Assert.Contains("applied", panel.StatusInfo, StringComparison.Ordinal);
        Assert.False(panel.ErrorStatus);

        // The whole preset is staged before the mode applies it, so the
        // instrument is commanded once rather than once per field.
        Assert.Single(operations.Executions);
        Assert.Equal("Signal.ApplyCarrier", operations.Executions[0]);
    }

    [Fact]
    public async Task APresetWithClocks_ShouldApplyEachChannel()
    {
        (RfLabPanelViewModel panel, RecordingInstrumentOperations operations, _) =
            CreatePanel(
                ("clocks",
                [
                    "SI5351Fclk0,4000000",
                    "SI5351Fclk1,5000000",
                    "SI5351Fclk2,6000000"
                ]));

        panel.SelectedSettingsFile = "clocks";
        await WaitForAsync(() => operations.Executions.Count >= 3);
        await Task.Delay(50);

        Assert.Equal(4_000_000, panel.ClockFrequency0);
        Assert.Equal(5_000_000, panel.ClockFrequency1);
        Assert.Equal(6_000_000, panel.ClockFrequency2);
        Assert.Contains("Clock.ApplyOutput0", operations.Executions);
        Assert.Contains("Clock.ApplyOutput1", operations.Executions);
        Assert.Contains("Clock.ApplyOutput2", operations.Executions);
    }

    [Fact]
    public async Task AValueThePresetOmits_ShouldLeaveThePanelUnchanged()
    {
        (RfLabPanelViewModel panel, _, _) = CreatePanel(
            ("partial", ["Frequency,15000000"]));
        panel.Amplitude = 33;
        int untouchedSweepTime = panel.SweepTime;

        panel.SelectedSettingsFile = "partial";
        await Task.Delay(50);

        Assert.Equal(15_000_000, panel.Frequency);

        // Absent is not zero.
        Assert.Equal(33, panel.Amplitude);
        Assert.Equal(untouchedSweepTime, panel.SweepTime);
    }

    [Fact]
    public async Task AnUnknownSweepModeOrSensor_ShouldBeIgnored()
    {
        (RfLabPanelViewModel panel, _, _) = CreatePanel(
            ("odd", ["SweepMode,Helical", "Sensor,ThermocoupleOfTheFuture"]));
        string sweepMode = panel.SelectedSweepMode;
        string sensor = panel.SelectedSensor.Name;

        panel.SelectedSettingsFile = "odd";
        await Task.Delay(50);

        Assert.Equal(sweepMode, panel.SelectedSweepMode);
        Assert.Equal(sensor, panel.SelectedSensor.Name);
        Assert.False(panel.ErrorStatus);
    }

    [Fact]
    public async Task AnUnreadablePreset_ShouldReportRatherThanApplyNothingSilently()
    {
        (RfLabPanelViewModel panel, _, _) = CreatePanel(("present", ["Mode,0"]));

        // Listed by the stub but absent from it when read.
        panel.SelectedSettingsFile = "vanished";
        await Task.Delay(50);

        Assert.True(panel.ErrorStatus);
        Assert.Contains("could not be read", panel.StatusInfo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APresetCarryingAMode_ShouldSelectIt()
    {
        (RfLabPanelViewModel panel, _, _) = CreatePanel(
            ("measure", ["Mode,5"]));

        panel.SelectedSettingsFile = "measure";
        await Task.Delay(50);

        Assert.True(panel.IsModeMEASURE);

        panel.DDS_ModMode = (int)RfLabSignalMode.Off;
    }

    [Fact]
    public async Task AnOutOfRangeMode_ShouldBeIgnored()
    {
        (RfLabPanelViewModel panel, _, _) = CreatePanel(("odd", ["Mode,42"]));

        panel.SelectedSettingsFile = "odd";
        await Task.Delay(50);

        Assert.Equal((int)RfLabSignalMode.Off, panel.DDS_ModMode);
        Assert.False(panel.ErrorStatus);
    }

    private static ClientInstrumentPanelContext Context(
        RecordingInstrumentOperations operations) =>
        new(
            "rf-lab-signal-lab",
            "rf-minilab-01",
            "rf-minilab-01",
            "RF Signal Lab",
            operations);

    private static (
        RfLabPanelViewModel Panel,
        RecordingInstrumentOperations Operations,
        StubPresetStore Store) CreatePanel(
        params (string Name, string[] Lines)[] presets)
    {
        var operations = new RecordingInstrumentOperations();
        var store = new StubPresetStore(presets);
        var panel = new RfLabPanelViewModel(
            Context(operations),
            new RfLabPanelViewModelTests.RecordingScheduler(),
            store);

        return (panel, operations, store);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 200 && !condition(); attempt++)
        {
            await Task.Delay(10);
        }
    }

    internal sealed class StubPresetStore : IRfLabPresetStore
    {
        private readonly Dictionary<string, string[]> presets;

        public StubPresetStore((string Name, string[] Lines)[] presets) =>
            this.presets = presets.ToDictionary(
                entry => entry.Name,
                entry => entry.Lines,
                StringComparer.Ordinal);

        public IReadOnlyList<string> ListNames() =>
            [.. presets.Keys.OrderBy(name => name, StringComparer.Ordinal)];

        public RfLabPreset? Read(string name) =>
            presets.TryGetValue(name, out string[]? lines)
                ? RfLabPreset.FromLines(name, lines)
                : null;
    }
}
