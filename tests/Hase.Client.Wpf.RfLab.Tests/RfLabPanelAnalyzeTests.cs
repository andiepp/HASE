using Hase.Client;
using Hase.Client.Wpf.RfLab.ViewModels;
using Hase.Client.Wpf.Services;

namespace Hase.Client.Wpf.RfLab.Tests;

public sealed class RfLabPanelAnalyzeTests
{
    [Fact]
    public void AnalyzeMode_ShouldGateTheSweepFieldsLikeTheOriginalPanel()
    {
        (RfLabPanelViewModel panel, _, _) = CreatePanel();

        panel.DDS_ModMode = (int)RfLabSignalMode.Analyze;

        Assert.True(panel.IsModeANALYZE);
        Assert.True(panel.IsModeSWEEP);
        Assert.False(panel.IsModeMEASURE);
        Assert.Equal("f", panel.Xlabel);
    }

    [Fact]
    public async Task Analyze_ShouldStepTheCarrierAcrossTheSpanAndPlotEachReading()
    {
        (RfLabPanelViewModel panel, RecordingInstrumentOperations operations, _) =
            CreatePanel();
        operations.SetRead("sensor-level", RemoteValue.FromNumeric(-40.0));
        panel.SweepStartFrequency = 10_000_000;
        panel.SweepStopFrequency = 20_000_000;
        panel.MeasurementCount = 10;
        panel.DDS_ModMode = (int)RfLabSignalMode.Analyze;

        await RunAnalyzeAsync(panel);

        // Ten analysed points plus the carrier restored at the end.
        Assert.Equal(11, operations.Executions.Count);
        Assert.All(
            operations.Executions,
            execution => Assert.Equal("Signal.ApplyCarrier", execution));
        Assert.Equal(10, operations.Reads.Count(read => read == "sensor-level"));

        Assert.Equal(10, panel.MeasurementData.Count);
        Assert.Equal(10_000_000, panel.MeasurementData[0].X);
        Assert.Equal(19_000_000, panel.MeasurementData[^1].X);
        Assert.All(panel.MeasurementData, point => Assert.Equal(-40.0, point.Y));
        Assert.False(panel.ErrorStatus);
        Assert.Contains("Analyze complete", panel.StatusInfo, StringComparison.Ordinal);
        Assert.False(panel.IsSweepActive);
    }

    [Fact]
    public async Task Analyze_ShouldSettleBetweenStepsUsingTheSweepDuration()
    {
        (RfLabPanelViewModel panel, RecordingInstrumentOperations operations,
            RfLabPanelViewModelTests.RecordingScheduler scheduler) = CreatePanel();
        operations.SetRead("sensor-level", RemoteValue.FromNumeric(-50.0));
        panel.SweepStartFrequency = 10_000_000;
        panel.SweepStopFrequency = 11_000_000;
        panel.MeasurementCount = 10;
        panel.SweepTime = 10_000;
        panel.DDS_ModMode = (int)RfLabSignalMode.Analyze;

        await RunAnalyzeAsync(panel);

        // Ten steps across ten seconds settle for a second each.
        Assert.Equal(10, scheduler.Delays.Count);
        Assert.All(
            scheduler.Delays,
            delay => Assert.InRange(delay.TotalMilliseconds, 900, 1000));
    }

    [Fact]
    public async Task Analyze_ShouldRaiseAShortDurationToTheOriginalFloor()
    {
        (RfLabPanelViewModel panel, RecordingInstrumentOperations operations,
            RfLabPanelViewModelTests.RecordingScheduler scheduler) = CreatePanel();
        operations.SetRead("sensor-level", RemoteValue.FromNumeric(-50.0));
        panel.MeasurementCount = 10;
        panel.SweepTime = 100;
        panel.DDS_ModMode = (int)RfLabSignalMode.Analyze;

        await RunAnalyzeAsync(panel);

        // The floor is 3200 ms over ten points.
        Assert.All(
            scheduler.Delays,
            delay => Assert.InRange(delay.TotalMilliseconds, 250, 320));
    }

