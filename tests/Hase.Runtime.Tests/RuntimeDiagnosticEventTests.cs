using Hase.Runtime.Diagnostics;

namespace Hase.Runtime.Tests;

public sealed class RuntimeDiagnosticEventTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Constructor_EmptyEventName_ThrowsArgumentException(
        string eventName)
    {
        Assert.Throws<ArgumentException>(
            () => new RuntimeDiagnosticEvent(
                RuntimeDiagnosticLevel.Operational,
                RuntimeDiagnosticCategory.RuntimeConnection,
                eventName));
    }

    [Fact]
    public void Constructor_NegativeDuration_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new RuntimeDiagnosticEvent(
                RuntimeDiagnosticLevel.Operational,
                RuntimeDiagnosticCategory.RuntimeConnection,
                "ConnectionFailed",
                duration: TimeSpan.FromTicks(-1)));
    }

    [Fact]
    public void Constructor_ValidValues_NormalizesAndCopiesInput()
    {
        Dictionary<string, string> details =
            new()
            {
                ["  State  "] = "Ready"
            };

        RuntimeDiagnosticEvent diagnosticEvent =
            new(
                RuntimeDiagnosticLevel.Operational,
                RuntimeDiagnosticCategory.RuntimeConnection,
                "  StateChanged  ",
                RuntimeDiagnosticSeverity.Information,
                "  endpoint-01  ",
                Guid.NewGuid(),
                duration: TimeSpan.FromMilliseconds(12),
                outcome: RuntimeDiagnosticOutcome.Succeeded,
                details: details);

        details["State"] =
            "Disconnected";

        Assert.Equal(
            "StateChanged",
            diagnosticEvent.EventName);

        Assert.Equal(
            "endpoint-01",
            diagnosticEvent.EndpointId);

        Assert.Equal(
            "Ready",
            diagnosticEvent.Details["State"]);
    }
}
