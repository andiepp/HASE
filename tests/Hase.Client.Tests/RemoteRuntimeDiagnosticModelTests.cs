using Hase.Client;

namespace Hase.Client.Tests;

public sealed class RemoteRuntimeDiagnosticModelTests
{
    [Fact]
    public void Enums_ShouldExposeProjectedDiagnosticVocabulary()
    {
        Assert.Equal(3, Enum.GetValues<RemoteRuntimeDiagnosticLevel>().Length);
        Assert.Equal(9, Enum.GetValues<RemoteRuntimeDiagnosticCategory>().Length);
        Assert.Equal(4, Enum.GetValues<RemoteRuntimeDiagnosticSeverity>().Length);
        Assert.Equal(2, Enum.GetValues<RemoteRuntimeDiagnosticDirection>().Length);
        Assert.Equal(4, Enum.GetValues<RemoteRuntimeDiagnosticOutcome>().Length);
        Assert.Equal(
            6,
            Enum.GetValues<RemoteRuntimeDiagnosticStreamFailureKind>().Length);
    }

    [Fact]
    public void ByteSnapshot_Values_ShouldBeImmutable()
    {
        byte[] source = [0x10, 0x20];
        var snapshot = new RemoteRuntimeDiagnosticByteSnapshot(2, source, false);
        source[0] = 0xFF;
        byte[] copy = snapshot.ToArray();
        copy[1] = 0xFF;

        Assert.Equal(new byte[] { 0x10, 0x20 }, snapshot.Bytes);
        Assert.Equal(2, snapshot.CapturedByteCount);
        Assert.False(snapshot.IsTruncated);
    }

    [Fact]
    public void ByteSnapshot_InvalidCounts_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            "originalByteCount",
            () => new RemoteRuntimeDiagnosticByteSnapshot(-1, [], false));
        Assert.Throws<ArgumentException>(
            "capturedBytes",
            () => new RemoteRuntimeDiagnosticByteSnapshot(1, [0x01, 0x02], false));
        Assert.Throws<ArgumentOutOfRangeException>(
            "capturedBytes",
            () => new RemoteRuntimeDiagnosticByteSnapshot(
                257,
                new byte[257],
                false));
    }

    [Fact]
    public void ByteSnapshot_InconsistentTruncation_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "isTruncated",
            () => new RemoteRuntimeDiagnosticByteSnapshot(2, [0x01], false));
        Assert.Throws<ArgumentException>(
            "isTruncated",
            () => new RemoteRuntimeDiagnosticByteSnapshot(1, [0x01], true));
    }

    [Fact]
    public void Record_AllValues_ShouldBePreservedAndNormalized()
    {
        Guid generation = Guid.NewGuid();
        Guid operation = Guid.NewGuid();
        var snapshot = new RemoteRuntimeDiagnosticByteSnapshot(2, [1, 2], false);
        RemoteRuntimeDiagnosticRecord record = CreateRecord(
            runtimeHostId: " host-01 ",
            eventName: " Exchange ",
            endpointId: " endpoint-01 ",
            attachmentGeneration: generation,
            direction: RemoteRuntimeDiagnosticDirection.Inbound,
            operationId: operation,
            duration: TimeSpan.FromMilliseconds(5),
            outcome: RemoteRuntimeDiagnosticOutcome.Succeeded,
            byteSnapshot: snapshot);

        Assert.Equal("host-01", record.RuntimeHostId);
        Assert.Equal("Exchange", record.EventName);
        Assert.Equal("endpoint-01", record.EndpointId);
        Assert.Equal(generation, record.AttachmentGeneration);
        Assert.Equal(operation, record.OperationId);
        Assert.Same(snapshot, record.ByteSnapshot);
    }

    [Fact]
    public void Record_InvalidRuntimeHostIdentity_ShouldThrow()
    {
        foreach (string? value in new string?[] { null, "", "   " })
        {
            Assert.Throws<ArgumentException>(
                "runtimeHostId",
                () => CreateRecord(runtimeHostId: value!));
        }
    }

    [Fact]
    public void Record_InvalidEventName_ShouldThrow()
    {
        foreach (string? value in new string?[] { null, "", "   " })
        {
            Assert.Throws<ArgumentException>(
                "eventName",
                () => CreateRecord(eventName: value!));
        }
    }

    [Fact]
    public void Record_NonPositiveSourceSequence_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            "sourceSequence",
            () => CreateRecord(sourceSequence: 0));
    }

    [Fact]
    public void Record_NonUtcTimestamp_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "timestampUtc",
            () => CreateRecord(
                timestampUtc: new DateTimeOffset(
                    2026,
                    8,
                    7,
                    12,
                    0,
                    0,
                    TimeSpan.FromHours(2))));
    }

    [Fact]
    public void Record_NegativeDuration_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            "duration",
            () => CreateRecord(duration: TimeSpan.FromTicks(-1)));
    }

    [Fact]
    public void Record_Details_ShouldBeCopiedAndReadOnly()
    {
        var details = new Dictionary<string, string> { ["key"] = "value" };
        RemoteRuntimeDiagnosticRecord record = CreateRecord(details: details);
        details["key"] = "changed";

        Assert.Equal("value", record.Details["key"]);
        Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(record.Details);
        Assert.Throws<NotSupportedException>(
            () => ((IDictionary<string, string>)record.Details).Add("new", "value"));
    }

    [Fact]
    public void Observation_ShouldRequirePositiveSequenceAndRecord()
    {
        RemoteRuntimeDiagnosticRecord record = CreateRecord();
        var observation = new RemoteRuntimeDiagnosticObservation(1, record);

        Assert.Equal(1, observation.Sequence);
        Assert.Same(record, observation.Record);
        Assert.Throws<ArgumentOutOfRangeException>(
            "sequence",
            () => new RemoteRuntimeDiagnosticObservation(0, record));
        Assert.Throws<ArgumentNullException>(
            "record",
            () => new RemoteRuntimeDiagnosticObservation(1, null!));
    }

    private static RemoteRuntimeDiagnosticRecord CreateRecord(
        string runtimeHostId = "host-01",
        long sourceSequence = 1,
        DateTimeOffset? timestampUtc = null,
        string eventName = "Event",
        string? endpointId = null,
        Guid? attachmentGeneration = null,
        RemoteRuntimeDiagnosticDirection? direction = null,
        Guid? operationId = null,
        TimeSpan? duration = null,
        RemoteRuntimeDiagnosticOutcome? outcome = null,
        IReadOnlyDictionary<string, string>? details = null,
        RemoteRuntimeDiagnosticByteSnapshot? byteSnapshot = null) =>
        new(
            runtimeHostId,
            sourceSequence,
            timestampUtc ?? DateTimeOffset.UnixEpoch,
            byteSnapshot is null
                ? RemoteRuntimeDiagnosticLevel.Operational
                : RemoteRuntimeDiagnosticLevel.Bytes,
            RemoteRuntimeDiagnosticCategory.RuntimeConnection,
            eventName,
            RemoteRuntimeDiagnosticSeverity.Information,
            endpointId,
            attachmentGeneration,
            direction,
            operationId,
            duration,
            outcome,
            details,
            byteSnapshot);
}
