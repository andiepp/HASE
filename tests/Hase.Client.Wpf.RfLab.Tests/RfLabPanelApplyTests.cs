using System.ComponentModel;
using Hase.Client;
using Hase.Client.Wpf.RfLab.ViewModels;
using Hase.Client.Wpf.Services;

namespace Hase.Client.Wpf.RfLab.Tests;

/// <summary>
/// The panel must not disable itself to perform an apply, and it must not let
/// the applies a moving dial produces overlap.
/// </summary>
public sealed class RfLabPanelApplyTests
{
    [Fact]
    public async Task ApplyingACarrier_ShouldNeverDisableThePanel()
    {
        (RfLabPanelViewModel panel, RecordingInstrumentOperations operations) =
            CreatePanel();
        var enabledStates = new List<bool>();
        panel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(panel.IsUIEnabled))
            {
                enabledStates.Add(panel.IsUIEnabled);
            }
        };

        panel.Frequency = 21_400_000;
        await WaitForAsync(() => operations.Executions.Count > 0);

        // A disable and re-enable around the round trip is what made the
        // whole surface flicker on every dial movement.
        Assert.Empty(enabledStates);
        Assert.True(panel.IsUIEnabled);
    }

    [Fact]
    public async Task ApplyFailure_ShouldLeaveThePanelEnabled()
    {
        (RfLabPanelViewModel panel, RecordingInstrumentOperations operations) =
            CreatePanel();
        operations.FailNextCommand = true;

        panel.Frequency = 21_400_000;
        await WaitForAsync(() => panel.ErrorStatus);

        Assert.True(panel.ErrorStatus);
        Assert.True(panel.IsUIEnabled);
    }

    [Fact]
    public async Task DialMovement_ShouldCollapseOverlappingApplies()
    {
        (RfLabPanelViewModel panel, RecordingInstrumentOperations operations) =
            CreatePanel();
        operations.CloseGate();

        // The dial reports every intermediate value it passes through.
        panel.Frequency = 11_000_000;
        await WaitForAsync(() => operations.Executions.Count == 1);
        panel.Frequency = 12_000_000;
        panel.Frequency = 13_000_000;
        panel.Frequency = 14_000_000;

        // Only the first apply is in flight; the rest collapse into one.
        Assert.Single(operations.Executions);

        operations.OpenGate();
        await WaitForAsync(() => operations.Executions.Count == 2);
        await Task.Delay(50);

        Assert.Equal(2, operations.Executions.Count);

        // The apply that follows carries the newest value, not an
        // intermediate one.
        Assert.Equal(("target-frequency", 14_000_000d), operations.Writes[^2]);
        Assert.Equal(14_000_000, panel.Frequency);
    }

    [Fact]
    public async Task SequentialChanges_ShouldEachApply()
    {
        (RfLabPanelViewModel panel, RecordingInstrumentOperations operations) =
            CreatePanel();

        panel.Frequency = 11_000_000;
        await WaitForAsync(() => operations.Executions.Count == 1);
        panel.Amplitude = 30;
        await WaitForAsync(() => operations.Executions.Count == 2);

        // Nothing is coalesced when the applies do not overlap.
        Assert.Equal(2, operations.Executions.Count);
        Assert.All(
            operations.Executions,
            execution => Assert.Equal("Signal.ApplyCarrier", execution));
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
