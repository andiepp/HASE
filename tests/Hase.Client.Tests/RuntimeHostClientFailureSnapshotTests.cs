using Hase.Client;

namespace Hase.Client.Tests;

public sealed class RuntimeHostClientFailureSnapshotTests
{
    [Fact]
    public void Constructor_Values_ShouldNormalizeMessage()
    {
        var failure = new RuntimeHostClientFailureSnapshot(
            RuntimeHostClientFailureCategory.TransportUnavailable,
            " unavailable ");

        Assert.Equal(RuntimeHostClientFailureCategory.TransportUnavailable, failure.Category);
        Assert.Equal("unavailable", failure.Message);
    }

    [Fact]
    public void Constructor_UnspecifiedCategory_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            "category",
            () => new RuntimeHostClientFailureSnapshot(
                RuntimeHostClientFailureCategory.Unspecified,
                "failure"));
    }

    [Fact]
    public void Constructor_EmptyMessage_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "message",
            () => new RuntimeHostClientFailureSnapshot(
                RuntimeHostClientFailureCategory.Unknown,
                " "));
    }
}
