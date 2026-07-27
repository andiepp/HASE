using Hase.Client;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;

namespace Hase.Client.Tests;

public sealed class RemoteRuntimeHostSnapshotTests
{
    [Fact]
    public void Constructor_Values_ShouldPreserveImmutableSnapshot()
    {
        var runtimeHostId =
            new RemoteRuntimeHostId(
                "host-01");
        var apiVersion =
            new RuntimeHostClientApiVersion(
                1,
                0);
        var attachment =
            CreateAttachment(
                "endpoint-01",
                "0a11d9d4-7a02-43be-ae3f-eef9d11e0de8");
        var source =
            new List<RemoteEndpointAttachmentSnapshot>
            {
                attachment
            };

        var snapshot =
            new RemoteRuntimeHostSnapshot(
                runtimeHostId,
                apiVersion,
                source);

        source.Clear();

        Assert.Same(
            runtimeHostId,
            snapshot.RuntimeHostId);
        Assert.Equal(
            apiVersion,
            snapshot.ApiVersion);
        Assert.Equal(
            new[]
            {
                attachment
            },
            snapshot.Attachments);
    }

    [Fact]
    public void Constructor_EmptyAttachments_ShouldSucceed()
    {
        var snapshot =
            new RemoteRuntimeHostSnapshot(
                new RemoteRuntimeHostId(
                    "host-01"),
                RuntimeHostClientApiVersion.Current,
                []);

        Assert.Empty(
            snapshot.Attachments);
    }

    [Fact]
    public void Attachments_IsReadOnly()
    {
        var snapshot =
            new RemoteRuntimeHostSnapshot(
                new RemoteRuntimeHostId(
                    "host-01"),
                RuntimeHostClientApiVersion.Current,
                [
                    CreateAttachment(
                        "endpoint-01",
                        "0a11d9d4-7a02-43be-ae3f-eef9d11e0de8")
                ]);

        Assert.IsAssignableFrom<IReadOnlyList<RemoteEndpointAttachmentSnapshot>>(
            snapshot.Attachments);
        Assert.Throws<NotSupportedException>(
            () =>
                ((IList<RemoteEndpointAttachmentSnapshot>)
                    snapshot.Attachments)
                    .Add(
                        CreateAttachment(
                            "endpoint-02",
                            "f64f6262-f154-4399-8c32-4bf15a1133af")));
    }

    [Fact]
    public void Constructor_NullRuntimeHostId_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "runtimeHostId",
            () => new RemoteRuntimeHostSnapshot(
                null!,
                RuntimeHostClientApiVersion.Current,
                []));
    }

    [Fact]
    public void Constructor_DefaultApiVersion_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "apiVersion",
            () => new RemoteRuntimeHostSnapshot(
                new RemoteRuntimeHostId(
                    "host-01"),
                default,
                []));
    }

    [Fact]
    public void Constructor_NullAttachments_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "attachments",
            () => new RemoteRuntimeHostSnapshot(
                new RemoteRuntimeHostId(
                    "host-01"),
                RuntimeHostClientApiVersion.Current,
                null!));
    }

    [Fact]
    public void Constructor_NullAttachment_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "attachments",
            () => new RemoteRuntimeHostSnapshot(
                new RemoteRuntimeHostId(
                    "host-01"),
                RuntimeHostClientApiVersion.Current,
                [
                    null!
                ]));
    }

    [Fact]
    public void Constructor_DuplicateEndpointIdentity_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "attachments",
            () => new RemoteRuntimeHostSnapshot(
                new RemoteRuntimeHostId(
                    "host-01"),
                RuntimeHostClientApiVersion.Current,
                [
                    CreateAttachment(
                        "endpoint-01",
                        "0a11d9d4-7a02-43be-ae3f-eef9d11e0de8"),
                    CreateAttachment(
                        "endpoint-01",
                        "f64f6262-f154-4399-8c32-4bf15a1133af")
                ]));
    }

    private static RemoteEndpointAttachmentSnapshot CreateAttachment(
        string endpointId,
        string generation)
    {
        return new RemoteEndpointAttachmentSnapshot(
            new RemoteEndpointAttachmentGeneration(
                Guid.Parse(
                    generation)),
            new EndpointDescriptor(
                new EndpointId(
                    endpointId)),
            new RemoteEndpointConnectionStatus(
                RemoteEndpointConnectionState.Ready));
    }
}
