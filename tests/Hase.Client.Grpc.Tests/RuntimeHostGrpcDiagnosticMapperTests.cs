using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Hase.Client;
using Hase.Client.Grpc;
using GrpcV1 = Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Client.Grpc.Tests;

public sealed class RuntimeHostGrpcDiagnosticMapperTests
{
    [Fact]
    public void Map_CompleteRecord_ShouldPreserveStructure()
    {
        Guid generation = Guid.NewGuid();
        Guid operation = Guid.NewGuid();
        GrpcV1.ProjectedDiagnosticObservation source = CreateObservation();
        source.Record.EndpointId = "endpoint-01";
        source.Record.AttachmentGeneration = generation.ToString("D");
        source.Record.Direction = GrpcV1.RuntimeDiagnosticDirection.Inbound;
        source.Record.OperationId = operation.ToString("D");
        source.Record.Duration = Duration.FromTimeSpan(TimeSpan.FromMilliseconds(5));
        source.Record.Outcome = GrpcV1.RuntimeDiagnosticOutcome.Succeeded;
        source.Record.Details.Add("State", "Ready");

        RemoteRuntimeDiagnosticObservation result =
            new RuntimeHostGrpcDiagnosticMapper().Map(source);

        Assert.Equal(1, result.Sequence);
        Assert.Equal("host-01", result.Record.RuntimeHostId);
        Assert.Equal(generation, result.Record.AttachmentGeneration);
        Assert.Equal(operation, result.Record.OperationId);
        Assert.Equal("Ready", result.Record.Details["State"]);
    }

    [Fact]
    public void Map_Bytes_ShouldPreserveBoundedSnapshot()
    {
        GrpcV1.ProjectedDiagnosticObservation source = CreateObservation();
        source.Record.Level = GrpcV1.RuntimeDiagnosticLevel.Bytes;
        source.Record.Category = GrpcV1.RuntimeDiagnosticCategory.TransportBytes;
        source.Record.ByteSnapshot = new GrpcV1.ProjectedDiagnosticByteSnapshot
        {
            OriginalByteCount = 4,
            CapturedBytes = ByteString.CopyFrom(new byte[] { 0x01, 0x02 }),
            IsTruncated = true
        };

        RemoteRuntimeDiagnosticByteSnapshot result =
            new RuntimeHostGrpcDiagnosticMapper().Map(source).Record.ByteSnapshot!;

        Assert.Equal(4, result.OriginalByteCount);
        Assert.Equal(new byte[] { 0x01, 0x02 }, result.Bytes);
        Assert.True(result.IsTruncated);
    }

    [Fact]
    public void Map_UnspecifiedEnum_ShouldReject()
    {
        GrpcV1.ProjectedDiagnosticObservation source = CreateObservation();
        source.Record.Level = GrpcV1.RuntimeDiagnosticLevel.Unspecified;

        Assert.Throws<InvalidDataException>(
            () => new RuntimeHostGrpcDiagnosticMapper().Map(source));
    }

    [Fact]
    public void Map_InvalidOptionalGuid_ShouldReject()
    {
        GrpcV1.ProjectedDiagnosticObservation source = CreateObservation();
        source.Record.OperationId = "not-a-guid";

        Assert.Throws<InvalidDataException>(
            () => new RuntimeHostGrpcDiagnosticMapper().Map(source));
    }

    [Fact]
    public void Map_OverflowingSequence_ShouldReject()
    {
        GrpcV1.ProjectedDiagnosticObservation source = CreateObservation();
        source.Sequence = ulong.MaxValue;

        Assert.Throws<OverflowException>(
            () => new RuntimeHostGrpcDiagnosticMapper().Map(source));
    }

    [Fact]
    public void Map_MissingRecord_ShouldReject()
    {
        Assert.Throws<InvalidDataException>(
            () => new RuntimeHostGrpcDiagnosticMapper().Map(
                new GrpcV1.ProjectedDiagnosticObservation { Sequence = 1 }));
    }

    internal static GrpcV1.ProjectedDiagnosticObservation CreateObservation(
        ulong sequence = 1) =>
        new()
        {
            Sequence = sequence,
            Record = new GrpcV1.ProjectedDiagnosticRecord
            {
                RuntimeHostId = "host-01",
                SourceSequence = sequence,
                TimestampUtc = Timestamp.FromDateTimeOffset(DateTimeOffset.UnixEpoch),
                Level = GrpcV1.RuntimeDiagnosticLevel.Operational,
                Category = GrpcV1.RuntimeDiagnosticCategory.RuntimeConnection,
                EventName = "Connected",
                Severity = GrpcV1.RuntimeDiagnosticSeverity.Information
            }
        };
}
