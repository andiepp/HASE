#nullable enable

using System.IO;

namespace Hase.Client.Wpf.RfLab.Presets;

/// <summary>
/// Reads presets from a directory of text files, one preset per file, named
/// as the original application named them.
/// </summary>
/// <remarks>
/// The directory is supplied by the application that composes the panel
/// rather than discovered here, because a path that exists on one computer
/// need not exist on another and the panel project carries no machine
/// knowledge.
///
/// Every failure is treated as an absence. A missing directory, a denied
/// read and an unreadable file all mean the operator sees fewer presets,
/// which is a smaller problem than a panel that will not open.
/// </remarks>
public sealed class RfLabPresetDirectoryStore : IRfLabPresetStore
{
    private const string PresetFilePattern = "*.txt";

    private readonly string directoryPath;

    public RfLabPresetDirectoryStore(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        this.directoryPath = directoryPath;
    }

    /// <summary>
    /// The directory the client keeps presets in when the application names
    /// no other. It sits beside the client's other configuration so that it
    /// survives an application update, which replaces only the program.
    /// </summary>
    public static string DefaultDirectoryPath =>
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "HASE",
            "Client",
            "Configuration",
            "RfLabPresets");

    /// <inheritdoc />
    public IReadOnlyList<string> ListNames()
    {
        try
        {
            if (!Directory.Exists(directoryPath))
            {
                return [];
            }

            return [.. Directory
                .EnumerateFiles(directoryPath, PresetFilePattern)
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)];
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <inheritdoc />
    public RfLabPreset? Read(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        try
        {
            string filePath = Path.Combine(directoryPath, name + ".txt");

            // A name is an entry this store listed, so it must resolve to a
            // file directly inside the directory. Anything else is refused
            // rather than followed.
            string resolved = Path.GetFullPath(filePath);
            string resolvedDirectory = Path.GetFullPath(directoryPath);
            if (!string.Equals(
                    Path.GetDirectoryName(resolved),
                    resolvedDirectory.TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return File.Exists(resolved)
                ? RfLabPreset.FromFile(resolved)
                : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