    [Fact]
    public async Task Analyze_ShouldReturnTheGeneratorToThePanelCarrier()
    {
        (RfLabPanelViewModel panel, RecordingInstrumentOperations operations, _) =
            CreatePanel();
        operations.SetRead("sensor-level", RemoteValue.FromNumeric(-40.0));
        panel.Frequency = 145_000_000;
        panel.SweepStartFrequency = 10_000_000;
        panel.SweepStopFrequency = 20_000_000;
        panel.MeasurementCount = 10;
        panel.DDS_ModMode = (int)RfLabSignalMode.Analyze;
        operations.Writes.Clear();

        await RunAnalyzeAsync(panel);

        Assert.Equal(
            ("target-frequency", 145_000_000d),
            operations.Writes[^1]);
    }

    [Fact]
    public async Task Analyze_ShouldStopWhenTheDetectorReadFails()
    {
        (RfLabPanelViewModel panel, RecordingInstrumentOperations operations, _) =
            CreatePanel();
        panel.MeasurementCount = 10;
        panel.DDS_ModMode = (int)RfLabSignalMode.Analyze;

        // No scripted detector reading, so the first read fails.
        await RunAnalyzeAsync(panel);

        Assert.True(panel.ErrorStatus);
        Assert.Contains("Analyze stopped", panel.StatusInfo, StringComparison.Ordinal);
        Assert.Empty(panel.MeasurementData);
        Assert.False(panel.IsSweepActive);
    }

    [Fact]
    public async Task Analyze_ShouldRejectASpanThatDoesNotAscend()
    {
        (RfLabPanelViewModel panel, RecordingInstrumentOperations operations, _) =
            CreatePanel();
        panel.SweepStartFrequency = 20_000_000;
        panel.SweepStopFrequency = 20_000_000;
        panel.DDS_ModMode = (int)RfLabSignalMode.Analyze;
        operations.Executions.Clear();

        await RunAnalyzeAsync(panel);

        Assert.True(panel.ErrorStatus);
        Assert.Contains(
            "stop frequency above the start frequency",
            panel.StatusInfo,
            StringComparison.Ordinal);
        Assert.Empty(operations.Executions);
        Assert.False(panel.IsSweepActive);
    }

    [Fact]
    public async Task LeavingAnalyzeMode_ShouldStopARunningAnalysis()
    {
        (RfLabPanelViewModel panel, RecordingInstrumentOperations operations, _) =
            CreatePanel();
        operations.SetRead("sensor-level", RemoteValue.FromNumeric(-40.0));
        panel.MeasurementCount = 500;
        panel.DDS_ModMode = (int)RfLabSignalMode.Analyze;
        panel.IsSweepActive = true;

        panel.DDS_ModMode = (int)RfLabSignalMode.Off;
        await WaitForAsync(() => !panel.IsSweepActive);

        int executionsAfterStop = operations.Executions.Count;
        await Task.Delay(50);

        Assert.False(panel.IsSweepActive);
        Assert.True(
            operations.Executions.Count <= executionsAfterStop + 1,
            "the analysis kept commanding the instrument after the mode changed");
    }

    [Fact]
    public async Task Dispose_ShouldStopARunningAnalysis()
    {
        (RfLabPanelViewModel panel, RecordingInstrumentOperations operations, _) =
            CreatePanel();
        operations.SetRead("sensor-level", RemoteValue.FromNumeric(-40.0));
        panel.MeasurementCount = 500;
        panel.DDS_ModMode = (int)RfLabSignalMode.Analyze;
        panel.IsSweepActive = true;

        panel.Dispose();
        await Task.Delay(50);
        int executions = operations.Executions.Count;
        await Task.Delay(50);

        Assert.True(
            operations.Executions.Count <= executions + 1,
            "the analysis kept commanding the instrument after disposal");
    }

    private static (
        RfLabPanelViewModel Panel,
        RecordingInstrumentOperations Operations,
        RfLabPanelViewModelTests.RecordingScheduler Scheduler) CreatePanel()
    {
        var operations = new RecordingInstrumentOperations();
        var scheduler = new RfLabPanelViewModelTests.RecordingScheduler();
        var panel = new RfLabPanelViewModel(
            new ClientInstrumentPanelContext(
                "rf-lab-signal-lab",
                "rf-minilab-01",
                "rf-minilab-01",
                "RF Signal Lab",
                operations),
            scheduler);

        return (panel, operations, scheduler);
    }

    private static async Task RunAnalyzeAsync(RfLabPanelViewModel panel)
    {
        panel.IsSweepActive = true;
        await WaitForAsync(() => !panel.IsSweepActive);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 200 && !condition(); attempt++)
        {
            await Task.Delay(10);
        }
    }
}
