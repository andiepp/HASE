using Hase.Client.Configuration;
using Hase.Client.Diagnostics;

namespace Hase.Client.Tests;

public sealed class RemoteRuntimeDiagnosticClientEventMapperTests
{
    [Fact]
    public void Map_ProjectedBytes_ShouldPreserveScopeAndSnapshot()
    {
        var bytes = new RemoteRuntimeDiagnosticByteSnapshot(2, [1, 2], false);
        var source = new RemoteRuntimeDiagnosticRecord(
            "host-01",
            7,
            DateTimeOffset.UnixEpoch,
            RemoteRuntimeDiagnosticLevel.Bytes,
            RemoteRuntimeDiagnosticCategory.TransportBytes,
            "BytesReceived",
            RemoteRuntimeDiagnosticSeverity.Trace,
            endpointId: "endpoint-01",
            byteSnapshot: bytes);
        RuntimeHostProfile profile = CreateProfile();

        ClientDiagnosticEvent result =
            RemoteRuntimeDiagnosticClientEventMapper.Map(
                source,
                profile,
                profile.ExpectedRuntimeHostId);

        Assert.Equal(ClientDiagnosticLevel.Bytes, result.Level);
        Assert.Equal(ClientDiagnosticCategory.NorthboundBytes, result.Category);
        Assert.Same(bytes, result.ByteSnapshot);
        Assert.Equal(profile.ProfileId, result.SessionContext!.ProfileId);
        Assert.Equal("7", result.Metadata["RemoteSourceSequence"]);
    }

    [Fact]
    public void Map_MismatchedRuntimeHostIdentity_ShouldReject()
    {
        var source = new RemoteRuntimeDiagnosticRecord(
            "other-host",
            1,
            DateTimeOffset.UnixEpoch,
            RemoteRuntimeDiagnosticLevel.Operational,
            RemoteRuntimeDiagnosticCategory.RuntimeConnection,
            "Connected",
            RemoteRuntimeDiagnosticSeverity.Information);
        RuntimeHostProfile profile = CreateProfile();

        Assert.Throws<InvalidDataException>(
            () => RemoteRuntimeDiagnosticClientEventMapper.Map(
                source,
                profile,
                profile.ExpectedRuntimeHostId));
    }

    private static RuntimeHostProfile CreateProfile() =>
        new(
            new RuntimeHostProfileId("profile-01"),
            "Desktop Host",
            new RemoteRuntimeHostId("host-01"),
            true);
}
