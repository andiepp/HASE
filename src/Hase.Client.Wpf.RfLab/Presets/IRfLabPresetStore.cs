#nullable enable

namespace Hase.Client.Wpf.RfLab.Presets;

/// <summary>
/// Supplies the panel's stored settings. The panel reads presets and never
/// writes them, so this store is read-only.
/// </summary>
public interface IRfLabPresetStore
{
    /// <summary>
    /// The names of the available presets, in the order they should be
    /// offered. An unreachable or absent store yields none rather than
    /// failing, because a missing preset folder is not a reason to refuse
    /// to operate the instrument.
    /// </summary>
    IReadOnlyList<string> ListNames();

    /// <summary>
    /// Reads one preset by name, or null when it cannot be read.
    /// </summary>
    RfLabPreset? Read(string name);
}
