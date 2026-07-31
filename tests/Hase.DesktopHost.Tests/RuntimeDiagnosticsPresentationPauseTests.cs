using Hase.DesktopHost.App.ViewModels;
using Hase.Runtime.Diagnostics;

namespace Hase.DesktopHost.Tests;

public sealed class RuntimeDiagnosticsPresentationPauseTests
{
    [Fact]
    public void Constructor_ShouldStartWithPresentationRunning()
    {
        var viewModel =
            CreateViewModel();

        Assert.False(
            viewModel.IsPresentationPaused);
        Assert.True(
            viewModel.PausePresentationCommand.CanExecute());
        Assert.False(
            viewModel.ResumePresentationCommand.CanExecute());
    }

    [Fact]
    public void PauseAndResumeCommands_ShouldExposeMutuallyExclusiveState()
    {
        var viewModel =
            CreateViewModel();

        viewModel.PausePresentationCommand.Execute();

        Assert.True(
            viewModel.IsPresentationPaused);
        Assert.False(
            viewModel.PausePresentationCommand.CanExecute());
        Assert.True(
            viewModel.ResumePresentationCommand.CanExecute());

        viewModel.ResumePresentationCommand.Execute();

        Assert.False(
            viewModel.IsPresentationPaused);
        Assert.True(
            viewModel.PausePresentationCommand.CanExecute());
        Assert.False(
            viewModel.ResumePresentationCommand.CanExecute());
    }

    [Fact]
    public void Refresh_WhilePaused_ShouldFreezeEntriesAndSelection()
    {
        var session =
            new DesktopRuntimeDiagnosticSession();
        Publish(
            session,
            "one");
        Publish(
            session,
            "two");

        var viewModel =
            new RuntimeDiagnosticsViewModel(
                session);
        viewModel.Refresh();
        viewModel.SelectedEntry =
            viewModel.Entries[1];
        long selectedSequence =
            viewModel.SelectedEntry.Sequence;

        viewModel.PausePresentationCommand.Execute();
        Publish(
            session,
            "three");
        viewModel.Refresh();

        Assert.Equal(
            [
                "two",
                "one"
            ],
            viewModel.Entries
                .Select(
                    entry =>
                        entry.EventName)
                .ToArray());
        Assert.Equal(
            selectedSequence,
            viewModel.SelectedEntry!.Sequence);
        Assert.Equal(
            3,
            session.CaptureDiagnostics().Count);
    }

    [Fact]
    public void Resume_ShouldImmediatelyReconcileCurrentBoundedSnapshot()
    {
        var session =
            new DesktopRuntimeDiagnosticSession(
                capacity: 2);
        Publish(
            session,
            "one");
        Publish(
            session,
            "two");

        var viewModel =
            new RuntimeDiagnosticsViewModel(
                session);
        viewModel.Refresh();
        viewModel.PausePresentationCommand.Execute();

        Publish(
            session,
            "three");
        Publish(
            session,
            "four");

        viewModel.ResumePresentationCommand.Execute();

        Assert.Equal(
            [
                "four",
                "three"
            ],
            viewModel.Entries
                .Select(
                    entry =>
                        entry.EventName)
                .ToArray());
        Assert.Equal(
            2,
            viewModel.RetainedEntryCount);
    }

    [Fact]
    public void DisplayFilter_WhilePaused_ShouldApplyToFrozenPresentation()
    {
        var session =
            new DesktopRuntimeDiagnosticSession(
                RuntimeDiagnosticLevel.Bytes);
        Publish(
            session,
            "operational",
            RuntimeDiagnosticLevel.Operational);
        Publish(
            session,
            "protocol",
            RuntimeDiagnosticLevel.Protocol);
        Publish(
            session,
            "bytes",
            RuntimeDiagnosticLevel.Bytes);

        var viewModel =
            new RuntimeDiagnosticsViewModel(
                session);
        viewModel.Refresh();
        viewModel.PausePresentationCommand.Execute();

        viewModel.SelectedDisplayMaximumLevel =
            RuntimeDiagnosticLevel.Protocol;

        Assert.True(
            viewModel.IsPresentationPaused);
        Assert.Equal(
            3,
            viewModel.RetainedEntryCount);
        Assert.Equal(
            [
                "protocol",
                "operational"
            ],
            viewModel.Entries
                .Select(
                    entry =>
                        entry.EventName)
                .ToArray());
    }

    [Fact]
    public void Clear_WhilePaused_ShouldClearSourceAndFrozenPresentation()
    {
        var session =
            new DesktopRuntimeDiagnosticSession();
        Publish(
            session,
            "one");

        var viewModel =
            new RuntimeDiagnosticsViewModel(
                session);
        viewModel.Refresh();
        viewModel.PausePresentationCommand.Execute();

        viewModel.ClearDiagnosticsCommand.Execute();

        Assert.True(
            viewModel.IsPresentationPaused);
        Assert.Empty(
            session.CaptureDiagnostics());
        Assert.Empty(
            viewModel.Entries);
        Assert.Equal(
            0,
            viewModel.RetainedEntryCount);
        Assert.False(
            viewModel.ClearDiagnosticsCommand.CanExecute());
    }

    private static RuntimeDiagnosticsViewModel CreateViewModel()
    {
        return new RuntimeDiagnosticsViewModel(
            new DesktopRuntimeDiagnosticSession());
    }

    private static void Publish(
        DesktopRuntimeDiagnosticSession session,
        string eventName,
        RuntimeDiagnosticLevel level =
            RuntimeDiagnosticLevel.Operational)
    {
        session.Publisher.Publish(
            new RuntimeDiagnosticEvent(
                level,
                level == RuntimeDiagnosticLevel.Bytes
                    ? RuntimeDiagnosticCategory.TransportBytes
                    : level == RuntimeDiagnosticLevel.Protocol
                        ? RuntimeDiagnosticCategory.ProtocolExchange
                        : RuntimeDiagnosticCategory.RuntimeConnection,
                eventName,
                direction:
                    level == RuntimeDiagnosticLevel.Bytes
                        ? RuntimeDiagnosticDirection.Inbound
                        : null));
    }
}
