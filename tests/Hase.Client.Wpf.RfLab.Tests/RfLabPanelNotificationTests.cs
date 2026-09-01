using Hase.Client.Wpf.RfLab.ViewModels;
using Hase.Client.Wpf.Services;

namespace Hase.Client.Wpf.RfLab.Tests;

/// <summary>
/// The ported controls cannot be bound, so the window pushes each changed
/// value into its control by hand. It can only push what the view model
/// announces, which makes these notifications load-bearing rather than
/// incidental.
/// </summary>
public sealed class RfLabPanelNotificationTests
{
    private static readonly string[] PushedValueNames =
    [
        nameof(RfLabPanelViewModel.Frequency),
        nameof(RfLabPanelViewModel.Amplitude),
        nameof(RfLabPanelViewModel.ModulationFrequency),
        nameof(RfLabPanelViewModel.AmplitudeModulationDepth),
        nameof(RfLabPanelViewModel.FrequencyDeviation),
        nameof(RfLabPanelViewModel.SweepStartFrequency),
        nameof(RfLabPanelViewModel.SweepStopFrequency),
        nameof(RfLabPanelViewModel.SweepTime),
        nameof(RfLabPanelViewModel.MeasurementInterval),
        nameof(RfLabPanelViewModel.MeasurementCount),
        nameof(RfLabPanelViewModel.ClockFrequency0),
        nameof(RfLabPanelViewModel.ClockFrequency1),
        nameof(RfLabPanelViewModel.ClockFrequency2)
    ];

    [Theory]
    [InlineData(nameof(RfLabPanelViewModel.Frequency))]
    [InlineData(nameof(RfLabPanelViewModel.Amplitude))]
    [InlineData(nameof(RfLabPanelViewModel.ModulationFrequency))]
    [InlineData(nameof(RfLabPanelViewModel.AmplitudeModulationDepth))]
    [InlineData(nameof(RfLabPanelViewModel.FrequencyDeviation))]
    [InlineData(nameof(RfLabPanelViewModel.SweepStartFrequency))]
    [InlineData(nameof(RfLabPanelViewModel.SweepStopFrequency))]
    [InlineData(nameof(RfLabPanelViewModel.SweepTime))]
    [InlineData(nameof(RfLabPanelViewModel.MeasurementInterval))]
    [InlineData(nameof(RfLabPanelViewModel.MeasurementCount))]
    [InlineData(nameof(RfLabPanelViewModel.ClockFrequency0))]
    [InlineData(nameof(RfLabPanelViewModel.ClockFrequency1))]
    [InlineData(nameof(RfLabPanelViewModel.ClockFrequency2))]
    public void EveryValueTheWindowPushes_ShouldAnnounceItsChange(string propertyName)
    {
        RfLabPanelViewModel panel = CreatePanel();
        List<string?> announced = Observe(panel);

        SetByName(panel, propertyName, CurrentByName(panel, propertyName) + 1);

        Assert.Contains(propertyName, announced);
    }

    [Fact]
    public async Task LoadingAPreset_ShouldAnnounceEveryValueItChanges()
    {
        // This is the case the push exists for: a dozen values change at once
        // and not one of them came from a control.
        RfLabPanelViewModel panel = CreatePanel(
            ("bench",
            [
                "Frequency,21400000",
                "Amplitude,40",
                "FMod,2000",
                "AMDepth,55",
                "Fdev,25000",
                "Fstart,1000000",
                "Fstop,5000000",
                "Tsweep,4000",
                "Tmeasure,250",
                "Nmeasure,300",
                "SI5351Fclk0,4000000",
                "SI5351Fclk1,5000000",
                "SI5351Fclk2,6000000"
            ]));
        List<string?> announced = Observe(panel);

        panel.SelectedSettingsFile = "bench";
        await Task.Delay(50);

        foreach (string name in PushedValueNames)
        {
            Assert.Contains(name, announced);
        }
    }

