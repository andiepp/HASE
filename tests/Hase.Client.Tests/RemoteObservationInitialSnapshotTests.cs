using Hase.Client;

namespace Hase.Client.Tests;

public sealed class RemoteObservationInitialSnapshotTests
{
    [Fact]
    public void Constructor_Values_ShouldPreserveBoundary()
    {
        var snapshot =
            new RemoteRuntimeHostSnapshot(
                new RemoteRuntimeHostId(
                    "host-01"),
                RuntimeHostClientApiVersion.Current,
                []);
        var sequence =
            new RemoteObservationSequence(
                42);

        var initialSnapshot =
            new RemoteObservationInitialSnapshot(
                snapshot,
                sequence);

        Assert.Same(
            snapshot,
            initialSnapshot.Snapshot);
        Assert.Same(
            sequence,
            initialSnapshot.SnapshotSequence);
    }

    [Fact]
    public void Constructor_NullSnapshot_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "snapshot",
            () => new RemoteObservationInitialSnapshot(
                null!,
                new RemoteObservationSequence(
                    0)));
    }

    [Fact]
    public void Constructor_NullSequence_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "snapshotSequence",
            () => new RemoteObservationInitialSnapshot(
                new RemoteRuntimeHostSnapshot(
                    new RemoteRuntimeHostId(
                        "host-01"),
                    RuntimeHostClientApiVersion.Current,
                    []),
                null!));
    }
}
