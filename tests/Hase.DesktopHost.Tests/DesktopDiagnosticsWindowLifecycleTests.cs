using Hase.DesktopHost.App.ViewModels;
using Hase.DesktopHost.App.Views;
using Hase.Runtime.Diagnostics;

namespace Hase.DesktopHost.Tests;

public sealed class DesktopDiagnosticsWindowLifecycleTests
{
    [Fact]
    public void Reopen_ShouldUseSameSharedViewModel()
    {
        var viewModel =
            new RuntimeDiagnosticsViewModel();
        var factory =
            new RecordingWindowFactory();
        var service =
            new DesktopDiagnosticsWindowService(
                viewModel,
                factory);

        service.Open();
        factory.Windows[0].Close();
        service.Open();

        Assert.Equal(
            2,
            factory.ViewModels.Count);
        Assert.All(
            factory.ViewModels,
            candidate =>
                Assert.Same(
                    viewModel,
                    candidate));
    }

    [Fact]
    public void Reopen_WhileRunning_ShouldRemainRunning()
    {
        var viewModel =
            new RuntimeDiagnosticsViewModel();
        var factory =
            new RecordingWindowFactory();
        var service =
            new DesktopDiagnosticsWindowService(
                viewModel,
                factory);

        service.Open();
        factory.Windows[0].Close();
        service.Open();

        Assert.False(
            viewModel.IsPresentationPaused);
        Assert.Equal(
            "Presentation: Running",
            viewModel.PresentationStatusText);
    }

    [Fact]
    public void Reopen_WhilePaused_ShouldPreserveProjectionFilterAndSelection()
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
        viewModel.SelectedDisplayMaximumLevel =
            RuntimeDiagnosticLevel.Protocol;
        viewModel.SelectedEntry =
            viewModel.Entries[1];
        long selectedSequence =
            viewModel.SelectedEntry.Sequence;
        viewModel.PausePresentationCommand.Execute();

        var factory =
            new RecordingWindowFactory();
        var service =
            new DesktopDiagnosticsWindowService(
                viewModel,
                factory);

        service.Open();
        factory.Windows[0].Close();
        service.Open();

        Assert.True(
            viewModel.IsPresentationPaused);
        Assert.Equal(
            RuntimeDiagnosticLevel.Protocol,
            viewModel.SelectedDisplayMaximumLevel);
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
        Assert.Equal(
            selectedSequence,
            viewModel.SelectedEntry!.Sequence);
    }

    [Fact]
    public void Resume_AfterClosedPausedWindow_ShouldReconcileCapturedActivity()
    {
        var session =
            new DesktopRuntimeDiagnosticSession();
        Publish(
            session,
            "before");

        var viewModel =
            new RuntimeDiagnosticsViewModel(
                session);
        viewModel.Refresh();
        viewModel.PausePresentationCommand.Execute();

        var factory =
            new RecordingWindowFactory();
        var service =
            new DesktopDiagnosticsWindowService(
                viewModel,
                factory);

        service.Open();
        factory.Windows[0].Close();
        Publish(
            session,
            "while-closed");
        service.Open();

        Assert.Equal(
            [
                "before"
            ],
            viewModel.Entries
                .Select(
                    entry =>
                        entry.EventName)
                .ToArray());

        viewModel.ResumePresentationCommand.Execute();

        Assert.Equal(
            [
                "while-closed",
                "before"
            ],
            viewModel.Entries
                .Select(
                    entry =>
                        entry.EventName)
                .ToArray());
    }

    [Fact]
    public void RepeatedCloseAndReopen_ShouldCreateOneWindowPerCycle()
    {
        var factory =
            new RecordingWindowFactory();
        var service =
            new DesktopDiagnosticsWindowService(
                new RuntimeDiagnosticsViewModel(),
                factory);

        for (int index = 0;
            index < 4;
            index++)
        {
            service.Open();
            service.Open();
            factory.Windows[index].Close();
        }

        Assert.Equal(
            4,
            factory.Windows.Count);
        Assert.All(
            factory.Windows,
            window =>
            {
                Assert.Equal(
                    1,
                    window.ShowCount);
                Assert.Equal(
                    2,
                    window.ActivateCount);
            });
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

    private sealed class RecordingWindowFactory
        : IDesktopDiagnosticsWindowFactory
    {
        public List<RuntimeDiagnosticsViewModel> ViewModels
        {
            get;
        } =
            [];

        public List<RecordingWindow> Windows
        {
            get;
        } =
            [];

        public IDesktopModelessWindow Create(
            RuntimeDiagnosticsViewModel diagnosticsViewModel)
        {
            ViewModels.Add(
                diagnosticsViewModel);

            var window =
                new RecordingWindow();
            Windows.Add(
                window);
            return window;
        }
    }

    private sealed class RecordingWindow
        : IDesktopModelessWindow
    {
        public event EventHandler? Closed;

        public bool IsMinimized =>
            false;

        public int ShowCount
        {
            get;
            private set;
        }

        public int ActivateCount
        {
            get;
            private set;
        }

        public void Restore()
        {
        }

        public void ShowWindow()
        {
            ShowCount++;
        }

        public void ActivateWindow()
        {
            ActivateCount++;
        }

        public void Close()
        {
            Closed?.Invoke(
                this,
                EventArgs.Empty);
        }
    }
}
