using System.IO;
using Hase.DesktopHost.App.ViewModels;
using Hase.Diagnostics.Export;
using Hase.Runtime.Diagnostics;

namespace Hase.DesktopHost.Tests;

public sealed class RuntimeDiagnosticsExportTests : IDisposable
{
    private static readonly DateTimeOffset ExportClock =
        new(2026, 8, 23, 15, 30, 0, TimeSpan.Zero);

    private readonly string directory;

    public RuntimeDiagnosticsExportTests()
    {
        directory =
            Path.Combine(
                Path.GetTempPath(),
                "hase-runtime-diagnostics-export-tests-"
                + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(
            directory);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(
                directory,
                recursive: true);
        }
    }

    [Fact]
    public async Task Export_WritesCompleteRetainedSessionIndependentOfDisplayFilter()
    {
        DesktopRuntimeDiagnosticSession session =
            CreateBytesSession();
        PublishOperational(
            session,
            "OperationalEvent");
        PublishBytes(
            session,
            "BytesEvent");

        StubExportDialogService dialogService =
            new(NewTargetPath());
        RuntimeDiagnosticsViewModel viewModel =
            CreateViewModel(
                session,
                dialogService);
        viewModel.Refresh();
        viewModel.SelectedDisplayMaximumLevel =
            RuntimeDiagnosticLevel.Operational;

        Assert.Equal(
            1,
            viewModel.DisplayedEntryCount);

        await viewModel.ExportDiagnosticsAsync();

        DiagnosticExportDocument document =
            await DiagnosticExportFile.ReadAsync(
                dialogService.TargetPath!);

        Assert.Equal(
            DiagnosticExportApplications.RuntimeHost,
            document.Envelope.Application);
        Assert.Equal(
            "Bytes",
            document.Envelope.CaptureLevel);
        Assert.Equal(
            "host-under-test",
            document.Envelope.RuntimeHostId);
        Assert.Equal(
            ExportClock,
            document.Envelope.ExportedAtUtc);
        Assert.Equal(
            2,
            document.Envelope.RecordCount);
        Assert.Equal(
            [
                "OperationalEvent",
                "BytesEvent"
            ],
            document.Records
                .Select(
                    record =>
                        record.EventName)
                .ToArray());
        Assert.Equal(
            "runtime-host-diagnostics-20260823-153000Z.jsonl",
            dialogService.SuggestedFileName);
    }

    [Fact]
    public async Task Export_IncludesRecordsRetainedWhilePresentationPaused()
    {
        DesktopRuntimeDiagnosticSession session =
            CreateBytesSession();
        PublishOperational(
            session,
            "BeforePause");

        StubExportDialogService dialogService =
            new(NewTargetPath());
        RuntimeDiagnosticsViewModel viewModel =
            CreateViewModel(
                session,
                dialogService);
        viewModel.Refresh();
        viewModel.PausePresentationCommand.Execute();

        PublishOperational(
            session,
            "DuringPause");
        viewModel.Refresh();

        Assert.Equal(
            1,
            viewModel.RetainedEntryCount);

        await viewModel.ExportDiagnosticsAsync();

        DiagnosticExportDocument document =
            await DiagnosticExportFile.ReadAsync(
                dialogService.TargetPath!);

        Assert.Equal(
            [
                "BeforePause",
                "DuringPause"
            ],
            document.Records
                .Select(
                    record =>
                        record.EventName)
                .ToArray());
    }

    [Fact]
    public async Task Export_RefusesToOverwriteAndLeavesTargetUntouched()
    {
        DesktopRuntimeDiagnosticSession session =
            CreateBytesSession();
        PublishOperational(
            session,
            "SomeEvent");

        string targetPath =
            NewTargetPath();
        await File.WriteAllTextAsync(
            targetPath,
            "occupied");

        StubExportDialogService dialogService =
            new(targetPath);
        RuntimeDiagnosticsViewModel viewModel =
            CreateViewModel(
                session,
                dialogService);

        await viewModel.ExportDiagnosticsAsync();

        Assert.StartsWith(
            "Export failed:",
            viewModel.ExportStatusText,
            StringComparison.Ordinal);
        Assert.Equal(
            "occupied",
            await File.ReadAllTextAsync(
                targetPath));
    }

    [Fact]
    public async Task Export_CancelledDialogWritesNothing()
    {
        DesktopRuntimeDiagnosticSession session =
            CreateBytesSession();
        PublishOperational(
            session,
            "SomeEvent");

        StubExportDialogService dialogService =
            new(targetPath: null);
        RuntimeDiagnosticsViewModel viewModel =
            CreateViewModel(
                session,
                dialogService);

        await viewModel.ExportDiagnosticsAsync();

        Assert.Equal(
            "Export cancelled.",
            viewModel.ExportStatusText);
        Assert.Empty(
            Directory.GetFiles(
                directory));
    }

