using System.IO;
using System.Xml.Linq;

namespace Hase.DesktopHost.Tests;

public sealed class RuntimeDiagnosticsWindowPresentationTests
{
    [Fact]
    public void DiagnosticsWindow_ShouldContainCompleteDiagnosticControls()
    {
        XDocument document =
            LoadView(
                "DiagnosticsWindow.xaml");

        string content =
            document.ToString(
                SaveOptions.DisableFormatting);

        Assert.Contains(
            "CaptureMaximumLevelText",
            content,
            StringComparison.Ordinal);
        Assert.Contains(
            "AvailableDisplayLevels",
            content,
            StringComparison.Ordinal);
        Assert.Contains(
            "ClearDiagnosticsCommand",
            content,
            StringComparison.Ordinal);
        Assert.Contains(
            "DisplayedEntryCount",
            content,
            StringComparison.Ordinal);
        Assert.Contains(
            "RetainedEntryCount",
            content,
            StringComparison.Ordinal);
        Assert.Contains(
            "SelectedEntry.ByteHex",
            content,
            StringComparison.Ordinal);
        Assert.Contains(
            "IsByteCaptureEnabled",
            content,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DiagnosticsWindow_ShouldBindDirectlyToSharedViewModel()
    {
        XDocument document =
            LoadView(
                "DiagnosticsWindow.xaml");

        Assert.DoesNotContain(
            "Diagnostics.",
            document.ToString(
                SaveOptions.DisableFormatting),
            StringComparison.Ordinal);
    }

    private static XDocument LoadView(
        string fileName)
    {
        string? directory =
            AppContext.BaseDirectory;

        while (directory is not null)
        {
            string candidate =
                Path.Combine(
                    directory,
                    "src",
                    "Hase.DesktopHost.App",
                    "Views",
                    fileName);

            if (File.Exists(
                    candidate))
            {
                return XDocument.Load(
                    candidate);
            }

            directory =
                Directory.GetParent(
                    directory)?.FullName;
        }

        throw new InvalidOperationException(
            $"Could not locate {fileName}.");
    }
}
