namespace Hase.Simulation.Values.Periodic;

/// <summary>
/// Generates a periodic value around a defined offset.
/// </summary>
public sealed class PeriodicValueGenerator : IValueGenerator
{
    private const double FullCycleRadians = 2.0 * Math.PI;

    private PeriodicValueGenerator(
        double offset,
        double amplitude,
        TimeSpan period,
        IPeriodicWaveform waveform,
        double phaseOffsetCycles)
    {
        ValidateParameters(offset, amplitude, period, waveform);

        Offset = offset;
        Amplitude = amplitude;
        Period = period;
        Waveform = waveform;
        PhaseOffsetCycles = NormalizePhase(phaseOffsetCycles);

        CurrentValue = CalculateValue(TimeSpan.Zero);
    }

    /// <summary>
    /// Creates a periodic generator without an initial phase offset.
    /// </summary>
    public PeriodicValueGenerator(
        double offset,
        double amplitude,
        TimeSpan period,
        IPeriodicWaveform waveform)
        : this(
            offset,
            amplitude,
            period,
            waveform,
            phaseOffsetCycles: 0.0)
    {
    }

    public double Offset { get; }

    public double Amplitude { get; }

    public TimeSpan Period { get; }

    public IPeriodicWaveform Waveform { get; }

    /// <summary>
    /// Gets the initial phase offset expressed as a fraction of one cycle.
    /// </summary>
    public double PhaseOffsetCycles { get; }

    public double CurrentValue { get; private set; }

    /// <summary>
    /// Creates a periodic generator with its phase offset expressed in radians.
    /// </summary>
    public static PeriodicValueGenerator FromPhaseRadians(
        double offset,
        double amplitude,
        TimeSpan period,
        IPeriodicWaveform waveform,
        double phaseRadians)
    {
        if (!double.IsFinite(phaseRadians))
        {
            throw new ArgumentOutOfRangeException(
                nameof(phaseRadians),
                "Phase must be finite.");
        }

        return new PeriodicValueGenerator(
            offset,
            amplitude,
            period,
            waveform,
            phaseRadians / FullCycleRadians);
    }

    /// <summary>
    /// Creates a periodic generator starting at a defined time position
    /// within its cycle.
    /// </summary>
    public static PeriodicValueGenerator FromTimeOffset(
        double offset,
        double amplitude,
        TimeSpan period,
        IPeriodicWaveform waveform,
        TimeSpan initialTimeOffset)
    {
        if (period <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(period),
                "The period must be greater than zero.");
        }

        var phaseOffsetCycles =
            initialTimeOffset.TotalSeconds / period.TotalSeconds;

        return new PeriodicValueGenerator(
            offset,
            amplitude,
            period,
            waveform,
            phaseOffsetCycles);
    }

    public void Update(SimulationStep step)
    {
        CurrentValue = CalculateValue(step.SimulationTime);
    }

    private double CalculateValue(TimeSpan simulationTime)
    {
        var elapsedCycles =
            simulationTime.TotalSeconds / Period.TotalSeconds;

        var phase = NormalizePhase(
            elapsedCycles + PhaseOffsetCycles);

        var normalizedValue = Waveform.GetValue(phase);

        if (!double.IsFinite(normalizedValue))
        {
            throw new InvalidOperationException(
                "The waveform returned a non-finite value.");
        }

        return Offset + Amplitude * normalizedValue;
    }

    private static double NormalizePhase(double phase)
    {
        if (!double.IsFinite(phase))
        {
            throw new ArgumentOutOfRangeException(
                nameof(phase),
                "Phase must be finite.");
        }

        return phase - Math.Floor(phase);
    }

    private static void ValidateParameters(
        double offset,
        double amplitude,
        TimeSpan period,
        IPeriodicWaveform waveform)
    {
        ArgumentNullException.ThrowIfNull(waveform);

        if (!double.IsFinite(offset))
        {
            throw new ArgumentOutOfRangeException(
                nameof(offset),
                "Offset must be finite.");
        }

        if (!double.IsFinite(amplitude) || amplitude < 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amplitude),
                "Amplitude must be finite and non-negative.");
        }

        if (period <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(period),
                "The period must be greater than zero.");
        }
    }
}