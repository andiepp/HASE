using Hase.Core.Domain.Data;

namespace Hase.Mcnf.RfLab;

/// <summary>
/// Quantities and units of the normalized RF-Lab model that are not part of
/// the shared core set.
/// </summary>
public static class RfLabUnits
{
    public static readonly Quantity Attenuation =
        new("attenuation", "Attenuation");

    public static readonly Quantity PowerLevel =
        new("power-level", "Power Level");

    public static readonly Quantity Ratio =
        new("ratio", "Ratio");

    public static readonly Unit DecibelAttenuation =
        new("decibel-attenuation", "Decibel", "dB", Attenuation);

    public static readonly Unit DecibelLevel =
        new("decibel-level", "Decibel", "dB", PowerLevel);

    public static readonly Unit Percent =
        new("percent", "Percent", "%", Ratio);

    public static readonly Unit Millisecond =
        new("millisecond", "Millisecond", "ms", Quantities.Time);
}
