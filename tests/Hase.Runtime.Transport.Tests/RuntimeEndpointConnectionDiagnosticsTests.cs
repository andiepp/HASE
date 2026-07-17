using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class RuntimeEndpointConnectionDiagnosticsTests
{
    [Fact]
    public void Empty_ShouldContainEmptySnapshots()
    {
        // Act
        RuntimeEndpointConnectionDiagnostics diagnostics =
            RuntimeEndpointConnectionDiagnostics.Empty;

        // Assert
        Assert.Equal(
            new TransportConnectionHealthSnapshot(
                hasConnection:
                    false,
                state:
                    null,
                lastStateChangeUtc:
                    null,
                replacementCount:
                    0),
            diagnostics.TransportHealth);

        Assert.Equal(
            RuntimeEndpointConnectionStatistics.Empty,
            diagnostics.ConnectionStatistics);

        Assert.Equal(
            TransportExchangeStatistics.Empty,
            diagnostics.ExchangeStatistics);
    }

    [Fact]
    public void Constructor_ValidSnapshots_ShouldPreserveSnapshots()
    {
        // Arrange
        DateTimeOffset stateChangedAtUtc =
            DateTimeOffset.UnixEpoch.AddMinutes(
                1);

        DateTimeOffset recoveryStartedAtUtc =
            DateTimeOffset.UnixEpoch.AddMinutes(
                2);

        DateTimeOffset recoveryCompletedAtUtc =
            DateTimeOffset.UnixEpoch.AddMinutes(
                3);

        DateTimeOffset exchangeCompletedAtUtc =
            DateTimeOffset.UnixEpoch.AddMinutes(
                4);

        var transportHealth =
            new TransportConnectionHealthSnapshot(
                hasConnection:
                    true,
                state:
                    TransportConnectionState.Connected,
                lastStateChangeUtc:
                    stateChangedAtUtc,
                replacementCount:
                    2);

        var connectionStatistics =
            new RuntimeEndpointConnectionStatistics(
                initialConnectionAttemptCount:
                    3,
                initialConnectionFailureCount:
                    2,
                reconnectAttemptCount:
                    4,
                reconnectFailureCount:
                    2,
                successfulRecoveryCount:
                    2,
                lastRecoveryStartedAtUtc:
                    recoveryStartedAtUtc,
                lastRecoveryCompletedAtUtc:
                    recoveryCompletedAtUtc,
                lastRecoveryDuration:
                    TimeSpan.FromSeconds(
                        5));

        var exchangeStatistics =
            new TransportExchangeStatistics(
                completedExchangeCount:
                    5,
                successfulExchangeCount:
                    3,
                failedExchangeCount:
                    1,
                cancelledExchangeCount:
                    1,
                totalRequestByteCount:
                    256,
                totalResponseByteCount:
                    2048,
                totalDuration:
                    TimeSpan.FromMilliseconds(
                        450),
                lastCompletedAtUtc:
                    exchangeCompletedAtUtc,
                lastOutcome:
                    TransportExchangeOutcome.Cancelled);

        // Act
        var diagnostics =
            new RuntimeEndpointConnectionDiagnostics(
                transportHealth:
                    transportHealth,
                connectionStatistics:
                    connectionStatistics,
                exchangeStatistics:
                    exchangeStatistics);

        // Assert
        Assert.Same(
            transportHealth,
            diagnostics.TransportHealth);

        Assert.Same(
            connectionStatistics,
            diagnostics.ConnectionStatistics);

        Assert.Same(
            exchangeStatistics,
            diagnostics.ExchangeStatistics);
    }

    [Theory]
    [InlineData("transportHealth")]
    [InlineData("connectionStatistics")]
    [InlineData("exchangeStatistics")]
    public void Constructor_NullSnapshot_ShouldThrow(
        string nullParameterName)
    {
        // Arrange
        var transportHealth =
            new TransportConnectionHealthSnapshot(
                hasConnection:
                    false,
                state:
                    null,
                lastStateChangeUtc:
                    null,
                replacementCount:
                    0);

        RuntimeEndpointConnectionStatistics connectionStatistics =
            RuntimeEndpointConnectionStatistics.Empty;

        TransportExchangeStatistics exchangeStatistics =
            TransportExchangeStatistics.Empty;

        // Act
        void Act()
        {
            _ = new RuntimeEndpointConnectionDiagnostics(
                transportHealth:
                    nullParameterName == "transportHealth"
                        ? null!
                        : transportHealth,
                connectionStatistics:
                    nullParameterName == "connectionStatistics"
                        ? null!
                        : connectionStatistics,
                exchangeStatistics:
                    nullParameterName == "exchangeStatistics"
                        ? null!
                        : exchangeStatistics);
        }

        // Assert
        ArgumentNullException exception =
            Assert.Throws<
                ArgumentNullException>(
                    Act);

        Assert.Equal(
            nullParameterName,
            exception.ParamName);
    }
}