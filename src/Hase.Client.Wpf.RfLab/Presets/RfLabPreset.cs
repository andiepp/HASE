#nullable enable

using System.Globalization;
using System.IO;

namespace Hase.Client.Wpf.RfLab.Presets;

/// <summary>
/// One stored panel setting, as the original application wrote it: a flat
/// list of names and values, one per line, separated by a comma.
/// </summary>
/// <remarks>
/// The file may carry values for surfaces this panel does not present, the
/// message generator among them. Those are read and kept rather than
/// dropped, so that a file written by the original application survives a
/// round trip through this one unchanged.
/// </remarks>
public sealed class RfLabPreset
{
    private readonly IReadOnlyDictionary<string, string> values;

    private RfLabPreset(
        string name,
        IReadOnlyDictionary<string, string> values)
    {
        Name = name;
        this.values = values;
    }

    /// <summary>The preset name, which is the file name without extension.</summary>
    public string Name { get; }

    public string? Description => Text("Description");

    public int? Mode => Number("Mode");

    public int? Frequency => Number("Frequency");

    public int? Amplitude => Number("Amplitude");

    public int? ModulationFrequency => Number("FMod");

    public int? AmplitudeModulationDepth => Number("AMDepth");

    public int? FrequencyDeviation => Number("Fdev");

    public string? SweepMode => Text("SweepMode");

    public string? Sensor => Text("Sensor");

    public int? SweepStartFrequency => Number("Fstart");

    public int? SweepStopFrequency => Number("Fstop");

    public int? SweepTime => Number("Tsweep");

    public int? MeasurementInterval => Number("Tmeasure");

    public int? MeasurementCount => Number("Nmeasure");

    public int? ClockFrequency0 => Number("SI5351Fclk0");

    public int? ClockFrequency1 => Number("SI5351Fclk1");

    public int? ClockFrequency2 => Number("SI5351Fclk2");

    /// <summary>
    /// Reads a preset from the lines of a stored file. Unknown names are
    /// retained; a malformed line is skipped rather than failing the file,
    /// because one bad line should not cost the operator a whole preset.
    /// </summary>
    public static RfLabPreset FromLines(string name, IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(lines);

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            int separator = line.IndexOf(',');
            if (separator <= 0)
            {
                continue;
            }

            string key = line[..separator].Trim();
            string value = line[(separator + 1)..].Trim();
            if (key.Length == 0)
            {
                continue;
            }

            values[key] = value;
        }

        return new RfLabPreset(name, values);
    }

    public static RfLabPreset FromFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        return FromLines(
            Path.GetFileNameWithoutExtension(filePath),
            File.ReadAllLines(filePath));
    }

    private string? Text(string key) =>
        values.TryGetValue(key, out string? value)
        && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;

    private int? Number(string key) =>
        values.TryGetValue(key, out string? value)
        && int.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out int number)
            ? number
            : null;
}
