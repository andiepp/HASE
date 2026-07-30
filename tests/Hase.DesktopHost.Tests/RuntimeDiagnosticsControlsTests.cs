using Hase.DesktopHost.App.ViewModels;
using Hase.Runtime.Diagnostics;

namespace Hase.DesktopHost.Tests;

public sealed class RuntimeDiagnosticsControlsTests
{
    [Theory]
    [InlineData(
        RuntimeDiagnosticLevel.Operational,
        1)]
    [InlineData(
        RuntimeDiagnosticLevel.Protocol,
        2)]
    [InlineData(
        RuntimeDiagnosticLevel.Bytes,
        3)]
    public void Constructor_ExposesOnlyCapturedDisplayLevels(
        RuntimeDiagnosticLevel maximumLevel,
        int expectedLevelCount)
    {
        var viewModel =
            new RuntimeDiagnosticsViewModel(
                new DesktopRuntimeDiagnosticSession(
                    maximumLevel));

        Assert.Equal(
            maximumLevel,
            viewModel.CaptureMaximumLevel);
        Assert.Equal(
            maximumLevel.ToString(),
            viewModel.CaptureMaximumLevelText);
        Assert.Equal(
            expectedLevelCount,
            viewModel.AvailableDisplayLevels.Count);
        Assert.Equal(
            maximumLevel,
            viewModel.SelectedDisplayMaximumLevel);
    }

    [Fact]
    public void DisplayFilter_IsCumulativeAndRetainsHiddenRecords()
    {
        var session =
            new DesktopRuntimeDiagnosticSession(
                RuntimeDiagnosticLevel.Bytes);

        Publish(
            session,
            RuntimeDiagnosticLevel.Operational,
            "operational");
        Publish(
            session,
            RuntimeDiagnosticLevel.Protocol,
            "protocol");
        Publish(
            session,
            RuntimeDiagnosticLevel.Bytes,
            "bytes");

        var viewModel =
            new RuntimeDiagnosticsViewModel(
                session);

        viewModel.Refresh();
        viewModel.SelectedEntry =
            viewModel.Entries[0];

        viewModel.SelectedDisplayMaximumLevel =
            RuntimeDiagnosticLevel.Protocol;

        Assert.Equal(
            3,
            viewModel.RetainedEntryCount);
        Assert.Equal(
            2,
            viewModel.DisplayedEntryCount);
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
            "protocol",
            viewModel.SelectedEntry!.EventName);

        viewModel.SelectedDisplayMaximumLevel =
            RuntimeDiagnosticLevel.Bytes;

        Assert.Equal(
            3,
            viewModel.DisplayedEntryCount);
        Assert.Equal(
            "bytes",
            viewModel.Entries[0].EventName);
    }

    [Fact]
    public void DisplayFilter_AboveCaptureLevel_ShouldThrow()
    {
        var viewModel =
            new RuntimeDiagnosticsViewModel(
                new DesktopRuntimeDiagnosticSession(
                    RuntimeDiagnosticLevel.Operational));

        Assert.Throws<ArgumentOutOfRangeException>(
            "value",
            () =>
                viewModel.SelectedDisplayMaximumLevel =
                    RuntimeDiagnosticLevel.Protocol);
    }

    [Fact]
    public void ClearCommand_ClearsRetainedAndDisplayedRecords()
    {
        var session =
            new DesktopRuntimeDiagnosticSession();
        Publish(
            session,
            RuntimeDiagnosticLevel.Operational,
            "operational");

        var viewModel =
            new RuntimeDiagnosticsViewModel(
                session);

        viewModel.Refresh();

        Assert.True(
            viewModel.ClearDiagnosticsCommand.CanExecute());

        viewModel.ClearDiagnosticsCommand.Execute();

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

    [Fact]
    public void ClearCommand_EmptySession_ShouldBeDisabled()
    {
        var viewModel =
            new RuntimeDiagnosticsViewModel(
                new DesktopRuntimeDiagnosticSession());

        Assert.False(
            viewModel.ClearDiagnosticsCommand.CanExecute());
    }

    [Fact]
    public void BytesCapture_ExposesPayloadWarningState()
    {
        var operational =
            new RuntimeDiagnosticsViewModel(
                new DesktopRuntimeDiagnosticSession(
                    RuntimeDiagnosticLevel.Operational));

        var bytes =
            new RuntimeDiagnosticsViewModel(
                new DesktopRuntimeDiagnosticSession(
                    RuntimeDiagnosticLevel.Bytes));

        Assert.False(
            operational.IsByteCaptureEnabled);
        Assert.True(
            bytes.IsByteCaptureEnabled);
    }

    private static void Publish(
        DesktopRuntimeDiagnosticSession session,
        RuntimeDiagnosticLevel level,
        string eventName)
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
