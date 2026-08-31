using Hase.Client;
using Hase.Client.Wpf.RfLab.ViewModels;
using Hase.Client.Wpf.Services;

namespace Hase.Client.Wpf.RfLab.Tests;

/// <summary>
/// The Si5351 clock outputs of the original Special Signals tab. Each channel
/// carries its own target and its own apply command, so changing one touches
/// neither the others nor the signal path.
/// </summary>
public sealed class RfLabPanelClockTests
{
    [Theory]
    [InlineData(0, 12_000_000)]
    [InlineData(1, 24_000_000)]
    [InlineData(2, 48_000_000)]
    public async Task ChangingAClockOutput_ShouldStageAndApplyThatChannel(
        int channel,
        int frequency)
    {
        (RfLabPanelViewModel panel, RecordingInstrumentOperations operations) =
            CreatePanel();

        SetClock(panel, channel, frequency);
        await WaitForAsync(() => operations.Executions.Count > 0);

        Assert.Equal(
            [($"clock{channel}-frequency", (double)frequency)],
            operations.Writes);
        Assert.Equal([$"Clock.ApplyOutput{channel}"], operations.Executions);
    }

    [Fact]
    public async Task ChangingOneClockOutput_ShouldNotTouchTheSignalPath()
    {
        (RfLabPanelViewModel panel, RecordingInstrumentOperations operations) =
            CreatePanel();

        panel.ClockFrequency1 = 24_000_000;
        await WaitForAsync(() => operations.Executions.Count > 0);
        await Task.Delay(50);

        Assert.DoesNotContain(
            operations.Writes,
            write => write.PropertyId.StartsWith("target-", StringComparison.Ordinal));
        Assert.DoesNotContain(
            operations.Executions,
            execution => execution.StartsWith("Signal.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RepeatedClockChanges_ShouldCollapseOverlappingApplies()
    {
        (RfLabPanelViewModel panel, RecordingInstrumentOperations operations) =
            CreatePanel();
        operations.CloseGate();

        panel.ClockFrequency0 = 11_000_000;
        await WaitForAsync(() => operations.Executions.Count == 1);
        panel.ClockFrequency0 = 12_000_000;
        panel.ClockFrequency0 = 13_000_000;

        Assert.Single(operations.Executions);

        operations.OpenGate();
        await WaitForAsync(() => operations.Executions.Count == 2);
        await Task.Delay(50);

        // The channel ends at its newest value, not an intermediate one.
        Assert.Equal(2, operations.Executions.Count);
        Assert.Equal(("clock0-frequency", 13_000_000d), operations.Writes[^1]);
    }

    [Fact]
    public async Task ChangingTwoClockChannels_ShouldApplyBothIndependently()
    {
        (RfLabPanelViewModel panel, RecordingInstrumentOperations operations) =
            CreatePanel();
        operations.CloseGate();

        panel.ClockFrequency0 = 11_000_000;
        panel.ClockFrequency2 = 33_000_000;
        await WaitForAsync(() => operations.Executions.Count == 2);

        // One channel in flight must not hold another back.
        Assert.Equal(
            ["Clock.ApplyOutput0", "Clock.ApplyOutput2"],
            operations.Executions);

        operations.OpenGate();
        await Task.Delay(50);
    }

    [Fact]
    public void SettingTheSameClockFrequency_ShouldNotReapply()
    {
        (RfLabPanelViewModel panel, RecordingInstrumentOperations operations) =
            CreatePanel();

        panel.ClockFrequency0 = panel.ClockFrequency0;

        Assert.Empty(operations.Executions);
    }

    [Fact]
    public async Task APresentClockGenerator_ShouldEnableTheClockControls()
    {
        (RfLabPanelViewModel panel, RecordingInstrumentOperations operations) =
            CreatePanel();
        operations.SetRead(
            "clock-generator-present",
            RemoteValue.FromBoolean(true));

        await panel.InitializeAsync();

        Assert.True(panel.IsClockGeneratorPresent);
        Assert.Equal("Clock generator: present", panel.ClockGeneratorState);
    }

    [Fact]
    public async Task AnAbsentClockGenerator_ShouldDisableTheClockControls()
    {
        (RfLabPanelViewModel panel, RecordingInstrumentOperations operations) =
            CreatePanel();
        operations.SetRead(
            "clock-generator-present",
            RemoteValue.FromBoolean(false));

        await panel.InitializeAsync();

        Assert.False(panel.IsClockGeneratorPresent);
        Assert.Equal("Clock generator: absent", panel.ClockGeneratorState);
    }

    [Fact]
    public async Task AnUnreadableClockGenerator_ShouldDisableTheClockControls()
    {
        (RfLabPanelViewModel panel, _) = CreatePanel();

        // No scripted value, so the read fails.
        await panel.InitializeAsync();

        Assert.False(panel.IsClockGeneratorPresent);
        Assert.Equal("Clock generator: unknown", panel.ClockGeneratorState);
    }

    [Fact]
    public void TheClockOutputs_ShouldStartAtTheDefinitionDefaults()
    {
        (RfLabPanelViewModel panel, _) = CreatePanel();

        Assert.Equal(1_000_000, panel.ClockFrequency0);
        Assert.Equal(2_000_000, panel.ClockFrequency1);
        Assert.Equal(3_000_000, panel.ClockFrequency2);
    }

    private static void SetClock(
        RfLabPanelViewModel panel,
        int channel,
        int frequency)
    {
        switch (channel)
        {
            case 0:
                panel.ClockFrequency0 = frequency;
                break;
            case 1:
                panel.ClockFrequency1 = frequency;
                break;
            default:
                panel.ClockFrequency2 = frequency;
                break;
        }
    }

    private static (RfLabPanelViewModel Panel, RecordingInstrumentOperations Operations)
        CreatePanel()
    {
        var operations = new RecordingInstrumentOperations();
        var panel = new RfLabPanelViewModel(
            new ClientInstrumentPanelContext(
                "rf-lab-signal-lab",
                "rf-minilab-01",
                "rf-minilab-01",
                "RF Signal Lab",
                operations),
            new RfLabPanelViewModelTests.RecordingScheduler());

        return (panel, operations);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 200 && !condition(); attempt++)
        {
            await Task.Delay(10);
        }
    }
}
