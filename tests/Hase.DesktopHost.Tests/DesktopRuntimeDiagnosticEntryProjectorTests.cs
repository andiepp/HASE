using System.Globalization;
using Hase.Runtime.Diagnostics;

namespace Hase.DesktopHost.Tests;

public sealed class DesktopRuntimeDiagnosticEntryProjectorTests
{
    private static readonly Guid Generation =
        Guid.Parse(
            "bd997c50-029f-4711-b04e-cd4fb75217f4");

    private static readonly Guid OperationId =
        Guid.Parse(
            "dcce59d6-31e5-474a-ae52-12c21942abc4");

    [Fact]
    public void Project_CompleteOperationalRecord_PreservesSafeFields()
    {
        RuntimeDiagnosticRecord record =
            Publish(
                new RuntimeDiagnosticEvent(
                    RuntimeDiagnosticLevel.Operational,
                    RuntimeDiagnosticCategory.RuntimeCommand,
                    "CommandExecutionCompleted",
                    RuntimeDiagnosticSeverity.Information,
                    "endpoint-one",
                    Generation,
                    RuntimeDiagnosticDirection.Inbound,
                    OperationId,
                    TimeSpan.FromMilliseconds(
                        1250),
                    RuntimeDiagnosticOutcome.Succeeded,
                    new Dictionary<string, string>
                    {
                        ["instrumentId"] =
                            "controller-one"
                    }));

        DesktopRuntimeDiagnosticEntry entry =
            DesktopRuntimeDiagnosticEntryProjector.Project(
                record);

        Assert.Equal(
            record.Sequence,
            entry.Sequence);
        Assert.Equal(
            record.TimestampUtc,
            entry.TimestampUtc);
        Assert.Equal(
            record.TimestampUtc.ToString(
                "O",
                CultureInfo.InvariantCulture),
            entry.TimestampUtcText);
        Assert.Equal(
            RuntimeDiagnosticLevel.Operational,
            entry.Level);
        Assert.Equal(
            RuntimeDiagnosticCategory.RuntimeCommand,
            entry.Category);
        Assert.Equal(
            "CommandExecutionCompleted",
            entry.EventName);
        Assert.Equal(
            "endpoint-one",
            entry.EndpointId);
        Assert.Equal(
            Generation.ToString(
                "D"),
            entry.AttachmentGeneration);
        Assert.Equal(
            "Inbound",
            entry.Direction);
        Assert.Equal(
            OperationId.ToString(
                "D"),
            entry.OperationId);
        Assert.Equal(
            "00:00:01.2500000",
            entry.Duration);
        Assert.Equal(
            "Succeeded",
            entry.Outcome);
        Assert.Equal(
            new DesktopRuntimeDiagnosticDetail(
                "instrumentId",
                "controller-one"),
            Assert.Single(
                entry.Details));
        Assert.False(
            entry.HasByteSnapshot);
    }

    [Fact]
    public void Project_MissingOptionalFields_UsesEmptyDisplayValues()
    {
        RuntimeDiagnosticRecord record =
            Publish(
                new RuntimeDiagnosticEvent(
                    RuntimeDiagnosticLevel.Operational,
                    RuntimeDiagnosticCategory.RuntimeConnection,
                    "ConnectionStateChanged"));

        DesktopRuntimeDiagnosticEntry entry =
            DesktopRuntimeDiagnosticEntryProjector.Project(
                record);

        Assert.Empty(
            entry.EndpointId);
        Assert.Empty(
            entry.AttachmentGeneration);
        Assert.Empty(
            entry.Direction);
        Assert.Empty(
            entry.OperationId);
        Assert.Empty(
            entry.Duration);
        Assert.Empty(
            entry.Outcome);
        Assert.Empty(
            entry.Details);
        Assert.Empty(
            entry.ByteSummary);
        Assert.Empty(
            entry.ByteHex);
        Assert.Equal(
            0,
            entry.OriginalByteCount);
        Assert.Equal(
            0,
            entry.CapturedByteCount);
    }

