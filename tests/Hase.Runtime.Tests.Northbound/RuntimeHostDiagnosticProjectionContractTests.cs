using Hase.Runtime.Diagnostics;
using Hase.Runtime.Northbound;

namespace Hase.Runtime.Tests.Northbound;

public sealed class RuntimeHostDiagnosticProjectionContractTests
{
    [Fact]
    public void Policy_Default_IsDisabledWithOperationalCeiling()
    {
        var policy = new RuntimeHostDiagnosticProjectionPolicy();

        Assert.False(policy.IsEnabled);
        Assert.Equal(RuntimeDiagnosticLevel.Operational, policy.MaximumLevel);
        Assert.False(policy.Allows(RuntimeDiagnosticLevel.Operational));
    }

    [Fact]
    public void Policy_EnabledWithoutLevel_DefaultsToOperational()
    {
        var policy = new RuntimeHostDiagnosticProjectionPolicy(isEnabled: true);

        Assert.True(policy.IsEnabled);
        Assert.Equal(RuntimeDiagnosticLevel.Operational, policy.MaximumLevel);
    }

    [Theory]
    [InlineData(RuntimeDiagnosticLevel.Operational, true)]
    [InlineData(RuntimeDiagnosticLevel.Protocol, true)]
    [InlineData(RuntimeDiagnosticLevel.Bytes, false)]
    public void Policy_AllowsOnlyLevelsAtOrBelowExplicitCeiling(
        RuntimeDiagnosticLevel level,
        bool expected)
    {
        var policy = new RuntimeHostDiagnosticProjectionPolicy(
            isEnabled: true,
            maximumLevel: RuntimeDiagnosticLevel.Protocol);

        Assert.Equal(expected, policy.Allows(level));
    }

