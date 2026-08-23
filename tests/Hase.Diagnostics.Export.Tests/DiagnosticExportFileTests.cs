using System.Text;
using Hase.Client;
using Hase.Client.Configuration;
using Hase.Client.Diagnostics;
using Hase.Diagnostics.Export;
using Hase.Runtime.Diagnostics;

namespace Hase.Diagnostics.Export.Tests;

public sealed class DiagnosticExportFileTests : IDisposable
{
    private readonly string directory;

    public DiagnosticExportFileTests()
    {
        directory = Path.Combine(
            Path.GetTempPath(),
            "hase-diagnostic-export-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private string NewTargetPath() =>
        Path.Combine(directory, Guid.NewGuid().ToString("N") + ".jsonl");

    private static IReadOnlyList<RuntimeDiagnosticRecord> MintHostRecords()
    {
        BoundedRuntimeDiagnosticCollector collector =
            new(capacity: 16, RuntimeDiagnosticLevel.Bytes);
        RuntimeDiagnosticPublisher publisher = new(collector);

        publisher.Publish(new RuntimeDiagnosticEvent(
            RuntimeDiagnosticLevel.Operational,
            RuntimeDiagnosticCategory.RuntimeConnection,
            "EndpointVerified",
            RuntimeDiagnosticSeverity.Information,
            endpointId: "arduino-uno-01",
            attachmentGeneration: Guid.NewGuid(),
            details: new Dictionary<string, string>
            {
                ["transport"] = "serial",
                ["endpointKind"] = "compact"
            }));

        byte[] originalBytes = new byte[300];
        for (int index = 0; index < originalBytes.Length; index++)
        {
            originalBytes[index] = (byte)(index % 251);
        }

        publisher.Publish(new RuntimeDiagnosticEvent(
            RuntimeDiagnosticLevel.Bytes,
            RuntimeDiagnosticCategory.TransportBytes,
            "FrameReceived",
            RuntimeDiagnosticSeverity.Trace,
            endpointId: "arduino-uno-01",
            direction: RuntimeDiagnosticDirection.Inbound,
            operationId: Guid.NewGuid(),
            duration: TimeSpan.FromMilliseconds(12.5),
            outcome: RuntimeDiagnosticOutcome.Succeeded,
            byteSnapshot: new RuntimeDiagnosticByteSnapshot(
                originalBytes.Length,
                originalBytes.AsSpan(
                    0,
                    RuntimeDiagnosticByteSnapshot.MaximumCapturedByteCount),
                isTruncated: true)));

        publisher.Publish(new RuntimeDiagnosticEvent(
            RuntimeDiagnosticLevel.Operational,
            RuntimeDiagnosticCategory.RuntimeCommand,
            "CommandExecutionFailed",
            RuntimeDiagnosticSeverity.Error,
            endpointId: "arduino-uno-01",
            operationId: Guid.NewGuid(),
            outcome: RuntimeDiagnosticOutcome.Failed,
            details: new Dictionary<string, string>
            {
                ["commandName"] = "reset"
            }));

        return collector.GetSnapshot();
    }

    private static IReadOnlyList<ClientDiagnosticRecord> MintClientRecords()
    {
        BoundedClientDiagnosticCollector collector =
            new(capacity: 16, ClientDiagnosticLevel.Bytes);
        ClientDiagnosticPublisher publisher = new(collector);

        ClientDiagnosticSessionContext sessionContext = new(
            new RuntimeHostProfileId("example-host"),
            "Example Host",
            new RemoteRuntimeHostId("host-1"),
            new RemoteRuntimeHostId("host-1"));

        publisher.Publish(new ClientDiagnosticEvent(
            ClientDiagnosticLevel.Operational,
            ClientDiagnosticCategory.ClientConnection,
            "SessionEstablished",
            ClientDiagnosticSeverity.Information,
            metadata: new Dictionary<string, string>
            {
                ["profileKind"] = "secured"
            },
            sessionContext: sessionContext));

        byte[] originalBytes = new byte[64];
        for (int index = 0; index < originalBytes.Length; index++)
        {
            originalBytes[index] = (byte)index;
        }

        publisher.Publish(new ClientDiagnosticEvent(
            ClientDiagnosticLevel.Bytes,
            ClientDiagnosticCategory.NorthboundBytes,
            "RemoteBytesObserved",
            ClientDiagnosticSeverity.Trace,
            direction: ClientDiagnosticDirection.Inbound,
            operationId: Guid.NewGuid(),
            endpointId: "esp32-lan-01",
            attachmentGeneration: Guid.NewGuid(),
            instrumentId: "kel103-identity",
            descriptorPath: "properties/output-voltage",
            duration: TimeSpan.FromMilliseconds(3),
            outcome: ClientDiagnosticOutcome.Succeeded,
            sessionContext: sessionContext,
            byteSnapshot: new RemoteRuntimeDiagnosticByteSnapshot(
                originalBytes.Length,
                originalBytes,
                isTruncated: false)));

        return collector.GetSnapshot().Records;
    }

    private static void AssertRecordsEqual(
        DiagnosticExportDocument expected,
        DiagnosticExportDocument actual)
    {
        Assert.Equal(expected.Envelope.Application, actual.Envelope.Application);
        Assert.Equal(
            expected.Envelope.CaptureLevel, actual.Envelope.CaptureLevel);
        Assert.Equal(
            expected.Envelope.RuntimeHostId, actual.Envelope.RuntimeHostId);
        Assert.Equal(
            expected.Envelope.ExportedAtUtc, actual.Envelope.ExportedAtUtc);
        Assert.Equal(expected.Envelope.RecordCount, actual.Envelope.RecordCount);
        Assert.Equal(expected.Records.Count, actual.Records.Count);

        for (int index = 0; index < expected.Records.Count; index++)
        {
            ExportedDiagnosticRecord left = expected.Records[index];
            ExportedDiagnosticRecord right = actual.Records[index];

            Assert.Equal(left.Sequence, right.Sequence);
            Assert.Equal(left.TimestampUtc, right.TimestampUtc);
            Assert.Equal(left.Level, right.Level);
            Assert.Equal(left.Category, right.Category);
            Assert.Equal(left.EventName, right.EventName);
            Assert.Equal(left.Severity, right.Severity);
            Assert.Equal(left.Direction, right.Direction);
            Assert.Equal(left.OperationId, right.OperationId);
            Assert.Equal(left.EndpointId, right.EndpointId);
            Assert.Equal(left.AttachmentGeneration, right.AttachmentGeneration);
            Assert.Equal(left.InstrumentId, right.InstrumentId);
            Assert.Equal(left.DescriptorPath, right.DescriptorPath);
            Assert.Equal(left.Duration, right.Duration);
            Assert.Equal(left.Outcome, right.Outcome);
            Assert.Equal(
                left.Details.OrderBy(item => item.Key, StringComparer.Ordinal),
                right.Details.OrderBy(item => item.Key, StringComparer.Ordinal));
            Assert.Equal(left.SessionContext, right.SessionContext);

            if (left.ByteSnapshot is null)
            {
                Assert.Null(right.ByteSnapshot);
            }
            else
            {
                Assert.NotNull(right.ByteSnapshot);
                Assert.Equal(
                    left.ByteSnapshot.OriginalByteCount,
                    right.ByteSnapshot.OriginalByteCount);
                Assert.Equal(
                    left.ByteSnapshot.CapturedBytes,
                    right.ByteSnapshot.CapturedBytes);
                Assert.Equal(
                    left.ByteSnapshot.IsTruncated,
                    right.ByteSnapshot.IsTruncated);
            }
        }
    }

    [Fact]
    public async Task RuntimeHostExportRoundTripsAllFields()
    {
        DiagnosticExportDocument document =
            RuntimeHostDiagnosticExport.ToDocument(
                RuntimeDiagnosticLevel.Bytes,
                "host-1",
                new DateTimeOffset(2026, 8, 23, 12, 0, 0, TimeSpan.Zero),
                MintHostRecords());
        string path = NewTargetPath();

        await DiagnosticExportFile.WriteNewAsync(path, document);
        DiagnosticExportDocument roundTripped =
            await DiagnosticExportFile.ReadAsync(path);

        Assert.Equal(
            DiagnosticExportApplications.RuntimeHost,
            roundTripped.Envelope.Application);
        Assert.Equal("host-1", roundTripped.Envelope.RuntimeHostId);
        AssertRecordsEqual(document, roundTripped);
    }

    [Fact]
    public async Task ClientExportRoundTripsAllFields()
    {
        DiagnosticExportDocument document =
            ClientDiagnosticExport.ToDocument(
                ClientDiagnosticLevel.Bytes,
                new DateTimeOffset(2026, 8, 23, 12, 0, 0, TimeSpan.Zero),
                MintClientRecords());
        string path = NewTargetPath();

        await DiagnosticExportFile.WriteNewAsync(path, document);
        DiagnosticExportDocument roundTripped =
            await DiagnosticExportFile.ReadAsync(path);

        Assert.Equal(
            DiagnosticExportApplications.Client,
            roundTripped.Envelope.Application);
        Assert.Null(roundTripped.Envelope.RuntimeHostId);
        Assert.NotNull(roundTripped.Records[1].SessionContext);
        Assert.Equal(
            "kel103-identity", roundTripped.Records[1].InstrumentId);
        AssertRecordsEqual(document, roundTripped);
    }

    [Fact]
    public async Task EmptyExportRoundTrips()
    {
        DiagnosticExportDocument document =
            ClientDiagnosticExport.ToDocument(
                ClientDiagnosticLevel.Operational,
                new DateTimeOffset(2026, 8, 23, 12, 0, 0, TimeSpan.Zero),
                Array.Empty<ClientDiagnosticRecord>());
        string path = NewTargetPath();

        await DiagnosticExportFile.WriteNewAsync(path, document);
        DiagnosticExportDocument roundTripped =
            await DiagnosticExportFile.ReadAsync(path);

        Assert.Empty(roundTripped.Records);
        Assert.Equal(0, roundTripped.Envelope.RecordCount);
    }

    [Fact]
    public async Task WriterRefusesExistingTarget()
    {
        DiagnosticExportDocument document = EmptyHostDocument();
        string path = NewTargetPath();
        await File.WriteAllTextAsync(path, "occupied");

        await Assert.ThrowsAsync<IOException>(() =>
            DiagnosticExportFile.WriteNewAsync(path, document));
        Assert.Equal("occupied", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task WriterRefusesMissingDirectory()
    {
        string path = Path.Combine(
            directory, "missing-subdirectory", "export.jsonl");

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            DiagnosticExportFile.WriteNewAsync(path, EmptyHostDocument()));
    }

    [Fact]
    public async Task WriterRefusesRelativePath()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            DiagnosticExportFile.WriteNewAsync(
                "relative-export.jsonl", EmptyHostDocument()));
    }

    [Fact]
    public async Task WriterLeavesNoTemporaryFile()
    {
        string path = NewTargetPath();

        await DiagnosticExportFile.WriteNewAsync(path, EmptyHostDocument());

        string[] entries = Directory.GetFiles(directory);
        Assert.Equal(new[] { path }, entries);
    }

    [Fact]
    public async Task WriterEmitsUtf8WithoutByteOrderMark()
    {
        string path = NewTargetPath();

        await DiagnosticExportFile.WriteNewAsync(path, EmptyHostDocument());

        byte[] written = await File.ReadAllBytesAsync(path);
        Assert.True(written.Length > 3);
        Assert.False(
            written[0] == 0xEF && written[1] == 0xBB && written[2] == 0xBF);
        Assert.Equal((byte)'\n', written[^1]);
    }

    [Fact]
    public async Task ReaderToleratesCarriageReturnLineEndings()
    {
        DiagnosticExportDocument document =
            RuntimeHostDiagnosticExport.ToDocument(
                RuntimeDiagnosticLevel.Bytes,
                "host-1",
                new DateTimeOffset(2026, 8, 23, 12, 0, 0, TimeSpan.Zero),
                MintHostRecords());
        string path = NewTargetPath();
        await DiagnosticExportFile.WriteNewAsync(path, document);

        string content = await File.ReadAllTextAsync(path);
        string crlfPath = NewTargetPath();
        await File.WriteAllTextAsync(
            crlfPath,
            content.Replace("\n", "\r\n"),
            new UTF8Encoding(false));

        DiagnosticExportDocument roundTripped =
            await DiagnosticExportFile.ReadAsync(crlfPath);
        AssertRecordsEqual(document, roundTripped);
    }

    [Fact]
    public async Task ReaderRejectsWrongDocumentKind()
    {
        await AssertTamperRejectedAsync(content => content.Replace(
            "\"hase-diagnostic-export\"", "\"some-other-document\""));
    }

    [Fact]
    public async Task ReaderRejectsUnsupportedFormatVersion()
    {
        await AssertTamperRejectedAsync(content => content.Replace(
            "\"formatVersion\":1", "\"formatVersion\":2"));
    }

    [Fact]
    public async Task ReaderRejectsUnknownEnvelopeField()
    {
        await AssertTamperRejectedAsync(content => content.Replace(
            "\"formatVersion\":1",
            "\"formatVersion\":1,\"unexpected\":true"));
    }

    [Fact]
    public async Task ReaderRejectsUnknownRecordField()
    {
        await AssertTamperRejectedAsync(content => content.Replace(
            "\"eventName\":\"EndpointVerified\"",
            "\"eventName\":\"EndpointVerified\",\"unexpected\":true"));
    }

    [Fact]
    public async Task ReaderRejectsUnknownApplication()
    {
        await AssertTamperRejectedAsync(content => content.Replace(
            "\"application\":\"runtime-host\"",
            "\"application\":\"unknown-application\""));
    }

    [Fact]
    public async Task ReaderRejectsRecordCountBelowRecordLines()
    {
        await AssertTamperRejectedAsync(content => content.Replace(
            "\"recordCount\":3", "\"recordCount\":2"));
    }

    [Fact]
    public async Task ReaderRejectsMissingRecordLines()
    {
        await AssertTamperRejectedAsync(content =>
        {
            string[] lines = content.Split(
                '\n', StringSplitOptions.RemoveEmptyEntries);
            return string.Join('\n', lines[..^1]) + "\n";
        });
    }

    [Fact]
    public async Task ReaderRejectsMalformedRecordLine()
    {
        await AssertTamperRejectedAsync(content =>
        {
            string[] lines = content.Split(
                '\n', StringSplitOptions.RemoveEmptyEntries);
            lines[1] = "this is not json";
            return string.Join('\n', lines) + "\n";
        });
    }

    [Fact]
    public async Task ReaderRejectsInvalidSnapshotHex()
    {
        await AssertTamperRejectedAsync(content => content.Replace(
            "\"capturedHex\":\"00", "\"capturedHex\":\"ZZ"));
    }

    [Fact]
    public async Task ReaderRejectsInconsistentSnapshotTruncation()
    {
        await AssertTamperRejectedAsync(content => content.Replace(
            "\"isTruncated\":true", "\"isTruncated\":false"));
    }

    [Fact]
    public async Task ReaderRejectsEmptyFile()
    {
        string path = NewTargetPath();
        await File.WriteAllTextAsync(path, string.Empty);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            DiagnosticExportFile.ReadAsync(path));
    }

