using Hase.Transport.Discovery;

namespace Hase.Transport.Tests;

public sealed class WindowsPnpEntityQueryContractTests
{
    [Fact]
    public void Query_ShouldExposeSnapshots()
    {
        // Arrange
        var expectedSnapshot =
            new WindowsPnpEntitySnapshot(
                "USB-SERIAL CH340 (COM10)",
                "wch.cn",
                @"USB\VID_1A86&PID_7523\ABC123",
                "USB-SERIAL CH340");

        IWindowsPnpEntityQuery query =
            new StubWindowsPnpEntityQuery(
                expectedSnapshot);

        // Act
        IReadOnlyList<WindowsPnpEntitySnapshot> snapshots =
            query.Query();

        // Assert
        WindowsPnpEntitySnapshot actualSnapshot =
            Assert.Single(
                snapshots);

        Assert.Same(
            expectedSnapshot,
            actualSnapshot);
    }

    [Fact]
    public void Snapshot_ShouldExposeValues()
    {
        // Act
        var snapshot =
            new WindowsPnpEntitySnapshot(
                "USB-SERIAL CH340 (COM10)",
                "wch.cn",
                @"USB\VID_1A86&PID_7523\ABC123",
                "USB-SERIAL CH340");

        // Assert
        Assert.Equal(
            "USB-SERIAL CH340 (COM10)",
            snapshot.Name);

        Assert.Equal(
            "wch.cn",
            snapshot.Manufacturer);

        Assert.Equal(
            @"USB\VID_1A86&PID_7523\ABC123",
            snapshot.PnpDeviceId);

        Assert.Equal(
            "USB-SERIAL CH340",
            snapshot.Description);
    }

    private sealed class StubWindowsPnpEntityQuery
        : IWindowsPnpEntityQuery
    {
        private readonly IReadOnlyList<
            WindowsPnpEntitySnapshot> _snapshots;

        public StubWindowsPnpEntityQuery(
            params WindowsPnpEntitySnapshot[] snapshots)
        {
            _snapshots =
                snapshots;
        }

        public IReadOnlyList<WindowsPnpEntitySnapshot> Query()
        {
            return _snapshots;
        }
    }
}