    [Fact]
    public void Policy_UndefinedMaximum_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RuntimeHostDiagnosticProjectionPolicy(
                isEnabled: true,
                maximumLevel: (RuntimeDiagnosticLevel)99));
    }

    [Fact]
    public void Projector_RemoteCeilingAboveHostCapture_IsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new RuntimeHostDiagnosticProjector(
                new RuntimeHostId("host-one"),
                RuntimeDiagnosticLevel.Protocol,
                new RuntimeHostDiagnosticProjectionPolicy(
                    isEnabled: true,
                    maximumLevel: RuntimeDiagnosticLevel.Bytes)));
    }

    [Fact]
    public void Projector_DefaultPolicy_ProjectsNothing()
    {
        var projector = new RuntimeHostDiagnosticProjector(
            new RuntimeHostId("host-one"),
            RuntimeDiagnosticLevel.Bytes);

        bool projected = projector.TryProject(
            CreateRecord(RuntimeDiagnosticLevel.Operational),
            out RuntimeHostProjectedDiagnosticRecord? record);

        Assert.False(projected);
        Assert.Null(record);
    }

    [Fact]
    public void Projector_OperationalRecord_CopiesStructureAndFiltersUnknownDetails()
    {
        var hostId = new RuntimeHostId("host-one");
        var projector = new RuntimeHostDiagnosticProjector(
            hostId,
            RuntimeDiagnosticLevel.Bytes,
            new RuntimeHostDiagnosticProjectionPolicy(isEnabled: true));
        Guid attachmentGeneration = Guid.NewGuid();
        Guid operationId = Guid.NewGuid();
        RuntimeDiagnosticRecord source = CreateRecord(
            RuntimeDiagnosticLevel.Operational,
            RuntimeDiagnosticCategory.RuntimeConnection,
            "ConnectionChanged",
            endpointId: "endpoint-one",
            attachmentGeneration: attachmentGeneration,
            direction: RuntimeDiagnosticDirection.Inbound,
            operationId: operationId,
            duration: TimeSpan.FromMilliseconds(4),
            outcome: RuntimeDiagnosticOutcome.Succeeded,
            details: new Dictionary<string, string>
            {
                ["CurrentState"] = "Ready",
                ["unknownFutureDetail"] = "MUST_NOT_PROJECT",
                ["exceptionMessage"] = "SENSITIVE"
            });

        bool projected = projector.TryProject(
            source,
            out RuntimeHostProjectedDiagnosticRecord? record);

        Assert.True(projected);
        Assert.NotNull(record);
        Assert.Equal(hostId, record.RuntimeHostId);
        Assert.Equal(source.Sequence, record.SourceSequence);
        Assert.Equal(TimeSpan.Zero, record.TimestampUtc.Offset);
        Assert.Equal(source.TimestampUtc, record.TimestampUtc);
        Assert.Equal(source.Level, record.Level);
        Assert.Equal(source.Category, record.Category);
        Assert.Equal(source.EventName, record.EventName);
        Assert.Equal(source.Severity, record.Severity);
        Assert.Equal("endpoint-one", record.EndpointId);
        Assert.Equal(attachmentGeneration, record.AttachmentGeneration);
        Assert.Equal(RuntimeDiagnosticDirection.Inbound, record.Direction);
        Assert.Equal(operationId, record.OperationId);
        Assert.Equal(TimeSpan.FromMilliseconds(4), record.Duration);
        Assert.Equal(RuntimeDiagnosticOutcome.Succeeded, record.Outcome);
        Assert.Equal("Ready", record.Details["CurrentState"]);
        Assert.Single(record.Details);
        Assert.Null(record.ByteSnapshot);
    }

    [Fact]
    public void Projector_RecordAboveRemoteCeiling_IsNotProjected()
    {
        var projector = new RuntimeHostDiagnosticProjector(
            new RuntimeHostId("host-one"),
            RuntimeDiagnosticLevel.Bytes,
            new RuntimeHostDiagnosticProjectionPolicy(isEnabled: true));

        bool projected = projector.TryProject(
            CreateRecord(RuntimeDiagnosticLevel.Protocol),
            out RuntimeHostProjectedDiagnosticRecord? record);

        Assert.False(projected);
        Assert.Null(record);
    }

    [Fact]
    public void Projector_BytesRecord_CopiesExactBoundedSnapshotAndSafeMetadata()
    {
        byte[] sourceBytes = [0x2A, 0x49, 0x44, 0x4E, 0x3F, 0x0D];
        RuntimeDiagnosticRecord source = CreateRecord(
            RuntimeDiagnosticLevel.Bytes,
            RuntimeDiagnosticCategory.TransportBytes,
            "TransportBytesSent",
            endpointId: "kel-one",
            direction: RuntimeDiagnosticDirection.Outbound,
            details: new Dictionary<string, string>
            {
                ["protocolFamily"] = "ScpiText",
                ["correlationId"] = "abc",
                ["portName"] = "MUST_NOT_PROJECT"
            },
            byteSnapshot: new RuntimeDiagnosticByteSnapshot(
                sourceBytes.Length,
                sourceBytes,
                isTruncated: false));
        var projector = new RuntimeHostDiagnosticProjector(
            new RuntimeHostId("host-one"),
            RuntimeDiagnosticLevel.Bytes,
            new RuntimeHostDiagnosticProjectionPolicy(
                isEnabled: true,
                maximumLevel: RuntimeDiagnosticLevel.Bytes));

        Assert.True(projector.TryProject(
            source,
            out RuntimeHostProjectedDiagnosticRecord? record));

        RuntimeHostProjectedDiagnosticByteSnapshot snapshot =
            Assert.IsType<RuntimeHostProjectedDiagnosticByteSnapshot>(
                record!.ByteSnapshot);
        Assert.Equal(sourceBytes, snapshot.ToArray());
        Assert.Equal(sourceBytes.Length, snapshot.OriginalByteCount);
        Assert.False(snapshot.IsTruncated);
        Assert.Equal("ScpiText", record.Details["protocolFamily"]);
        Assert.Equal("abc", record.Details["correlationId"]);
        Assert.Equal(2, record.Details.Count);
    }

    [Fact]
    public void ProjectedSnapshot_OwnsBytesAndReturnedCopies()
    {
        byte[] source = [0x01, 0x02, 0x03];
        var snapshot = new RuntimeHostProjectedDiagnosticByteSnapshot(
            source.Length,
            source,
            isTruncated: false);

        source[0] = 0xFF;
        byte[] returned = snapshot.ToArray();
        returned[1] = 0xFF;

        Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, snapshot.ToArray());
        Assert.Throws<NotSupportedException>(() =>
            ((IList<byte>)snapshot.Bytes)[0] = 0xFF);
    }

    private static RuntimeDiagnosticRecord CreateRecord(
        RuntimeDiagnosticLevel level,
        RuntimeDiagnosticCategory category =
            RuntimeDiagnosticCategory.RuntimeConnection,
        string eventName = "TestEvent",
        string? endpointId = null,
        Guid? attachmentGeneration = null,
        RuntimeDiagnosticDirection? direction = null,
        Guid? operationId = null,
        TimeSpan? duration = null,
        RuntimeDiagnosticOutcome? outcome = null,
        IReadOnlyDictionary<string, string>? details = null,
        RuntimeDiagnosticByteSnapshot? byteSnapshot = null)
    {
        var collector = new BoundedRuntimeDiagnosticCollector(
            1,
            RuntimeDiagnosticLevel.Bytes);
        var publisher = new RuntimeDiagnosticPublisher(collector);
        publisher.Publish(new RuntimeDiagnosticEvent(
            level,
            category,
            eventName,
            RuntimeDiagnosticSeverity.Information,
            endpointId,
            attachmentGeneration,
            direction,
            operationId,
            duration,
            outcome,
            details,
            byteSnapshot));
        return Assert.Single(collector.GetSnapshot());
    }
}