    [Fact]
    public async Task ReaderRejectsInvalidUtf8()
    {
        string path = NewTargetPath();
        await File.WriteAllBytesAsync(
            path, new byte[] { 0x7B, 0xFF, 0xFE, 0x7D, 0x0A });

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            DiagnosticExportFile.ReadAsync(path));
    }

    [Fact]
    public async Task ReaderRejectsOversizedDocument()
    {
        string path = NewTargetPath();
        await using (FileStream stream = File.Create(path))
        {
            stream.SetLength(
                DiagnosticExportFile.MaximumDocumentByteCount + 1);
        }

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            DiagnosticExportFile.ReadAsync(path));
    }

    private async Task AssertTamperRejectedAsync(
        Func<string, string> tamper)
    {
        DiagnosticExportDocument document =
            RuntimeHostDiagnosticExport.ToDocument(
                RuntimeDiagnosticLevel.Bytes,
                "host-1",
                new DateTimeOffset(2026, 8, 23, 12, 0, 0, TimeSpan.Zero),
                MintHostRecords());
        string path = NewTargetPath();
        await DiagnosticExportFile.WriteNewAsync(path, document);

        string content = await File.ReadAllTextAsync(path);
        string tampered = tamper(content);
        Assert.NotEqual(content, tampered);

        string tamperedPath = NewTargetPath();
        await File.WriteAllTextAsync(
            tamperedPath, tampered, new UTF8Encoding(false));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            DiagnosticExportFile.ReadAsync(tamperedPath));
    }

    private static DiagnosticExportDocument EmptyHostDocument() =>
        RuntimeHostDiagnosticExport.ToDocument(
            RuntimeDiagnosticLevel.Operational,
            "host-1",
            new DateTimeOffset(2026, 8, 23, 12, 0, 0, TimeSpan.Zero),
            Array.Empty<RuntimeDiagnosticRecord>());
}
