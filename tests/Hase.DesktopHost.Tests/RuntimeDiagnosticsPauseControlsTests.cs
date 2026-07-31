using System.IO;
using System.Xml.Linq;

namespace Hase.DesktopHost.Tests;

public sealed class RuntimeDiagnosticsPauseControlsTests
{
    [Fact]
    public void DiagnosticsWindow_ShouldBindPauseAndResumeControls()
    {
        XDocument document =
            LoadDiagnosticsWindow();
        string content =
            document.ToString(
                SaveOptions.DisableFormatting);

        Assert.Contains(
            "PausePresentationCommand",
            content,
            StringComparison.Ordinal);
        Assert.Contains(
            "ResumePresentationCommand",
            content,
            StringComparison.Ordinal);
        Assert.Contains(
            "PresentationStatusText",
            content,
            StringComparison.Ordinal);
        Assert.Contains(
            "PresentationStatusDescription",
            content,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DiagnosticsWindow_ShouldExposeAccessiblePauseAndResumeLabels()
    {
        XDocument document =
            LoadDiagnosticsWindow();
        XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        XElement pause =
            document
                .Descendants(
                    presentation + "Button")
                .Single(
                    element =>
                        string.Equals(
                            (string?)element.Attribute(
                                "Content"),
                            "Pause",
                            StringComparison.Ordinal));
        XElement resume =
            document
                .Descendants(
                    presentation + "Button")
                .Single(
                    element =>
                        string.Equals(
                            (string?)element.Attribute(
                                "Content"),
                            "Resume",
                            StringComparison.Ordinal));

        Assert.Equal(
            "Pause diagnostics presentation",
            (string?)pause.Attribute(
                "AutomationProperties.Name"));
        Assert.Equal(
            "Resume diagnostics presentation",
            (string?)resume.Attribute(
                "AutomationProperties.Name"));
        Assert.NotNull(
            pause.Attribute(
                "ToolTip"));
        Assert.NotNull(
            resume.Attribute(
                "ToolTip"));
    }

    private static XDocument LoadDiagnosticsWindow()
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
                    "DiagnosticsWindow.xaml");

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
            "Could not locate DiagnosticsWindow.xaml.");
    }
}
