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
/// application. They cannot be bound, so the window carries the values in both
/// directions itself: each control's change reaches the view model, and each
/// value the view model changes is pushed back into its control.
///
/// The second direction matters whenever something other than the operator
/// sets a value — loading a stored setting sets a dozen at once. Without it
/// the instrument receives the new values while the panel goes on showing the
/// old ones.
///
/// Converting these controls to dependency properties would replace all of
/// this with bindings, and remains deferred.
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
        PushAllValues();

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

        viewModel.PropertyChanged += OnViewModelPropertyChanged;

        _ = viewModel.InitializeAsync();
    }

    private void Window_Closing(object sender, CancelEventArgs eventArgs)
    {
        viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        viewModel.Dispose();
    }

    /// <summary>
    /// Carries a value the view model changed into its control.
    /// </summary>
    /// <remarks>
    /// The digit controls are told not to raise their change event, so a value
    /// pushed here does not travel back. The two dials have no such option,
    /// but their echo is harmless: the view model already holds the value and
    /// stops on it rather than applying again.
    /// </remarks>
    private void OnViewModelPropertyChanged(
        object? sender,
        PropertyChangedEventArgs eventArgs)
    {
        switch (eventArgs.PropertyName)
        {
            case nameof(RfLabPanelViewModel.Frequency):
                FrequencyDial.Frequency = viewModel.Frequency;
                break;
            case nameof(RfLabPanelViewModel.Amplitude):
                AmplitudeDial.Amplitude = viewModel.Amplitude;
                break;
            case nameof(RfLabPanelViewModel.ModulationFrequency):
                NCMD_Fmod.SetValue(viewModel.ModulationFrequency, false);
                break;
            case nameof(RfLabPanelViewModel.AmplitudeModulationDepth):
                NCMD_AMdepth.SetValue(viewModel.AmplitudeModulationDepth, false);
                break;
            case nameof(RfLabPanelViewModel.FrequencyDeviation):
                NCMD_FMdev.SetValue(viewModel.FrequencyDeviation, false);
                break;
            case nameof(RfLabPanelViewModel.SweepStartFrequency):
                NCMD_Fstart.SetValue(viewModel.SweepStartFrequency, false);
                break;
            case nameof(RfLabPanelViewModel.SweepStopFrequency):
                NCMD_Fstop.SetValue(viewModel.SweepStopFrequency, false);
                break;
            case nameof(RfLabPanelViewModel.SweepTime):
                NCMD_Tsweep.SetValue(viewModel.SweepTime, false);
                break;
            case nameof(RfLabPanelViewModel.MeasurementInterval):
                NCMD_Tmeasure.SetValue(viewModel.MeasurementInterval, false);
                break;
            case nameof(RfLabPanelViewModel.MeasurementCount):
                NCMD_Nmeasure.SetValue(viewModel.MeasurementCount, false);
                break;
            case nameof(RfLabPanelViewModel.ClockFrequency0):
                NCMD_SI5351_Fclk0.SetValue(viewModel.ClockFrequency0, false);
                break;
            case nameof(RfLabPanelViewModel.ClockFrequency1):
                NCMD_SI5351_Fclk1.SetValue(viewModel.ClockFrequency1, false);
                break;
            case nameof(RfLabPanelViewModel.ClockFrequency2):
                NCMD_SI5351_Fclk2.SetValue(viewModel.ClockFrequency2, false);
                break;
        }
    }

    private void PushAllValues()
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
    }
}
