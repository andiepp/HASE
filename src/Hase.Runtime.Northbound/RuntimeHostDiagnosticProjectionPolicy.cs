using Hase.Runtime.Diagnostics;

namespace Hase.Runtime.Northbound;

/// <summary>
/// Defines the explicit local disclosure ceiling for remote Runtime Host
/// diagnostic projection.
/// </summary>
public sealed record RuntimeHostDiagnosticProjectionPolicy
{
    public RuntimeHostDiagnosticProjectionPolicy(
        bool isEnabled = false,
        RuntimeDiagnosticLevel maximumLevel =
            RuntimeDiagnosticLevel.Operational)
    {
        if (!Enum.IsDefined(maximumLevel))
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumLevel),
                maximumLevel,
                "The diagnostic projection level is not defined.");
        }

        IsEnabled = isEnabled;
        MaximumLevel = maximumLevel;
    }

    public bool IsEnabled { get; }

    public RuntimeDiagnosticLevel MaximumLevel { get; }

    public bool Allows(RuntimeDiagnosticLevel level)
    {
        return IsEnabled
            && Enum.IsDefined(level)
            && level <= MaximumLevel;
    }
}
