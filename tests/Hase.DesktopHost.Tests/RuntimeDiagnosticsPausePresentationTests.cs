using Hase.DesktopHost.App.ViewModels;

namespace Hase.DesktopHost.Tests;

public sealed class RuntimeDiagnosticsPausePresentationTests
{
    [Fact]
    public void Constructor_ShouldExposeRunningPresentationText()
    {
        var viewModel =
            new RuntimeDiagnosticsViewModel();

        Assert.Equal(
            "Presentation: Running",
            viewModel.PresentationStatusText);
        Assert.Equal(
            "Presentation updates automatically from the retained diagnostic session.",
            viewModel.PresentationStatusDescription);
    }

    [Fact]
    public void Pause_ShouldExposePausedPresentationText()
    {
        var viewModel =
            new RuntimeDiagnosticsViewModel();

        viewModel.PausePresentationCommand.Execute();

        Assert.Equal(
            "Presentation: Paused",
            viewModel.PresentationStatusText);
        Assert.Equal(
            "Presentation is paused. Diagnostic capture and bounded retention continue.",
            viewModel.PresentationStatusDescription);
    }

    [Fact]
    public void PauseAndResume_ShouldNotifyPresentationTextProperties()
    {
        var viewModel =
            new RuntimeDiagnosticsViewModel();
        var changedProperties =
            new List<string?>();

        viewModel.PropertyChanged +=
            (_, eventArgs) =>
                changedProperties.Add(
                    eventArgs.PropertyName);

        viewModel.PausePresentationCommand.Execute();
        viewModel.ResumePresentationCommand.Execute();

        Assert.Equal(
            2,
            changedProperties.Count(
                propertyName =>
                    propertyName
                    == nameof(
                        RuntimeDiagnosticsViewModel
                            .PresentationStatusText)));
        Assert.Equal(
            2,
            changedProperties.Count(
                propertyName =>
                    propertyName
                    == nameof(
                        RuntimeDiagnosticsViewModel
                            .PresentationStatusDescription)));
    }
}
