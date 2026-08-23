using System.Text;
using Hase.Client;
using Hase.Client.Configuration;
using Hase.Client.Diagnostics;
using Hase.Diagnostics.Export;
using Hase.Diagnostics.Offline;
using Hase.Runtime.Diagnostics;

namespace Hase.Diagnostics.Offline.Tests;

public sealed class DiagnosticOfflineToolTests : IDisposable
{
    private readonly string directory;

    public DiagnosticOfflineToolTests()
    {
        directory = Path.Combine(
            Path.GetTempPath(),
            "hase-diagnostics-offline-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private string NewPath() =>
        Path.Combine(directory, Guid.NewGuid().ToString("N") + ".jsonl");

    private static async Task<(int ExitCode, string Output, string Error)>
        RunAsync(params string[] args)
    {
        StringWriter output = new();
        StringWriter error = new();
        int exitCode =
            await DiagnosticOfflineTool.RunAsync(args, output, error);
        return (exitCode, output.ToString(), error.ToString());
    }

    private async Task<string> WriteHostExportAsync()
    {
        BoundedRuntimeDiagnosticCollector collector =
            new(capacity: 16, RuntimeDiagnosticLevel.Bytes);
        RuntimeDiagnosticPublisher publisher = new(collector);

        publisher.Publish(new RuntimeDiagnosticEvent(
            RuntimeDiagnosticLevel.Operational,
            RuntimeDiagnosticCategory.RuntimeConnection,
            "EndpointVerified",
            endpointId: "arduino-uno-01",
            details: new Dictionary<string, string>
            {
                ["transport"] = "serial"
            }));
        publisher.Publish(new RuntimeDiagnosticEvent(
            RuntimeDiagnosticLevel.Operational,
            RuntimeDiagnosticCategory.RuntimeCommand,
            "CommandExecuted",
            endpointId: "arduino-uno-01",
            outcome: RuntimeDiagnosticOutcome.Succeeded));

        byte[] payload = [0x0A, 0x0B, 0x0C];
        publisher.Publish(new RuntimeDiagnosticEvent(
            RuntimeDiagnosticLevel.Bytes,
            RuntimeDiagnosticCategory.TransportBytes,
            "FrameReceived",
            RuntimeDiagnosticSeverity.Trace,
            endpointId: "esp32-lan-01",
            direction: RuntimeDiagnosticDirection.Inbound,
            outcome: RuntimeDiagnosticOutcome.Succeeded,
            byteSnapshot: new RuntimeDiagnosticByteSnapshot(
                payload.Length,
                payload,
                isTruncated: false)));

        DiagnosticExportDocument document =
            RuntimeHostDiagnosticExport.ToDocument(
                RuntimeDiagnosticLevel.Bytes,
                "host-1",
                new DateTimeOffset(2026, 8, 23, 17, 0, 0, TimeSpan.Zero),
                collector.GetSnapshot());
        string path = NewPath();
        await DiagnosticExportFile.WriteNewAsync(path, document);
        return path;
    }

    private async Task<string> WriteClientExportAsync()
    {
        BoundedClientDiagnosticCollector collector =
            new(capacity: 16, ClientDiagnosticLevel.Operational);
        ClientDiagnosticPublisher publisher = new(collector);

        publisher.Publish(new ClientDiagnosticEvent(
            ClientDiagnosticLevel.Operational,
            ClientDiagnosticCategory.ClientConnection,
            "SessionEstablished",
            instrumentId: "kel103-identity",
            sessionContext: new ClientDiagnosticSessionContext(
                new RuntimeHostProfileId("example-host"),
                "Example Host",
                new RemoteRuntimeHostId("host-1"))));

        DiagnosticExportDocument document =
            ClientDiagnosticExport.ToDocument(
                ClientDiagnosticLevel.Operational,
                new DateTimeOffset(2026, 8, 23, 17, 30, 0, TimeSpan.Zero),
                collector.GetSnapshot().Records);
        string path = NewPath();
        await DiagnosticExportFile.WriteNewAsync(path, document);
        return path;
    }

    private async Task<string> WriteAuthoredAsync(string content)
    {
        string path = NewPath();
        await File.WriteAllTextAsync(path, content, new UTF8Encoding(false));
        return path;
    }

    private const string AuthoredValidDocument =
        "{\"documentKind\":\"hase-diagnostic-export\",\"formatVersion\":1,"
        + "\"application\":\"runtime-host\",\"captureLevel\":\"Operational\","
        + "\"runtimeHostId\":\"authored-host\","
        + "\"exportedAtUtc\":\"2026-08-23T18:00:00+00:00\",\"recordCount\":1}\n"
        + "{\"sequence\":1,\"timestampUtc\":\"2026-08-23T17:59:00+00:00\","
        + "\"level\":\"Operational\",\"category\":\"RuntimeConnection\","
        + "\"eventName\":\"AuthoredEvent\",\"severity\":\"Information\"}\n";

    [Fact]
    public async Task Validate_AcceptsHostAndClientExports()
    {
        string hostPath = await WriteHostExportAsync();
        string clientPath = await WriteClientExportAsync();

        (int hostExit, string hostOutput, string hostError) =
            await RunAsync("validate", hostPath);
        (int clientExit, string clientOutput, string clientError) =
            await RunAsync("validate", clientPath);

        Assert.Equal(DiagnosticOfflineTool.ExitSuccess, hostExit);
        Assert.Equal(string.Empty, hostError);
        Assert.Contains("valid HASE diagnostic export", hostOutput);
        Assert.Contains("Application: runtime-host", hostOutput);
        Assert.Contains("Runtime host: host-1", hostOutput);
        Assert.Contains("Records: 3", hostOutput);

        Assert.Equal(DiagnosticOfflineTool.ExitSuccess, clientExit);
        Assert.Equal(string.Empty, clientError);
        Assert.Contains("Application: client", clientOutput);
        Assert.DoesNotContain("Runtime host:", clientOutput);
        Assert.Contains("Records: 1", clientOutput);
    }

    [Fact]
    public async Task Validate_AcceptsAuthoredDocument()
    {
        string path = await WriteAuthoredAsync(AuthoredValidDocument);

        (int exitCode, string output, string error) =
            await RunAsync("validate", path);

        Assert.Equal(DiagnosticOfflineTool.ExitSuccess, exitCode);
        Assert.Equal(string.Empty, error);
        Assert.Contains("Runtime host: authored-host", output);
        Assert.Contains("Records: 1", output);
    }

    [Fact]
    public async Task Validate_RejectsTamperedDocumentWithExitOne()
    {
        string path = await WriteAuthoredAsync(
            AuthoredValidDocument.Replace(
                "\"recordCount\":1", "\"recordCount\":2"));

        (int exitCode, string output, string error) =
            await RunAsync("validate", path);

        Assert.Equal(DiagnosticOfflineTool.ExitFailure, exitCode);
        Assert.Equal(string.Empty, output);
        Assert.Contains("not a valid HASE diagnostic export", error);
    }

    [Fact]
    public async Task Validate_RejectsMissingFileWithExitOne()
    {
        (int exitCode, _, string error) =
            await RunAsync("validate", NewPath());

        Assert.Equal(DiagnosticOfflineTool.ExitFailure, exitCode);
        Assert.Contains("not a valid HASE diagnostic export", error);
    }

    [Fact]
    public async Task Summarize_ReportsAggregates()
    {
        string path = await WriteHostExportAsync();

        (int exitCode, string output, string error) =
            await RunAsync("summarize", path);

        Assert.Equal(DiagnosticOfflineTool.ExitSuccess, exitCode);
        Assert.Equal(string.Empty, error);
        Assert.Contains("Sequence range: 1 to 3", output);
        Assert.Contains("  Operational: 2", output);
        Assert.Contains("  Bytes: 1", output);
        Assert.Contains("  RuntimeConnection: 1", output);
        Assert.Contains("  RuntimeCommand: 1", output);
        Assert.Contains("  TransportBytes: 1", output);
        Assert.Contains("  Succeeded: 2", output);
        Assert.Contains("  (none): 1", output);
        Assert.Contains(
            "Distinct endpoints: arduino-uno-01, esp32-lan-01", output);
    }

    [Fact]
    public async Task Summarize_ReportsEmptyDocument()
    {
        DiagnosticExportDocument document =
            RuntimeHostDiagnosticExport.ToDocument(
                RuntimeDiagnosticLevel.Operational,
                "host-1",
                new DateTimeOffset(2026, 8, 23, 17, 0, 0, TimeSpan.Zero),
                Array.Empty<RuntimeDiagnosticRecord>());
        string path = NewPath();
        await DiagnosticExportFile.WriteNewAsync(path, document);

        (int exitCode, string output, _) = await RunAsync("summarize", path);

        Assert.Equal(DiagnosticOfflineTool.ExitSuccess, exitCode);
        Assert.Contains("The document contains no records.", output);
    }

    [Fact]
    public async Task Filter_WritesStrictReadableDocumentWithMatchingRecords()
    {
        string path = await WriteHostExportAsync();
        string outputPath = NewPath();

        (int exitCode, string output, string error) = await RunAsync(
            "filter", path,
            "--output", outputPath,
            "--endpoint", "arduino-uno-01",
            "--level", "Operational");

        Assert.Equal(DiagnosticOfflineTool.ExitSuccess, exitCode);
        Assert.Equal(string.Empty, error);
        Assert.Contains("Filtered 2 of 3 records", output);

        DiagnosticExportDocument filtered =
            await DiagnosticExportFile.ReadAsync(outputPath);
        Assert.Equal(2, filtered.Envelope.RecordCount);
        Assert.Equal("host-1", filtered.Envelope.RuntimeHostId);
        Assert.Equal(
            ["EndpointVerified", "CommandExecuted"],
            filtered.Records.Select(record => record.EventName).ToArray());
    }

    [Fact]
    public async Task Filter_EmptyMatchWritesValidEmptyDocument()
    {
        string path = await WriteHostExportAsync();
        string outputPath = NewPath();

        (int exitCode, string output, _) = await RunAsync(
            "filter", path,
            "--output", outputPath,
            "--event", "NoSuchEvent");

        Assert.Equal(DiagnosticOfflineTool.ExitSuccess, exitCode);
        Assert.Contains("Filtered 0 of 3 records", output);

        DiagnosticExportDocument filtered =
            await DiagnosticExportFile.ReadAsync(outputPath);
        Assert.Empty(filtered.Records);
    }

    [Fact]
    public async Task Filter_RefusesExistingOutput()
    {
        string path = await WriteHostExportAsync();
        string outputPath = NewPath();
        await File.WriteAllTextAsync(outputPath, "occupied");

        (int exitCode, _, string error) = await RunAsync(
            "filter", path, "--output", outputPath);

        Assert.Equal(DiagnosticOfflineTool.ExitFailure, exitCode);
        Assert.StartsWith(
            "The filter output could not be written:",
            error,
            StringComparison.Ordinal);
        Assert.Contains("already exists", error);
        Assert.DoesNotContain(
            "not a valid HASE diagnostic export", error);
        Assert.Equal("occupied", await File.ReadAllTextAsync(outputPath));
    }

    [Fact]
    public async Task Filter_WithoutOutputIsUsageError()
    {
        string path = await WriteHostExportAsync();

        (int exitCode, _, string error) = await RunAsync(
            "filter", path, "--event", "EndpointVerified");

        Assert.Equal(DiagnosticOfflineTool.ExitUsage, exitCode);
        Assert.Contains("Usage:", error);
    }

    [Fact]
    public async Task Show_RendersAllRecords()
    {
        string path = await WriteHostExportAsync();

        (int exitCode, string output, string error) =
            await RunAsync("show", path);

        Assert.Equal(DiagnosticOfflineTool.ExitSuccess, exitCode);
        Assert.Equal(string.Empty, error);
        Assert.Contains("--- Record 1 ---", output);
        Assert.Contains("--- Record 2 ---", output);
        Assert.Contains("--- Record 3 ---", output);
        Assert.Contains("Detail transport: serial", output);
        Assert.Contains("Byte snapshot: 3/3 bytes", output);
        Assert.Contains("Bytes (hex): 0A0B0C", output);
    }

    [Fact]
    public async Task Show_RendersSelectedSequenceWithSessionContext()
    {
        string path = await WriteClientExportAsync();

        (int exitCode, string output, _) = await RunAsync(
            "show", path, "--sequence", "1");

        Assert.Equal(DiagnosticOfflineTool.ExitSuccess, exitCode);
        Assert.Contains("--- Record 1 ---", output);
        Assert.Contains("Event: SessionEstablished", output);
        Assert.Contains("Instrument: kel103-identity", output);
        Assert.Contains("Profile: example-host (Example Host)", output);
        Assert.Contains("Expected host: host-1", output);
    }

    [Fact]
    public async Task Show_MissingSequenceIsFailure()
    {
        string path = await WriteHostExportAsync();

        (int exitCode, _, string error) = await RunAsync(
            "show", path, "--sequence", "99");

        Assert.Equal(DiagnosticOfflineTool.ExitFailure, exitCode);
        Assert.Contains("No record has sequence 99.", error);
    }

    [Fact]
    public async Task UnknownCommandIsUsageError()
    {
        string path = await WriteHostExportAsync();

        (int exitCode, _, string error) = await RunAsync("inspect", path);

        Assert.Equal(DiagnosticOfflineTool.ExitUsage, exitCode);
        Assert.Contains("Usage:", error);
    }

    [Fact]
    public async Task MissingArgumentsIsUsageError()
    {
        (int exitCode, _, string error) = await RunAsync("validate");

        Assert.Equal(DiagnosticOfflineTool.ExitUsage, exitCode);
        Assert.Contains("Usage:", error);
    }

    [Fact]
    public async Task UnknownFilterOptionIsUsageError()
    {
        string path = await WriteHostExportAsync();

        (int exitCode, _, string error) = await RunAsync(
            "filter", path, "--output", NewPath(), "--severity", "Error");

        Assert.Equal(DiagnosticOfflineTool.ExitUsage, exitCode);
        Assert.Contains("Usage:", error);
    }
}
