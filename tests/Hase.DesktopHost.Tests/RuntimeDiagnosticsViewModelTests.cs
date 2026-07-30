using Hase.DesktopHost.App.ViewModels;
using Hase.Runtime.Diagnostics;

namespace Hase.DesktopHost.Tests;

public sealed class RuntimeDiagnosticsViewModelTests
{
    [Fact]
    public void Refresh_InitialSnapshot_ProjectsNewestFirst()
    {
        DesktopRuntimeDiagnosticSession session =
            CreateSession();
        Publish(
            session,
            "first");
        Publish(
            session,
            "second");

        var viewModel =
            new RuntimeDiagnosticsViewModel(
                session);

        viewModel.Refresh();

        Assert.Equal(
            [
                "second",
                "first"
            ],
            viewModel.Entries
                .Select(
                    entry =>
                        entry.EventName)
                .ToArray());
        Assert.Equal(
            2,
            viewModel.RetainedEntryCount);
        Assert.False(
            viewModel.IsEmpty);
        Assert.Same(
            viewModel.Entries[0],
            viewModel.SelectedEntry);
    }

    [Fact]
    public void Refresh_NewRecord_IsIncrementalIdempotentAndPreservesSelection()
    {
        DesktopRuntimeDiagnosticSession session =
            CreateSession();
        Publish(
            session,
            "first");
        Publish(
            session,
            "second");

        var viewModel =
            new RuntimeDiagnosticsViewModel(
                session);

        viewModel.Refresh();

        DesktopRuntimeDiagnosticEntry selected =
            viewModel.Entries[1];
        viewModel.SelectedEntry =
            selected;

        Publish(
            session,
            "third");

        viewModel.Refresh();
        viewModel.Refresh();

        Assert.Equal(
            [
                "third",
                "second",
                "first"
            ],
            viewModel.Entries
                .Select(
                    entry =>
                        entry.EventName)
                .ToArray());
        Assert.Same(
            selected,
            viewModel.SelectedEntry);
    }

    [Fact]
    public void Refresh_EmptySnapshot_ClearsEntriesAndSelection()
    {
        DesktopRuntimeDiagnosticSession session =
            CreateSession();
        Publish(
            session,
            "first");

        var viewModel =
            new RuntimeDiagnosticsViewModel(
                session);

        viewModel.Refresh();
        session.ClearDiagnostics();
        viewModel.Refresh();

        Assert.Empty(
            viewModel.Entries);
        Assert.Equal(
            0,
            viewModel.RetainedEntryCount);
        Assert.True(
            viewModel.IsEmpty);
        Assert.False(
            viewModel.HasSelection);
        Assert.Null(
            viewModel.SelectedEntry);
    }

    [Fact]
    public void Refresh_ClearFollowedByNewRecord_RebuildsFromSource()
    {
        DesktopRuntimeDiagnosticSession session =
            CreateSession();
        Publish(
            session,
            "first");
        Publish(
            session,
            "second");

        var viewModel =
            new RuntimeDiagnosticsViewModel(
                session);

        viewModel.Refresh();

        session.ClearDiagnostics();
        Publish(
            session,
            "after-clear");
        viewModel.Refresh();

        Assert.Equal(
            "after-clear",
            Assert.Single(
                viewModel.Entries).EventName);
    }

    [Fact]
    public void Refresh_ReplacementSession_ReconcilesRestartedSequence()
    {
        DesktopRuntimeDiagnosticSession firstSession =
            CreateSession();
        Publish(
            firstSession,
            "previous-session");

        var source =
            new SwitchableDiagnosticSource(
                firstSession);
        var viewModel =
            new RuntimeDiagnosticsViewModel(
                source);

        viewModel.Refresh();

        DesktopRuntimeDiagnosticSession replacementSession =
            CreateSession();
        Publish(
            replacementSession,
            "replacement-session");
        source.Current =
            replacementSession;

        viewModel.Refresh();

        Assert.Equal(
            "replacement-session",
            Assert.Single(
                viewModel.Entries).EventName);
    }

    [Fact]
    public void Refresh_SelectedEvictedEntry_SelectsNewestRetainedEntry()
    {
        var session =
            new DesktopRuntimeDiagnosticSession(
                RuntimeDiagnosticLevel.Operational,
                capacity: 2);
        Publish(
            session,
            "first");
        Publish(
            session,
            "second");

        var viewModel =
            new RuntimeDiagnosticsViewModel(
                session);

        viewModel.Refresh();
        viewModel.SelectedEntry =
            viewModel.Entries[1];

        Publish(
            session,
            "third");
        viewModel.Refresh();

        Assert.Equal(
            [
                "third",
                "second"
            ],
            viewModel.Entries
                .Select(
                    entry =>
                        entry.EventName)
                .ToArray());
        Assert.Equal(
            "third",
            viewModel.SelectedEntry!.EventName);
    }

    private static DesktopRuntimeDiagnosticSession CreateSession()
    {
        return new DesktopRuntimeDiagnosticSession();
    }

    private static void Publish(
        DesktopRuntimeDiagnosticSession session,
        string eventName)
    {
        session.Publisher.Publish(
            new RuntimeDiagnosticEvent(
                RuntimeDiagnosticLevel.Operational,
                RuntimeDiagnosticCategory.RuntimeConnection,
                eventName));
    }

    private sealed class SwitchableDiagnosticSource(
        IDesktopRuntimeDiagnosticSource current)
        : IDesktopRuntimeDiagnosticSource
    {
        public IDesktopRuntimeDiagnosticSource Current
        {
            get;
            set;
        } =
            current;

        public IReadOnlyList<RuntimeDiagnosticRecord> CaptureDiagnostics()
        {
            return Current.CaptureDiagnostics();
        }

        public void ClearDiagnostics()
        {
            Current.ClearDiagnostics();
        }
    }
}
