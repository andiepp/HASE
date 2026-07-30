using Hase.Runtime.Diagnostics;

namespace Hase.DesktopHost.Tests;

public sealed class DesktopRuntimeDiagnosticSessionTests
{
    [Fact]
    public void Constructor_DefaultsToOperational()
    {
        var session =
            new DesktopRuntimeDiagnosticSession();

        Assert.Equal(
            RuntimeDiagnosticLevel.Operational,
            session.MaximumLevel);
        Assert.True(
            session.Publisher.IsEnabled(
                RuntimeDiagnosticLevel.Operational));
        Assert.False(
            session.Publisher.IsEnabled(
                RuntimeDiagnosticLevel.Protocol));
        Assert.False(
            session.Publisher.IsEnabled(
                RuntimeDiagnosticLevel.Bytes));
    }

    [Theory]
    [InlineData(RuntimeDiagnosticLevel.Protocol, false)]
    [InlineData(RuntimeDiagnosticLevel.Bytes, true)]
    public void Constructor_ConfiguresCumulativeMaximumLevel(
        RuntimeDiagnosticLevel maximumLevel,
        bool bytesEnabled)
    {
        var session =
            new DesktopRuntimeDiagnosticSession(
                maximumLevel);

        Assert.True(
            session.Publisher.IsEnabled(
                RuntimeDiagnosticLevel.Operational));
        Assert.True(
            session.Publisher.IsEnabled(
                RuntimeDiagnosticLevel.Protocol));
        Assert.Equal(
            bytesEnabled,
            session.Publisher.IsEnabled(
                RuntimeDiagnosticLevel.Bytes));
    }

    [Fact]
    public void CaptureDiagnostics_RetainsOnlyConfiguredCapacityInOrder()
    {
        var session =
            new DesktopRuntimeDiagnosticSession(
                RuntimeDiagnosticLevel.Operational,
                capacity: 2);

        Publish(
            session,
            "one");
        Publish(
            session,
            "two");
        Publish(
            session,
            "three");

        Assert.Equal(
            [
                "two",
                "three"
            ],
            session
                .CaptureDiagnostics()
                .Select(
                    record =>
                        record.EventName)
                .ToArray());
    }

    [Fact]
    public void ClearDiagnostics_RemovesRetainedRecords()
    {
        var session =
            new DesktopRuntimeDiagnosticSession();

        Publish(
            session,
            "one");

        session.ClearDiagnostics();

        Assert.Empty(
            session.CaptureDiagnostics());
    }

    [Fact]
    public void NewSession_DoesNotRetainPreviousSessionRecords()
    {
        var previous =
            new DesktopRuntimeDiagnosticSession();

        Publish(
            previous,
            "previous");

        var replacement =
            new DesktopRuntimeDiagnosticSession();

        Publish(
            replacement,
            "replacement");

        Assert.Equal(
            "previous",
            Assert.Single(
                previous.CaptureDiagnostics()).EventName);
        Assert.Equal(
            "replacement",
            Assert.Single(
                replacement.CaptureDiagnostics()).EventName);
    }

    private static void Publish(
        DesktopRuntimeDiagnosticSession session,
        string eventName)
    {
        session.Publisher.Publish(
            new RuntimeDiagnosticEvent(
                RuntimeDiagnosticLevel.Operational,
                RuntimeDiagnosticCategory.RuntimeConnection,
                eventName));
    }
}
