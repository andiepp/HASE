#nullable enable

using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Hase.Client.Wpf.Services;
using Prism.Commands;
using Prism.Mvvm;

namespace Hase.Client.Wpf.RfLab.ViewModels;

/// <summary>
/// Drives the RF-Lab panel against one published instrument.
/// </summary>
/// <remarks>
/// This replaces the original application's view model, which was itself the
/// MCNF device controller and drove the serial port directly. Here every
/// operation is a normalized Property or Command on the Runtime Host: the
/// panel stages target values, applies them with one Command, and reads the
/// detector back. The bound member names are preserved so the original view
/// binds unchanged.
/// </remarks>
public sealed class RfLabPanelViewModel : BindableBase, IDisposable
{
    /// <summary>The rendered measurement window, as in the original panel.</summary>
    private const int MaximumMeasurementPoints = 500;

    /// <summary>
    /// The shortest analysis duration, carried over from the original panel,
    /// which raised a shorter setting to this floor.
    /// </summary>
    private const int MinimumAnalyzeSweepTimeMs = 3_200;

    private const int MinimumAnalyzePoints = 10;

    private static readonly IReadOnlyDictionary<string, string> SweepCommandPaths =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Bidirectional"] = "Signal.StartSweepBidirectional",
            ["Ramp"] = "Signal.StartSweepRamp",
            ["SingleRamp"] = "Signal.StartSweepSingleRamp"
        };

    private readonly IRuntimeHostInstrumentOperations operations;
    private readonly IRfLabPanelScheduler scheduler;
    private readonly ObservableCollection<RfLabMeasurementPoint> measurementData = [];

    private RfLabSignalMode mode = RfLabSignalMode.Off;
    private RfLabSensorOption selectedSensor;
    private string selectedSweepMode = "Bidirectional";
    private bool isSweepActive;
    private bool isMeasurementActive;
    private bool isUiEnabled = true;
    private bool errorStatus;
    private bool led;
    private string statusInfo = "Ready.";
    private string sensorValueString = "--";
    private double sensorValueNormalized;
    private string productIdentity = string.Empty;
    private string nodeType = string.Empty;
    private string clockGeneratorState = string.Empty;
    private int measurementIndex;
    private IDisposable? measurementLoop;
    private CancellationTokenSource? analyzeCancellation;
    private bool disposed;
    private int frequency = 10_000_000;
    private int amplitude = 20;
    private int modulationFrequency = 1_000;
    private int amplitudeModulationDepth = 80;
    private int frequencyDeviation = 10_000;
    private int sweepStartFrequency = 10_000_000;
    private int sweepStopFrequency = 30_000_000;

    public RfLabPanelViewModel(
        ClientInstrumentPanelContext context,
        IRfLabPanelScheduler? scheduler = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        operations = context.Operations;
        this.scheduler = scheduler ?? new RfLabPanelScheduler();
        EndpointId = $"Endpoint: {context.EndpointId}";
        InstrumentId = $"Instrument: {context.InstrumentId}";
        Title = $"RF-Lab — {context.DisplayName}";

        Sensors =
        [
            new RfLabSensorOption("AD8307_50", "sensor-level", "dB", -70.0, 10.0),
            new RfLabSensorOption("AD_8", "sensor-voltage", "mV", 0.0, 2560.0)
        ];
        selectedSensor = Sensors[0];

        SweepModes = [.. SweepCommandPaths.Keys];

        ClearMeasurementCommand = new DelegateCommand(ClearMeasurement);
        CalibrateCommand = new DelegateCommand(() => { }, () => false);
        ToggleIndicatorCommand = new DelegateCommand(
            async () => await ToggleIndicatorAsync().ConfigureAwait(true));
    }

    public string Title { get; }

    public string EndpointId { get; }

    public string InstrumentId { get; }

    public IReadOnlyList<RfLabSensorOption> Sensors { get; }

    public IReadOnlyList<string> SweepModes { get; }

    public ICommand ClearMeasurementCommand { get; }

    public ICommand CalibrateCommand { get; }

    public ICommand ToggleIndicatorCommand { get; }

    public ObservableCollection<RfLabMeasurementPoint> MeasurementData =>
        measurementData;

    // ---------------------------------------------------------------
    // Staged target values. The panel's digit controls push these in;
    // an apply Command sends them to the instrument.
    // ---------------------------------------------------------------

    /// <remarks>
    /// Changing a live target applies it immediately, the way the original
    /// panel's dials drove the generator. Sweep and measurement targets are
    /// staged only; they take effect when the sweep or the measurement starts.
    /// </remarks>
    public int Frequency
    {
        get => frequency;
        set => SetLiveTarget(ref frequency, value);
    }

    public int Amplitude
    {
        get => amplitude;
        set => SetLiveTarget(ref amplitude, value);
    }

    public int ModulationFrequency
    {
        get => modulationFrequency;
        set => SetLiveTarget(ref modulationFrequency, value);
    }

    public int AmplitudeModulationDepth
    {
        get => amplitudeModulationDepth;
        set => SetLiveTarget(ref amplitudeModulationDepth, value);
    }

    public int FrequencyDeviation
    {
        get => frequencyDeviation;
        set => SetLiveTarget(ref frequencyDeviation, value);
    }

    public int SweepStartFrequency
    {
        get => sweepStartFrequency;
        set
        {
            if (SetProperty(ref sweepStartFrequency, value))
            {
                RaisePropertyChanged(nameof(Xmin));
            }
        }
    }

    public int SweepStopFrequency
    {
        get => sweepStopFrequency;
        set
        {
            if (SetProperty(ref sweepStopFrequency, value))
            {
                RaisePropertyChanged(nameof(Xmax));
            }
        }
    }

    public int SweepTime { get; set; } = 2_000;

    public int MeasurementInterval { get; set; } = 1_000;

    public int MeasurementCount { get; set; } = 200;

    public int DDS_ModMode
    {
        get => (int)mode;
        set
        {
            var requested = (RfLabSignalMode)value;
            if (mode == requested)
            {
                return;
            }

            mode = requested;
            CancelAnalyze();
            RaisePropertyChanged(nameof(DDS_ModMode));
            RaisePropertyChanged(nameof(IsModeAM));
            RaisePropertyChanged(nameof(IsModeFM));
            RaisePropertyChanged(nameof(IsModeSWEEP));
            RaisePropertyChanged(nameof(IsModeANALYZE));
            RaisePropertyChanged(nameof(IsModeMEASURE));
            RaisePropertyChanged(nameof(IsFModEnabled));
            RaisePropertyChanged(nameof(Xlabel));
            RaisePropertyChanged(nameof(Xmin));
            RaisePropertyChanged(nameof(Xmax));
            _ = ApplyModeAsync();
        }
    }

    public bool IsModeAM => mode == RfLabSignalMode.AmplitudeModulation;

    public bool IsModeFM => mode == RfLabSignalMode.FrequencyModulation;

    /// <summary>
    /// Gets whether the sweep span and duration apply, which they do in both
    /// the swept and the analysing mode. The original panel gated its sweep
    /// fields on exactly this.
    /// </summary>
    public bool IsModeSWEEP =>
        mode is RfLabSignalMode.Sweep or RfLabSignalMode.Analyze;

    public bool IsModeANALYZE => mode == RfLabSignalMode.Analyze;

    public bool IsModeMEASURE => mode == RfLabSignalMode.Measure;

    public bool IsFModEnabled => IsModeAM || IsModeFM;

    public bool IsSweepActive
    {
        get => isSweepActive;
        set
        {
            if (!SetProperty(ref isSweepActive, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(IsSweepInactive));

            if (!value)
            {
                CancelAnalyze();
                return;
            }

            _ = IsModeANALYZE
                ? StartAnalyzeAsync()
                : StartSweepAsync();
        }
    }

    public bool IsSweepInactive => !isSweepActive;

    public bool IsMeasurementActive
    {
        get => isMeasurementActive;
        set
        {
            if (!SetProperty(ref isMeasurementActive, value))
            {
                return;
            }

            if (value)
            {
                StartMeasurementLoop();
            }
            else
            {
                StopMeasurementLoop();
            }
        }
    }

    public bool IsUIEnabled
    {
        get => isUiEnabled;
        private set => SetProperty(ref isUiEnabled, value);
    }

    public bool ErrorStatus
    {
        get => errorStatus;
        private set => SetProperty(ref errorStatus, value);
    }

    public bool LED
    {
        get => led;
        private set => SetProperty(ref led, value);
    }

    public string StatusInfo
    {
        get => statusInfo;
        private set => SetProperty(ref statusInfo, value);
    }

    public string SensorValueString
    {
        get => sensorValueString;
        private set => SetProperty(ref sensorValueString, value);
    }

    public double SensorValueNormalized
    {
        get => sensorValueNormalized;
        private set => SetProperty(ref sensorValueNormalized, value);
    }

    public string ProductIdentity
    {
        get => productIdentity;
        private set => SetProperty(ref productIdentity, value);
    }

    public string NodeType
    {
        get => nodeType;
        private set => SetProperty(ref nodeType, value);
    }

    public string ClockGeneratorState
    {
        get => clockGeneratorState;
        private set => SetProperty(ref clockGeneratorState, value);
    }

    public string CalText => "C";

    public string CalibrationInfo => string.Empty;

    public RfLabSensorOption SelectedSensor
    {
        get => selectedSensor;
        set
        {
            if (SetProperty(ref selectedSensor, value))
            {
                ClearMeasurement();
            }
        }
    }

    public string SelectedSweepMode
    {
        get => selectedSweepMode;
        set => SetProperty(ref selectedSweepMode, value);
    }

    public string Xlabel => IsModeMEASURE ? "n" : "f";

    public double Xmin => IsModeMEASURE ? 0 : SweepStartFrequency;

    public double Xmax =>
        IsModeMEASURE ? MaximumMeasurementPoints : SweepStopFrequency;

    // The original panel offered a stored-settings list. Presets are not part
    // of this panel yet; the members remain so the view binds unchanged.
    public IReadOnlyList<string> SettingsFiles { get; } = [];

    public string? SelectedSettingsFile { get; set; }

    /// <summary>
    /// Reads the instrument's identity and current state once, so the panel
    /// opens on authoritative values rather than assumptions.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ProductIdentity = await ReadTextAsync("product-identity", cancellationToken)
            .ConfigureAwait(true);
        NodeType = await ReadTextAsync("node-type", cancellationToken)
            .ConfigureAwait(true);

        RemotePropertyOperationResult clockGenerator = await operations
            .ReadAsync("clock-generator-present", cancellationToken)
            .ConfigureAwait(true);
        ClockGeneratorState = clockGenerator.IsSuccess
            ? clockGenerator.ConfirmedValue?.Value?.BooleanValue == true
                ? "Clock generator: present"
                : "Clock generator: absent"
            : "Clock generator: unknown";

        RemotePropertyOperationResult indicator = await operations
            .ReadAsync("indicator-enabled", cancellationToken)
            .ConfigureAwait(true);
        LED = indicator.IsSuccess
            && indicator.ConfirmedValue?.Value?.BooleanValue == true;

        await ReadSensorAsync(cancellationToken).ConfigureAwait(true);
        StatusInfo = "Connected.";
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        CancelAnalyze();
        StopMeasurementLoop();
    }

    /// <summary>
    /// Stages a live target and re-applies the current signal, so a dial
    /// change reaches the instrument at once.
    /// </summary>
    private void SetLiveTarget(
        ref int field,
        int value,
        [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        RaisePropertyChanged(propertyName);

        if (mode is RfLabSignalMode.Off
            or RfLabSignalMode.AmplitudeModulation
            or RfLabSignalMode.FrequencyModulation)
        {
            _ = ApplyModeAsync();
        }
    }

    private async Task ApplyModeAsync()
    {
        switch (mode)
        {
            case RfLabSignalMode.Off:
                await ApplyAsync(
                    "Signal.ApplyCarrier",
                    [
                        ("target-frequency", Frequency),
                        ("target-attenuation", Amplitude)
                    ],
                    "Carrier applied.").ConfigureAwait(true);
                break;

            case RfLabSignalMode.AmplitudeModulation:
                await ApplyAsync(
                    "Signal.ApplyAmplitudeModulation",
                    [
                        ("target-frequency", Frequency),
                        ("target-attenuation", Amplitude),
                        ("modulation-frequency", ModulationFrequency),
                        ("am-depth", AmplitudeModulationDepth)
                    ],
                    "Amplitude modulation applied.").ConfigureAwait(true);
                break;

            case RfLabSignalMode.FrequencyModulation:
                await ApplyAsync(
                    "Signal.ApplyFrequencyModulation",
                    [
                        ("target-frequency", Frequency),
                        ("target-attenuation", Amplitude),
                        ("modulation-frequency", ModulationFrequency),
                        ("fm-deviation", FrequencyDeviation)
                    ],
                    "Frequency modulation applied.").ConfigureAwait(true);
                break;

            case RfLabSignalMode.Measure:
                IsMeasurementActive = true;
                break;

            case RfLabSignalMode.Sweep:
                // A sweep runs on the Start control, not on mode selection.
                break;
        }

        if (mode != RfLabSignalMode.Measure)
        {
            IsMeasurementActive = false;
        }
    }

    /// <summary>
    /// Steps the carrier across the sweep span and reads the detector at each
    /// step, plotting the response over frequency.
    /// </summary>
    /// <remarks>
    /// This is the original panel's ANALYZE mode. It is orchestrated here
    /// rather than on the node, because the instrument has no swept-measurement
    /// function: each point is a staged frequency, an applied carrier, a
    /// settling delay, and a detector read. The pace is therefore bounded by
    /// the round trip to the Runtime Host, and a point takes longer than it
    /// did when the original application owned the serial port.
    ///
    /// The sweep duration sets the settling delay per point, with the
    /// original's floor. When the run ends — completed or stopped — the
    /// carrier returns to the panel's own frequency and attenuation, as the
    /// original did, so the generator is never left parked at the last step.
    /// </remarks>
    private async Task StartAnalyzeAsync()
    {
        CancelAnalyze();
        var cancellation = new CancellationTokenSource();
        analyzeCancellation = cancellation;
        CancellationToken cancellationToken = cancellation.Token;

        int points = Math.Clamp(MeasurementCount, MinimumAnalyzePoints, MaximumMeasurementPoints);
        int start = SweepStartFrequency;
        int stop = SweepStopFrequency;

        if (stop <= start)
        {
            Fail("Analyze requires a stop frequency above the start frequency.");
            IsSweepActive = false;
            return;
        }

        int sweepTime = Math.Max(SweepTime, MinimumAnalyzeSweepTimeMs);
        double settlingDelay = sweepTime / (double)points;
        double step = (stop - start) / (double)points;

        ClearMeasurement();
        IsMeasurementActive = false;

        try
        {
            for (int index = 0; index < points; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var frequency = (int)Math.Round(start + (step * index));
                long startedTimestamp = Environment.TickCount64;

                if (!await ApplyAnalyzePointAsync(frequency, cancellationToken)
                    .ConfigureAwait(true))
                {
                    return;
                }

                double elapsed = Environment.TickCount64 - startedTimestamp;
                if (settlingDelay > elapsed)
                {
                    await scheduler.DelayAsync(
                        TimeSpan.FromMilliseconds(settlingDelay - elapsed),
                        cancellationToken).ConfigureAwait(true);
                }

                RemotePropertyOperationResult reading = await operations
                    .ReadAsync(SelectedSensor.PropertyId, cancellationToken)
                    .ConfigureAwait(true);

                if (!reading.IsSuccess
                    || reading.ConfirmedValue?.Value?.NumericValue is not double value)
                {
                    Fail($"Analyze stopped: detector read failed ({reading.Status}).");
                    return;
                }

                AddMeasurementPoint(frequency, value);
                SensorValueString = value.ToString("0.0", CultureInfo.CurrentCulture);
                SensorValueNormalized = Normalize(value);
                StatusInfo =
                    $"Analyze {index + 1}/{points} at "
                    + $"{frequency / 1_000_000.0:0.###} MHz.";
            }

            ErrorStatus = false;
            StatusInfo = $"Analyze complete: {points} points.";
        }
        catch (OperationCanceledException)
        {
            StatusInfo = "Analyze stopped.";
        }
        catch (Exception exception)
        {
            Fail($"Analyze stopped: {exception.Message}");
        }
        finally
        {
            await RestoreCarrierAfterAnalyzeAsync().ConfigureAwait(true);

            if (ReferenceEquals(analyzeCancellation, cancellation))
            {
                analyzeCancellation = null;
            }

            cancellation.Dispose();

            if (isSweepActive)
            {
                SetProperty(ref isSweepActive, false, nameof(IsSweepActive));
                RaisePropertyChanged(nameof(IsSweepInactive));
            }
        }
    }

    private async Task<bool> ApplyAnalyzePointAsync(
        int frequency,
        CancellationToken cancellationToken)
    {
        RemotePropertyOperationResult staged = await operations
            .WriteAsync(
                "target-frequency",
                RemoteValue.FromNumeric(frequency),
                cancellationToken)
            .ConfigureAwait(true);

        if (!staged.IsSuccess)
        {
            Fail($"Analyze stopped: target-frequency {staged.Status}.");
            return false;
        }

        RemoteCommandOperationResult applied = await operations
            .ExecuteAsync("Signal.ApplyCarrier", argument: null, cancellationToken)
            .ConfigureAwait(true);

        if (!applied.IsSuccess)
        {
            Fail($"Analyze stopped: Signal.ApplyCarrier {applied.Status}.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Returns the generator to the panel's own carrier once an analysis run
    /// ends, so it is never left parked at the last analysed step.
    /// </summary>
    private async Task RestoreCarrierAfterAnalyzeAsync()
    {
        try
        {
            RemotePropertyOperationResult staged = await operations
                .WriteAsync("target-frequency", RemoteValue.FromNumeric(Frequency))
                .ConfigureAwait(true);

            if (staged.IsSuccess)
            {
                _ = await operations
                    .ExecuteAsync("Signal.ApplyCarrier")
                    .ConfigureAwait(true);
            }
        }
        catch (Exception exception)
        {
            Fail(
                "The analysed carrier could not be restored: "
                + $"{exception.Message}");
        }
    }

    private void CancelAnalyze()
    {
        CancellationTokenSource? cancellation = analyzeCancellation;
        analyzeCancellation = null;

        if (cancellation is null)
        {
            return;
        }

        try
        {
            cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task StartSweepAsync()
    {
        if (!SweepCommandPaths.TryGetValue(SelectedSweepMode, out string? commandPath))
        {
            StatusInfo = "The selected sweep mode is not supported.";
            IsSweepActive = false;
            return;
        }

        ClearMeasurement();
        await ApplyAsync(
            commandPath,
            [
                ("sweep-start-frequency", SweepStartFrequency),
                ("sweep-stop-frequency", SweepStopFrequency),
                ("sweep-time", SweepTime),
                ("target-attenuation", Amplitude)
            ],
            "Sweep started.").ConfigureAwait(true);
    }

    private async Task ApplyAsync(
        string commandPath,
        IReadOnlyList<(string PropertyId, int Value)> targets,
        string successMessage)
    {
        IsUIEnabled = false;

        try
        {
            foreach ((string propertyId, int value) in targets)
            {
                RemotePropertyOperationResult written = await operations
                    .WriteAsync(propertyId, RemoteValue.FromNumeric(value))
                    .ConfigureAwait(true);

                if (!written.IsSuccess)
                {
                    Fail($"{propertyId}: {written.Status}.");
                    return;
                }
            }

            RemoteCommandOperationResult executed = await operations
                .ExecuteAsync(commandPath)
                .ConfigureAwait(true);

            if (executed.IsSuccess)
            {
                ErrorStatus = false;
                StatusInfo = successMessage;
            }
            else
            {
                Fail($"{commandPath}: {executed.Status}. {executed.Diagnostic}");
            }
        }
        catch (Exception exception)
        {
            Fail(exception.Message);
        }
        finally
        {
            IsUIEnabled = true;
        }
    }

    private async Task ToggleIndicatorAsync()
    {
        RemoteCommandOperationResult result = await operations
            .ExecuteAsync(LED ? "Indicator.SwitchOff" : "Indicator.SwitchOn")
            .ConfigureAwait(true);

        if (result.IsSuccess)
        {
            LED = !LED;
            ErrorStatus = false;
            return;
        }

        Fail($"Indicator: {result.Status}.");
    }

    private void StartMeasurementLoop()
    {
        StopMeasurementLoop();
        measurementLoop = scheduler.Schedule(
            TimeSpan.FromMilliseconds(Math.Max(10, MeasurementInterval)),
            async () => await ReadSensorAsync().ConfigureAwait(true));
    }

    private void StopMeasurementLoop()
    {
        measurementLoop?.Dispose();
        measurementLoop = null;
    }

    private async Task ReadSensorAsync(CancellationToken cancellationToken = default)
    {
        RemotePropertyOperationResult result = await operations
            .ReadAsync(SelectedSensor.PropertyId, cancellationToken)
            .ConfigureAwait(true);

        if (!result.IsSuccess
            || result.ConfirmedValue?.Value?.NumericValue is not double value)
        {
            Fail($"Detector read failed ({result.Status}).");
            return;
        }

        ErrorStatus = false;
        SensorValueString = value.ToString("0.0", CultureInfo.CurrentCulture);
        SensorValueNormalized = Normalize(value);
        AddMeasurementPoint(value);
    }

    private void AddMeasurementPoint(double value)
    {
        AddMeasurementPoint(
            IsModeMEASURE
                ? measurementIndex
                : Xmin + ((Xmax - Xmin)
                    * (measurementIndex / (double)MaximumMeasurementPoints)),
            value);
    }

    /// <summary>
    /// Adds one sample at an explicit abscissa, which an analysis run knows
    /// exactly because it commanded the frequency.
    /// </summary>
    private void AddMeasurementPoint(double abscissa, double value)
    {
        if (measurementData.Count >= MaximumMeasurementPoints)
        {
            ClearMeasurement();
        }

        measurementData.Add(new RfLabMeasurementPoint(abscissa, value));
        measurementIndex++;
    }

    private double Normalize(double value)
    {
        double span = SelectedSensor.Maximum - SelectedSensor.Minimum;
        return span <= 0
            ? 0
            : Math.Clamp((value - SelectedSensor.Minimum) / span, 0, 1);
    }

    private void ClearMeasurement()
    {
        measurementData.Clear();
        measurementIndex = 0;
        RaisePropertyChanged(nameof(Xlabel));
        RaisePropertyChanged(nameof(Xmin));
        RaisePropertyChanged(nameof(Xmax));
    }

    private async Task<string> ReadTextAsync(
        string propertyId,
        CancellationToken cancellationToken)
    {
        RemotePropertyOperationResult result = await operations
            .ReadAsync(propertyId, cancellationToken)
            .ConfigureAwait(true);

        return result.IsSuccess
            ? result.ConfirmedValue?.Value?.StringValue ?? string.Empty
            : string.Empty;
    }

    private void Fail(string message)
    {
        ErrorStatus = true;
        StatusInfo = message;
    }
}