    [Fact]
    public async Task Export_EmptySessionWritesValidEmptyDocument()
    {
        DesktopRuntimeDiagnosticSession session =
            CreateBytesSession();

        StubExportDialogService dialogService =
            new(NewTargetPath());
        RuntimeDiagnosticsViewModel viewModel =
            CreateViewModel(
                session,
                dialogService);

        await viewModel.ExportDiagnosticsAsync();

        DiagnosticExportDocument document =
            await DiagnosticExportFile.ReadAsync(
                dialogService.TargetPath!);

        Assert.Empty(
            document.Records);
        Assert.Equal(
            0,
            document.Envelope.RecordCount);
        Assert.Equal(
            "Exported 0 records to "
            + Path.GetFileName(
                dialogService.TargetPath!)
            + ".",
            viewModel.ExportStatusText);
    }

    [Fact]
    public async Task Export_StatusNamesFileWithoutDirectoryPath()
    {
        DesktopRuntimeDiagnosticSession session =
            CreateBytesSession();
        PublishOperational(
            session,
            "FirstEvent");
        PublishOperational(
            session,
            "SecondEvent");

        StubExportDialogService dialogService =
            new(NewTargetPath());
        RuntimeDiagnosticsViewModel viewModel =
            CreateViewModel(
                session,
                dialogService);

        await viewModel.ExportDiagnosticsAsync();

        Assert.Equal(
            "Exported 2 records to "
            + Path.GetFileName(
                dialogService.TargetPath!)
            + ".",
            viewModel.ExportStatusText);
        Assert.DoesNotContain(
            directory,
            viewModel.ExportStatusText,
            StringComparison.OrdinalIgnoreCase);
        Assert.True(
            viewModel.HasExportStatus);
    }

    [Fact]
    public async Task Export_WithoutDialogServiceDoesNothing()
    {
        DesktopRuntimeDiagnosticSession session =
            CreateBytesSession();
        PublishOperational(
            session,
            "SomeEvent");

        RuntimeDiagnosticsViewModel viewModel =
            new(session);

        await viewModel.ExportDiagnosticsAsync();

        Assert.Equal(
            string.Empty,
            viewModel.ExportStatusText);
        Assert.False(
            viewModel.HasExportStatus);
    }

    private string NewTargetPath()
    {
        return Path.Combine(
            directory,
            Guid.NewGuid().ToString("N") + ".jsonl");
    }

    private static RuntimeDiagnosticsViewModel CreateViewModel(
        DesktopRuntimeDiagnosticSession session,
        IDesktopDiagnosticExportDialogService dialogService)
    {
        return new RuntimeDiagnosticsViewModel(
            session,
            byteInterpretationService: null,
            dialogService,
            new DesktopRuntimeHostShellInformation(
                Composition: "Test composition",
                HostIdentity: "host-under-test",
                ApiVersion: "1",
                LoopbackBinding: "none",
                PrivateNetworkBinding: "none"),
            new FixedExportClock(ExportClock));
    }

    private sealed class FixedExportClock(
        DateTimeOffset value)
        : IDiagnosticExportClock
    {
        public DateTimeOffset UtcNow()
        {
            return value;
        }
    }

    private static DesktopRuntimeDiagnosticSession CreateBytesSession()
    {
        return new DesktopRuntimeDiagnosticSession(
            RuntimeDiagnosticLevel.Bytes);
    }

    private static void PublishOperational(
        DesktopRuntimeDiagnosticSession session,
        string eventName)
    {
        session.Publisher.Publish(
            new RuntimeDiagnosticEvent(
                RuntimeDiagnosticLevel.Operational,
                RuntimeDiagnosticCategory.RuntimeConnection,
                eventName));
    }

    private static void PublishBytes(
        DesktopRuntimeDiagnosticSession session,
        string eventName)
    {
        byte[] payload =
            [1, 2, 3, 4];

        session.Publisher.Publish(
            new RuntimeDiagnosticEvent(
                RuntimeDiagnosticLevel.Bytes,
                RuntimeDiagnosticCategory.TransportBytes,
                eventName,
                RuntimeDiagnosticSeverity.Trace,
                direction: RuntimeDiagnosticDirection.Inbound,
                byteSnapshot: new RuntimeDiagnosticByteSnapshot(
                    payload.Length,
                    payload,
                    isTruncated: false)));
    }

    private sealed class StubExportDialogService(
        string? targetPath)
        : IDesktopDiagnosticExportDialogService
    {
        public string? TargetPath
        {
            get;
        } =
            targetPath;

        public string? SuggestedFileName
        {
            get;
            private set;
        }

        public string? SelectExportTarget(
            string suggestedFileName)
        {
            SuggestedFileName =
                suggestedFileName;

            return TargetPath;
        }
    }
}
