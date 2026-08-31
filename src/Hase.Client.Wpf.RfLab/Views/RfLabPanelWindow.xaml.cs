#nullable enable

using System.ComponentModel;
using System.Windows;
using Hase.Client.Wpf.RfLab.ViewModels;

namespace Hase.Client.Wpf.RfLab.Views;

/// <summary>
/// Hosts the ported RF-Lab operating surface.
/// </summary>
/// <remarks>
/// The numeric dial controls carry their value as a plain property with a
/// change event rather than a dependency property, exactly as in the original
/// application. The window therefore pushes the staged values down once and
/// forwards each change to the view model, which is the same wiring the
/// original used and keeps the controls unmodified.
/// </remarks>
public partial class RfLabPanelWindow : Window
{
    private readonly RfLabPanelViewModel viewModel;

    public RfLabPanelWindow(RfLabPanelViewModel viewModel)
    {
        this.viewModel = viewModel
            ?? throw new ArgumentNullException(nameof(viewModel));

        InitializeComponent();
        DataContext = viewModel;
    }

    private void Window_Loaded(object sender, RoutedEventArgs eventArgs)
    {
        FrequencyDial.Frequency = viewModel.Frequency;
        AmplitudeDial.Amplitude = viewModel.Amplitude;
        NCMD_Fmod.SetValue(viewModel.ModulationFrequency, false);
        NCMD_AMdepth.SetValue(viewModel.AmplitudeModulationDepth, false);
        NCMD_FMdev.SetValue(viewModel.FrequencyDeviation, false);
        NCMD_Fstart.SetValue(viewModel.SweepStartFrequency, false);
        NCMD_Fstop.SetValue(viewModel.SweepStopFrequency, false);
        NCMD_Tsweep.SetValue(viewModel.SweepTime, false);
        NCMD_Tmeasure.SetValue(viewModel.MeasurementInterval, false);
        NCMD_Nmeasure.SetValue(viewModel.MeasurementCount, false);
        NCMD_SI5351_Fclk0.SetValue(viewModel.ClockFrequency0, false);
        NCMD_SI5351_Fclk1.SetValue(viewModel.ClockFrequency1, false);
        NCMD_SI5351_Fclk2.SetValue(viewModel.ClockFrequency2, false);

        FrequencyDial.ValueChanged += (_, args) => viewModel.Frequency = args.Value;
        AmplitudeDial.ValueChanged += (_, args) => viewModel.Amplitude = args.Value;
        NCMD_Fmod.ValueChanged += (_, args) => viewModel.ModulationFrequency = args.Value;
        NCMD_AMdepth.ValueChanged += (_, args) =>
            viewModel.AmplitudeModulationDepth = args.Value;
        NCMD_FMdev.ValueChanged += (_, args) => viewModel.FrequencyDeviation = args.Value;
        NCMD_Fstart.ValueChanged += (_, args) =>
            viewModel.SweepStartFrequency = args.Value;
        NCMD_Fstop.ValueChanged += (_, args) => viewModel.SweepStopFrequency = args.Value;
        NCMD_Tsweep.ValueChanged += (_, args) => viewModel.SweepTime = args.Value;
        NCMD_Tmeasure.ValueChanged += (_, args) =>
            viewModel.MeasurementInterval = args.Value;
        NCMD_Nmeasure.ValueChanged += (_, args) => viewModel.MeasurementCount = args.Value;
        NCMD_SI5351_Fclk0.ValueChanged += (_, args) =>
            viewModel.ClockFrequency0 = args.Value;
        NCMD_SI5351_Fclk1.ValueChanged += (_, args) =>
            viewModel.ClockFrequency1 = args.Value;
        NCMD_SI5351_Fclk2.ValueChanged += (_, args) =>
            viewModel.ClockFrequency2 = args.Value;

        _ = viewModel.InitializeAsync();
    }

    private void Window_Closing(object sender, CancelEventArgs eventArgs)
    {
        viewModel.Dispose();
    }
}
