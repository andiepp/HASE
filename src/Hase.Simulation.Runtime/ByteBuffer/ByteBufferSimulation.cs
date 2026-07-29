using Hase.Core.Domain.Data;

namespace Hase.Simulation.Runtime.ByteBuffer;

/// <summary>
/// Owns the authoritative opaque contents of one simulated byte buffer.
/// </summary>
public sealed class ByteBufferSimulation
{
    public const double MinimumSetpoint = -40.0;
    public const double MaximumSetpoint = 125.0;

    public bool Enabled
    {
        get;
        private set;
    }

    public double Setpoint
    {
        get;
        private set;
    } = 20.0;

    public string Label
    {
        get;
        private set;
    } = "HASE";

    private ByteArrayValue value =
        new(
            ReadOnlySpan<byte>.Empty);

    /// <summary>
    /// Gets the current immutable buffer value.
    /// </summary>
    public ByteArrayValue Value =>
        value;

    /// <summary>
    /// Replaces the current buffer without interpreting its contents.
    /// </summary>
    public void Replace(
        ByteArrayValue replacement)
    {
        value =
            replacement
            ?? throw new ArgumentNullException(
                nameof(replacement));
    }

    public void SetEnabled(
        bool enabled)
    {
        Enabled =
            enabled;
    }

    public bool TrySetSetpoint(
        double setpoint)
    {
        if (!double.IsFinite(
                setpoint)
            || setpoint < MinimumSetpoint
            || setpoint > MaximumSetpoint)
        {
            return false;
        }

        Setpoint =
            setpoint;
        return true;
    }

    public void SetLabel(
        string label)
    {
        Label =
            label
            ?? throw new ArgumentNullException(
                nameof(label));
    }
}