    [Fact]
    public void Project_Details_AreOrdinalAndImmutable()
    {
        Dictionary<string, string> sourceDetails =
            new()
            {
                ["zeta"] =
                    "last",
                ["Alpha"] =
                    "first",
                ["beta"] =
                    "middle"
            };

        RuntimeDiagnosticRecord record =
            Publish(
                new RuntimeDiagnosticEvent(
                    RuntimeDiagnosticLevel.Protocol,
                    RuntimeDiagnosticCategory.ProtocolExchange,
                    "ProtocolRequestSent",
                    details:
                        sourceDetails));

        DesktopRuntimeDiagnosticEntry entry =
            DesktopRuntimeDiagnosticEntryProjector.Project(
                record);

        sourceDetails["Alpha"] =
            "changed";

        Assert.Equal(
            [
                "Alpha",
                "beta",
                "zeta"
            ],
            entry.Details
                .Select(
                    detail =>
                        detail.Key)
                .ToArray());
        Assert.Equal(
            "first",
            entry.Details[0].Value);

        ICollection<DesktopRuntimeDiagnosticDetail> collection =
            Assert.IsAssignableFrom<
                ICollection<DesktopRuntimeDiagnosticDetail>>(
                entry.Details);

        Assert.Throws<NotSupportedException>(
            () =>
                collection.Add(
                    new DesktopRuntimeDiagnosticDetail(
                        "new",
                        "value")));
    }

    [Fact]
    public void Project_CompleteByteSnapshot_FormatsUppercaseHex()
    {
        byte[] source =
        [
            0x0A,
            0xB5,
            0x00
        ];

        RuntimeDiagnosticRecord record =
            Publish(
                CreateByteEvent(
                    new RuntimeDiagnosticByteSnapshot(
                        source.Length,
                        source,
                        isTruncated: false)));

        DesktopRuntimeDiagnosticEntry entry =
            DesktopRuntimeDiagnosticEntryProjector.Project(
                record);

        source[0] =
            0xFF;

        Assert.True(
            entry.HasByteSnapshot);
        Assert.Equal(
            3,
            entry.OriginalByteCount);
        Assert.Equal(
            3,
            entry.CapturedByteCount);
        Assert.False(
            entry.IsByteSnapshotTruncated);
        Assert.Equal(
            "3/3 bytes",
            entry.ByteSummary);
        Assert.Equal(
            "0AB500",
            entry.ByteHex);
    }

    [Fact]
    public void Project_TruncatedByteSnapshot_ReportsBothCounts()
    {
        RuntimeDiagnosticRecord record =
            Publish(
                CreateByteEvent(
                    new RuntimeDiagnosticByteSnapshot(
                        originalByteCount: 5,
                        capturedBytes:
                        [
                            0x01,
                            0x02
                        ],
                        isTruncated: true)));

        DesktopRuntimeDiagnosticEntry entry =
            DesktopRuntimeDiagnosticEntryProjector.Project(
                record);

        Assert.Equal(
            5,
            entry.OriginalByteCount);
        Assert.Equal(
            2,
            entry.CapturedByteCount);
        Assert.True(
            entry.IsByteSnapshotTruncated);
        Assert.Equal(
            "2/5 bytes (truncated)",
            entry.ByteSummary);
        Assert.Equal(
            "0102",
            entry.ByteHex);
    }

    [Fact]
    public void Project_NullRecord_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "record",
            () =>
                DesktopRuntimeDiagnosticEntryProjector.Project(
                    null!));
    }

    private static RuntimeDiagnosticEvent CreateByteEvent(
        RuntimeDiagnosticByteSnapshot snapshot)
    {
        return new RuntimeDiagnosticEvent(
            RuntimeDiagnosticLevel.Bytes,
            RuntimeDiagnosticCategory.TransportBytes,
            "TransportBytesReceived",
            endpointId:
                "endpoint-one",
            direction:
                RuntimeDiagnosticDirection.Inbound,
            byteSnapshot:
                snapshot);
    }

    private static RuntimeDiagnosticRecord Publish(
        RuntimeDiagnosticEvent diagnosticEvent)
    {
        var session =
            new DesktopRuntimeDiagnosticSession(
                RuntimeDiagnosticLevel.Bytes);

        session.Publisher.Publish(
            diagnosticEvent);

        return Assert.Single(
            session.CaptureDiagnostics());
    }
}