    [Fact]
    public void SettingTheSameValue_ShouldAnnounceNothing()
    {
        // A push that changes nothing would still repaint a control.
        RfLabPanelViewModel panel = CreatePanel();
        List<string?> announced = Observe(panel);

        panel.SweepTime = panel.SweepTime;
        panel.MeasurementCount = panel.MeasurementCount;
        panel.SweepStartFrequency = panel.SweepStartFrequency;

        Assert.Empty(announced);
    }

    private static List<string?> Observe(RfLabPanelViewModel panel)
    {
        var announced = new List<string?>();
        panel.PropertyChanged += (_, args) => announced.Add(args.PropertyName);
        return announced;
    }

    private static int CurrentByName(RfLabPanelViewModel panel, string name) =>
        name switch
        {
            nameof(RfLabPanelViewModel.Frequency) => panel.Frequency,
            nameof(RfLabPanelViewModel.Amplitude) => panel.Amplitude,
            nameof(RfLabPanelViewModel.ModulationFrequency) =>
                panel.ModulationFrequency,
            nameof(RfLabPanelViewModel.AmplitudeModulationDepth) =>
                panel.AmplitudeModulationDepth,
            nameof(RfLabPanelViewModel.FrequencyDeviation) =>
                panel.FrequencyDeviation,
            nameof(RfLabPanelViewModel.SweepStartFrequency) =>
                panel.SweepStartFrequency,
            nameof(RfLabPanelViewModel.SweepStopFrequency) =>
                panel.SweepStopFrequency,
            nameof(RfLabPanelViewModel.SweepTime) => panel.SweepTime,
            nameof(RfLabPanelViewModel.MeasurementInterval) =>
                panel.MeasurementInterval,
            nameof(RfLabPanelViewModel.MeasurementCount) => panel.MeasurementCount,
            nameof(RfLabPanelViewModel.ClockFrequency0) => panel.ClockFrequency0,
            nameof(RfLabPanelViewModel.ClockFrequency1) => panel.ClockFrequency1,
            _ => panel.ClockFrequency2
        };

    private static void SetByName(
        RfLabPanelViewModel panel,
        string name,
        int value)
    {
        switch (name)
        {
            case nameof(RfLabPanelViewModel.Frequency):
                panel.Frequency = value;
                break;
            case nameof(RfLabPanelViewModel.Amplitude):
                panel.Amplitude = value;
                break;
            case nameof(RfLabPanelViewModel.ModulationFrequency):
                panel.ModulationFrequency = value;
                break;
            case nameof(RfLabPanelViewModel.AmplitudeModulationDepth):
                panel.AmplitudeModulationDepth = value;
                break;
            case nameof(RfLabPanelViewModel.FrequencyDeviation):
                panel.FrequencyDeviation = value;
                break;
            case nameof(RfLabPanelViewModel.SweepStartFrequency):
                panel.SweepStartFrequency = value;
                break;
            case nameof(RfLabPanelViewModel.SweepStopFrequency):
                panel.SweepStopFrequency = value;
                break;
            case nameof(RfLabPanelViewModel.SweepTime):
                panel.SweepTime = value;
                break;
            case nameof(RfLabPanelViewModel.MeasurementInterval):
                panel.MeasurementInterval = value;
                break;
            case nameof(RfLabPanelViewModel.MeasurementCount):
                panel.MeasurementCount = value;
                break;
            case nameof(RfLabPanelViewModel.ClockFrequency0):
                panel.ClockFrequency0 = value;
                break;
            case nameof(RfLabPanelViewModel.ClockFrequency1):
                panel.ClockFrequency1 = value;
                break;
            default:
                panel.ClockFrequency2 = value;
                break;
        }
    }

    private static RfLabPanelViewModel CreatePanel(
        params (string Name, string[] Lines)[] presets) =>
        new(
            new ClientInstrumentPanelContext(
                "rf-lab-signal-lab",
                "rf-minilab-01",
                "rf-minilab-01",
                "RF Signal Lab",
                new RecordingInstrumentOperations()),
            new RfLabPanelViewModelTests.RecordingScheduler(),
            new RfLabPanelPresetTests.StubPresetStore(presets));
}
