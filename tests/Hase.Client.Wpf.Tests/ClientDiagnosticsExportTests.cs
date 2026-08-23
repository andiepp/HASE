using System.IO;
using Hase.Client;
using Hase.Client.Configuration;
using Hase.Client.Diagnostics;
using Hase.Client.Wpf.Services;
using Hase.Client.Wpf.ViewModels;
using Hase.Diagnostics.Export;

namespace Hase.Client.Wpf.Tests;

public sealed class ClientDiagnosticsExportTests : IDisposable
{
    private static readonly DateTimeOffset ExportClock =
        new(2026, 8, 23, 16, 0, 0, TimeSpan.Zero);

    private readonly string directory;

    public ClientDiagnosticsExportTests()
    {
        directory = Path.Combine(
            Path.GetTempPath(),
            "hase-client-diagnostics-export-tests-"
            + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Export_WritesCompleteRetainedSessionIndependentOfDisplayFilters()
    {
        BoundedClientDiagnosticCollector collector =
            new(10, ClientDiagnosticLevel.Bytes);
        ClientDiagnosticPublisher publisher = new(collector);
        publisher.Publish(CreateOperationalEvent(
            "HostOneEvent", CreateSessionContext("host-one")));
        publisher.Publish(CreateOperationalEvent(
            "HostTwoEvent", CreateSessionContext("host-two")));
        publisher.Publish(CreateBytesEvent("BytesEvent"));

        StubExportFilePicker picker = new(NewTargetPath());
        ClientDiagnosticsViewModel viewModel = CreateViewModel(collector, picker);
        viewModel.SelectedLevelFilter = nameof(ClientDiagnosticLevel.Operational);
        viewModel.SelectedCategoryFilter =
            nameof(ClientDiagnosticCategory.ClientConnection);
        viewModel.ConfigureRuntimeHosts(CreateRegistry("host-one"));
        viewModel.SelectedRuntimeHostFilter = viewModel.RuntimeHostFilters[1];

        Assert.Equal(1, viewModel.RecordCount);

        await viewModel.ExportDiagnosticsAsync();

        DiagnosticExportDocument document =
            await DiagnosticExportFile.ReadAsync(picker.TargetPath!);

        Assert.Equal(
            DiagnosticExportApplications.Client,
            document.Envelope.Application);
        Assert.Equal("Bytes", document.Envelope.CaptureLevel);
        Assert.Null(document.Envelope.RuntimeHostId);
        Assert.Equal(ExportClock, document.Envelope.ExportedAtUtc);
        Assert.Equal(3, document.Envelope.RecordCount);
        Assert.Equal(
            ["HostOneEvent", "HostTwoEvent", "BytesEvent"],
            document.Records.Select(record => record.EventName).ToArray());
        Assert.Equal(
            "host-one",
            document.Records[0].SessionContext!.ProfileId);
        Assert.Equal(
            "host-two",
            document.Records[1].SessionContext!.ProfileId);
        Assert.Equal(
            "client-diagnostics-20260823-160000Z.jsonl",
            picker.SuggestedFileName);
    }

    [Fact]
    public async Task Export_IncludesRecordsRetainedWhilePresentationPaused()
    {
        BoundedClientDiagnosticCollector collector = new(10);
        ClientDiagnosticPublisher publisher = new(collector);
        publisher.Publish(CreateOperationalEvent("BeforePause"));

        StubExportFilePicker picker = new(NewTargetPath());
        ClientDiagnosticsViewModel viewModel = CreateViewModel(collector, picker);
        viewModel.PauseCommand.Execute();
        publisher.Publish(CreateOperationalEvent("DuringPause"));
        viewModel.Refresh();

        Assert.Equal(1, viewModel.RecordCount);
        Assert.Equal(1, viewModel.PendingRecordCount);

        await viewModel.ExportDiagnosticsAsync();

        DiagnosticExportDocument document =
            await DiagnosticExportFile.ReadAsync(picker.TargetPath!);

        Assert.Equal(
            ["BeforePause", "DuringPause"],
            document.Records.Select(record => record.EventName).ToArray());
    }

    [Fact]
    public async Task Export_RefusesToOverwriteAndLeavesTargetUntouched()
    {
        BoundedClientDiagnosticCollector collector = new(10);
        ClientDiagnosticPublisher publisher = new(collector);
        publisher.Publish(CreateOperationalEvent("SomeEvent"));

        string targetPath = NewTargetPath();
        await File.WriteAllTextAsync(targetPath, "occupied");

        StubExportFilePicker picker = new(targetPath);
        ClientDiagnosticsViewModel viewModel = CreateViewModel(collector, picker);

        await viewModel.ExportDiagnosticsAsync();

        Assert.StartsWith(
            "Export failed:",
            viewModel.ExportStatusText,
            StringComparison.Ordinal);
        Assert.Equal("occupied", await File.ReadAllTextAsync(targetPath));
    }

    [Fact]
    public async Task Export_CancelledPickerWritesNothing()
    {
        BoundedClientDiagnosticCollector collector = new(10);
        ClientDiagnosticPublisher publisher = new(collector);
        publisher.Publish(CreateOperationalEvent("SomeEvent"));

        StubExportFilePicker picker = new(targetPath: null);
        ClientDiagnosticsViewModel viewModel = CreateViewModel(collector, picker);

        await viewModel.ExportDiagnosticsAsync();

        Assert.Equal("Export cancelled.", viewModel.ExportStatusText);
        Assert.Empty(Directory.GetFiles(directory));
    }

    [Fact]
    public async Task Export_EmptySessionWritesValidEmptyDocument()
    {
        BoundedClientDiagnosticCollector collector = new(10);

        StubExportFilePicker picker = new(NewTargetPath());
        ClientDiagnosticsViewModel viewModel = CreateViewModel(collector, picker);

        await viewModel.ExportDiagnosticsAsync();

        DiagnosticExportDocument document =
            await DiagnosticExportFile.ReadAsync(picker.TargetPath!);

        Assert.Empty(document.Records);
        Assert.Equal(0, document.Envelope.RecordCount);
        Assert.Equal(
            "Exported 0 records to "
            + Path.GetFileName(picker.TargetPath!) + ".",
            viewModel.ExportStatusText);
    }

    [Fact]
    public async Task Export_StatusNamesFileWithoutDirectoryPath()
    {
        BoundedClientDiagnosticCollector collector = new(10);
        ClientDiagnosticPublisher publisher = new(collector);
        publisher.Publish(CreateOperationalEvent("FirstEvent"));
        publisher.Publish(CreateOperationalEvent("SecondEvent"));

        StubExportFilePicker picker = new(NewTargetPath());
        ClientDiagnosticsViewModel viewModel = CreateViewModel(collector, picker);

        await viewModel.ExportDiagnosticsAsync();

        Assert.Equal(
            "Exported 2 records to "
            + Path.GetFileName(picker.TargetPath!) + ".",
            viewModel.ExportStatusText);
        Assert.DoesNotContain(
            directory,
            viewModel.ExportStatusText,
            StringComparison.OrdinalIgnoreCase);
        Assert.True(viewModel.HasExportStatus);
    }

    [Fact]
    public async Task Export_WithoutFilePickerDoesNothing()
    {
        BoundedClientDiagnosticCollector collector = new(10);
        ClientDiagnosticPublisher publisher = new(collector);
        publisher.Publish(CreateOperationalEvent("SomeEvent"));

        ClientDiagnosticsViewModel viewModel = new(collector);

        await viewModel.ExportDiagnosticsAsync();

        Assert.Equal(string.Empty, viewModel.ExportStatusText);
        Assert.False(viewModel.HasExportStatus);
    }

    private string NewTargetPath() =>
        Path.Combine(directory, Guid.NewGuid().ToString("N") + ".jsonl");

    private static ClientDiagnosticsViewModel CreateViewModel(
        BoundedClientDiagnosticCollector collector,
        IClientDiagnosticExportFilePicker picker)
    {
        return new ClientDiagnosticsViewModel(
            collector,
            picker,
            () => ExportClock);
    }

    private static ClientDiagnosticEvent CreateOperationalEvent(
        string eventName,
        ClientDiagnosticSessionContext? sessionContext = null)
    {
        return new ClientDiagnosticEvent(
            ClientDiagnosticLevel.Operational,
            ClientDiagnosticCategory.ClientConnection,
            eventName,
            sessionContext: sessionContext);
    }

    private static ClientDiagnosticEvent CreateBytesEvent(string eventName)
    {
        byte[] payload = [1, 2, 3, 4];
        return new ClientDiagnosticEvent(
            ClientDiagnosticLevel.Bytes,
            ClientDiagnosticCategory.NorthboundBytes,
            eventName,
            ClientDiagnosticSeverity.Trace,
            byteSnapshot: new RemoteRuntimeDiagnosticByteSnapshot(
                payload.Length,
                payload,
                isTruncated: false));
    }

    private static ClientDiagnosticSessionContext CreateSessionContext(
        string profileId)
    {
        return new ClientDiagnosticSessionContext(
            new RuntimeHostProfileId(profileId),
            "Profile " + profileId,
            new RemoteRuntimeHostId(profileId));
    }

    private static RuntimeHostProfileRegistry CreateRegistry(
        string profileId)
    {
        return new RuntimeHostProfileRegistry(
            [
                new RuntimeHostProfile(
                    new RuntimeHostProfileId(profileId),
                    "Profile " + profileId,
                    new RemoteRuntimeHostId(profileId))
            ]);
    }

    private sealed class StubExportFilePicker(string? targetPath)
        : IClientDiagnosticExportFilePicker
    {
        public string? TargetPath { get; } = targetPath;

        public string? SuggestedFileName { get; private set; }

        public string? SelectExportTarget(string suggestedFileName)
        {
            SuggestedFileName = suggestedFileName;
            return TargetPath;
        }
    }
}
