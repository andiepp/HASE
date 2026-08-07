using Google.Protobuf.WellKnownTypes;
using RuntimeDiagnostics = global::Hase.Runtime.Diagnostics;
using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostProjectedDiagnosticObservationMapperTests
{
    [Fact]
    public void Map_NullObservation_ShouldThrow()
    {
        var mapper = new RuntimeHostProjectedDiagnosticObservationMapper();

        Assert.Throws<ArgumentNullException>(
            "observation",
            () => mapper.Map(null!));
    }

    [Fact]
    public void Map_CompleteOperationalRecord_ShouldPreserveStructure()
    {
        Guid generation = Guid.NewGuid();
        Guid operationId = Guid.NewGuid();
        DateTimeOffset timestamp = new(
            2026,
            8,
            7,
            10,
            20,
            30,
            TimeSpan.Zero);
        Northbound.RuntimeHostProjectedDiagnosticObservation source =
            CreateObservation(
                sequence: 7,
                sourceSequenceOffset: 3,
                timestamp: timestamp,
                endpointId: "endpoint-one",
                attachmentGeneration: generation,
                direction: RuntimeDiagnostics.RuntimeDiagnosticDirection.Inbound,
                operationId: operationId,
                duration: TimeSpan.FromMilliseconds(125),
                outcome: RuntimeDiagnostics.RuntimeDiagnosticOutcome.Succeeded,
                details: new Dictionary<string, string>
                {
                    ["CurrentState"] = "Ready",
                    ["exceptionMessage"] = "must-not-project"
                });

        GrpcV1.ProjectedDiagnosticObservation result =
            new RuntimeHostProjectedDiagnosticObservationMapper().Map(source);

        Assert.Equal((ulong)7, result.Sequence);
        Assert.Equal("host-one", result.Record.RuntimeHostId);
        Assert.Equal((ulong)4, result.Record.SourceSequence);
        Assert.Equal(Timestamp.FromDateTimeOffset(timestamp), result.Record.TimestampUtc);
        Assert.Equal(GrpcV1.RuntimeDiagnosticLevel.Operational, result.Record.Level);
        Assert.Equal(
            GrpcV1.RuntimeDiagnosticCategory.RuntimeConnection,
            result.Record.Category);
        Assert.Equal("TestEvent", result.Record.EventName);
        Assert.Equal(
            GrpcV1.RuntimeDiagnosticSeverity.Information,
            result.Record.Severity);
        Assert.True(result.Record.HasEndpointId);
        Assert.Equal("endpoint-one", result.Record.EndpointId);
        Assert.True(result.Record.HasAttachmentGeneration);
        Assert.Equal(generation.ToString("D"), result.Record.AttachmentGeneration);
        Assert.True(result.Record.HasDirection);
        Assert.Equal(GrpcV1.RuntimeDiagnosticDirection.Inbound, result.Record.Direction);
        Assert.True(result.Record.HasOperationId);
        Assert.Equal(operationId.ToString("D"), result.Record.OperationId);
        Assert.Equal(
            Duration.FromTimeSpan(TimeSpan.FromMilliseconds(125)),
            result.Record.Duration);
        Assert.True(result.Record.HasOutcome);
        Assert.Equal(GrpcV1.RuntimeDiagnosticOutcome.Succeeded, result.Record.Outcome);
        Assert.Equal("Ready", Assert.Single(result.Record.Details).Value);
        Assert.Null(result.Record.ByteSnapshot);
    }

    [Fact]
    public void Map_AbsentOptionalMembers_ShouldRemainAbsent()
    {
        GrpcV1.ProjectedDiagnosticRecord result =
            new RuntimeHostProjectedDiagnosticObservationMapper()
                .Map(CreateObservation())
                .Record;

        Assert.False(result.HasEndpointId);
        Assert.False(result.HasAttachmentGeneration);
        Assert.False(result.HasDirection);
        Assert.False(result.HasOperationId);
        Assert.Null(result.Duration);
        Assert.False(result.HasOutcome);
        Assert.Empty(result.Details);
        Assert.Null(result.ByteSnapshot);
    }

    [Fact]
    public void Map_BytesRecord_ShouldPreserveBoundedSnapshotExactly()
    {
        byte[] captured = [0x2A, 0x49, 0x44, 0x4E, 0x3F, 0x0D];
        Northbound.RuntimeHostProjectedDiagnosticObservation source =
            CreateObservation(
                level: RuntimeDiagnostics.RuntimeDiagnosticLevel.Bytes,
                category: RuntimeDiagnostics.RuntimeDiagnosticCategory.TransportBytes,
                direction: RuntimeDiagnostics.RuntimeDiagnosticDirection.Outbound,
                byteSnapshot: new RuntimeDiagnostics.RuntimeDiagnosticByteSnapshot(
                    originalByteCount: 48,
                    capturedBytes: captured,
                    isTruncated: true));

        GrpcV1.ProjectedDiagnosticRecord result =
            new RuntimeHostProjectedDiagnosticObservationMapper().Map(source).Record;

        Assert.Equal(GrpcV1.RuntimeDiagnosticLevel.Bytes, result.Level);
        Assert.Equal(
            GrpcV1.RuntimeDiagnosticCategory.TransportBytes,
            result.Category);
        Assert.NotNull(result.ByteSnapshot);
        Assert.Equal((ulong)48, result.ByteSnapshot.OriginalByteCount);
        Assert.Equal(captured, result.ByteSnapshot.CapturedBytes.ToByteArray());
        Assert.True(result.ByteSnapshot.IsTruncated);
    }

    [Fact]
    public void Map_ProtocolRecord_ShouldPreserveProtocolLevel()
    {
        GrpcV1.ProjectedDiagnosticRecord result =
            new RuntimeHostProjectedDiagnosticObservationMapper()
                .Map(CreateObservation(
                    level: RuntimeDiagnostics.RuntimeDiagnosticLevel.Protocol,
                    category:
                        RuntimeDiagnostics.RuntimeDiagnosticCategory.ProtocolExchange))
                .Record;

        Assert.Equal(GrpcV1.RuntimeDiagnosticLevel.Protocol, result.Level);
        Assert.Equal(
            GrpcV1.RuntimeDiagnosticCategory.ProtocolExchange,
            result.Category);
    }

    [Theory]
    [InlineData(RuntimeDiagnostics.RuntimeDiagnosticLevel.Operational, 1)]
    [InlineData(RuntimeDiagnostics.RuntimeDiagnosticLevel.Protocol, 2)]
    [InlineData(RuntimeDiagnostics.RuntimeDiagnosticLevel.Bytes, 3)]
    public void Map_Level_ShouldUseStableRemoteValue(
        RuntimeDiagnostics.RuntimeDiagnosticLevel level,
        int expected)
    {
        GrpcV1.ProjectedDiagnosticRecord result =
            MapRecord(level: level);

        Assert.Equal(expected, (int)result.Level);
    }

    [Theory]
    [InlineData(RuntimeDiagnostics.RuntimeDiagnosticCategory.RuntimeAttachment, 1)]
    [InlineData(RuntimeDiagnostics.RuntimeDiagnosticCategory.RuntimeConnection, 2)]
    [InlineData(RuntimeDiagnostics.RuntimeDiagnosticCategory.RuntimeSynchronization, 3)]
    [InlineData(RuntimeDiagnostics.RuntimeDiagnosticCategory.RuntimeRecovery, 4)]
    [InlineData(RuntimeDiagnostics.RuntimeDiagnosticCategory.RuntimeProperty, 5)]
    [InlineData(RuntimeDiagnostics.RuntimeDiagnosticCategory.RuntimeCommand, 6)]
    [InlineData(RuntimeDiagnostics.RuntimeDiagnosticCategory.RuntimeEvent, 7)]
    [InlineData(RuntimeDiagnostics.RuntimeDiagnosticCategory.ProtocolExchange, 8)]
    [InlineData(RuntimeDiagnostics.RuntimeDiagnosticCategory.TransportBytes, 9)]
    public void Map_Category_ShouldUseStableRemoteValue(
        RuntimeDiagnostics.RuntimeDiagnosticCategory category,
        int expected)
    {
        Assert.Equal(expected, (int)MapRecord(category: category).Category);
    }

    [Theory]
    [InlineData(RuntimeDiagnostics.RuntimeDiagnosticSeverity.Trace, 1)]
    [InlineData(RuntimeDiagnostics.RuntimeDiagnosticSeverity.Information, 2)]
    [InlineData(RuntimeDiagnostics.RuntimeDiagnosticSeverity.Warning, 3)]
    [InlineData(RuntimeDiagnostics.RuntimeDiagnosticSeverity.Error, 4)]
    public void Map_Severity_ShouldUseStableRemoteValue(
        RuntimeDiagnostics.RuntimeDiagnosticSeverity severity,
        int expected)
    {
        Assert.Equal(expected, (int)MapRecord(severity: severity).Severity);
    }

    [Theory]
    [InlineData(RuntimeDiagnostics.RuntimeDiagnosticDirection.Outbound, 1)]
    [InlineData(RuntimeDiagnostics.RuntimeDiagnosticDirection.Inbound, 2)]
    public void Map_Direction_ShouldUseStableRemoteValue(
        RuntimeDiagnostics.RuntimeDiagnosticDirection direction,
        int expected)
    {
        Assert.Equal(
            expected,
            (int)MapRecord(direction: direction).Direction);
    }

    [Theory]
    [InlineData(RuntimeDiagnostics.RuntimeDiagnosticOutcome.Succeeded, 1)]
    [InlineData(RuntimeDiagnostics.RuntimeDiagnosticOutcome.Failed, 2)]
    [InlineData(RuntimeDiagnostics.RuntimeDiagnosticOutcome.Cancelled, 3)]
    [InlineData(RuntimeDiagnostics.RuntimeDiagnosticOutcome.TimedOut, 4)]
    public void Map_Outcome_ShouldUseStableRemoteValue(
        RuntimeDiagnostics.RuntimeDiagnosticOutcome outcome,
        int expected)
    {
        Assert.Equal(expected, (int)MapRecord(outcome: outcome).Outcome);
    }

    private static GrpcV1.ProjectedDiagnosticRecord MapRecord(
        RuntimeDiagnostics.RuntimeDiagnosticLevel level =
            RuntimeDiagnostics.RuntimeDiagnosticLevel.Operational,
        RuntimeDiagnostics.RuntimeDiagnosticCategory category =
            RuntimeDiagnostics.RuntimeDiagnosticCategory.RuntimeConnection,
        RuntimeDiagnostics.RuntimeDiagnosticSeverity severity =
            RuntimeDiagnostics.RuntimeDiagnosticSeverity.Information,
        RuntimeDiagnostics.RuntimeDiagnosticDirection? direction = null,
        RuntimeDiagnostics.RuntimeDiagnosticOutcome? outcome = null)
    {
        return new RuntimeHostProjectedDiagnosticObservationMapper()
            .Map(CreateObservation(
                level: level,
                category: category,
                severity: severity,
                direction: direction,
                outcome: outcome))
            .Record;
    }

    private static Northbound.RuntimeHostProjectedDiagnosticObservation
        CreateObservation(
            long sequence = 1,
            int sourceSequenceOffset = 0,
            DateTimeOffset? timestamp = null,
            RuntimeDiagnostics.RuntimeDiagnosticLevel level =
                RuntimeDiagnostics.RuntimeDiagnosticLevel.Operational,
            RuntimeDiagnostics.RuntimeDiagnosticCategory category =
                RuntimeDiagnostics.RuntimeDiagnosticCategory.RuntimeConnection,
            RuntimeDiagnostics.RuntimeDiagnosticSeverity severity =
                RuntimeDiagnostics.RuntimeDiagnosticSeverity.Information,
            string? endpointId = null,
            Guid? attachmentGeneration = null,
            RuntimeDiagnostics.RuntimeDiagnosticDirection? direction = null,
            Guid? operationId = null,
            TimeSpan? duration = null,
            RuntimeDiagnostics.RuntimeDiagnosticOutcome? outcome = null,
            IReadOnlyDictionary<string, string>? details = null,
            RuntimeDiagnostics.RuntimeDiagnosticByteSnapshot? byteSnapshot = null)
    {
        var collector = new RuntimeDiagnostics.BoundedRuntimeDiagnosticCollector(
            sourceSequenceOffset + 1,
            RuntimeDiagnostics.RuntimeDiagnosticLevel.Bytes);
        RuntimeDiagnostics.RuntimeDiagnosticPublisher publisher;
        if (timestamp.HasValue)
        {
            Func<DateTimeOffset> clock = () => timestamp.Value;
            publisher = (RuntimeDiagnostics.RuntimeDiagnosticPublisher)
                Activator.CreateInstance(
                    typeof(RuntimeDiagnostics.RuntimeDiagnosticPublisher),
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic,
                    binder: null,
                    args: [collector, clock],
                    culture: null)!;
        }
        else
        {
            publisher = new RuntimeDiagnostics.RuntimeDiagnosticPublisher(collector);
        }

        for (int index = 0; index < sourceSequenceOffset; index++)
        {
            publisher.Publish(CreateEvent());
        }

        publisher.Publish(new RuntimeDiagnostics.RuntimeDiagnosticEvent(
            level,
            category,
            "TestEvent",
            severity,
            endpointId,
            attachmentGeneration,
            direction,
            operationId,
            duration,
            outcome,
            details,
            byteSnapshot));
        RuntimeDiagnostics.RuntimeDiagnosticRecord source =
            collector.GetSnapshot().Last();

        var projector = new Northbound.RuntimeHostDiagnosticProjector(
            new Northbound.RuntimeHostId("host-one"),
            RuntimeDiagnostics.RuntimeDiagnosticLevel.Bytes,
            new Northbound.RuntimeHostDiagnosticProjectionPolicy(
                isEnabled: true,
                maximumLevel: RuntimeDiagnostics.RuntimeDiagnosticLevel.Bytes));
        Assert.True(projector.TryProject(
            source,
            out Northbound.RuntimeHostProjectedDiagnosticRecord? projected));

        return new Northbound.RuntimeHostProjectedDiagnosticObservation(
            new Northbound.RuntimeHostDiagnosticProjectionSequence(sequence),
            projected!);
    }

    private static RuntimeDiagnostics.RuntimeDiagnosticEvent CreateEvent()
    {
        return new RuntimeDiagnostics.RuntimeDiagnosticEvent(
            RuntimeDiagnostics.RuntimeDiagnosticLevel.Operational,
            RuntimeDiagnostics.RuntimeDiagnosticCategory.RuntimeConnection,
            "Skipped",
            RuntimeDiagnostics.RuntimeDiagnosticSeverity.Information);
    }

}
