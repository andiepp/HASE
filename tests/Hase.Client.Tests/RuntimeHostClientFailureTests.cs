using Hase.Client;

namespace Hase.Client.Tests;

public sealed class RuntimeHostClientFailureTests
{
    [Theory]
    [InlineData(RuntimeHostClientFailureCategory.Authentication)]
    [InlineData(RuntimeHostClientFailureCategory.Authorization)]
    [InlineData(RuntimeHostClientFailureCategory.ApiCompatibility)]
    [InlineData(RuntimeHostClientFailureCategory.TransportUnavailable)]
    [InlineData(RuntimeHostClientFailureCategory.DeadlineExceeded)]
    [InlineData(RuntimeHostClientFailureCategory.Cancelled)]
    [InlineData(RuntimeHostClientFailureCategory.ObservationGap)]
    [InlineData(RuntimeHostClientFailureCategory.InvalidRemoteContract)]
    [InlineData(RuntimeHostClientFailureCategory.LocalConfiguration)]
    [InlineData(RuntimeHostClientFailureCategory.Unknown)]
    public void Exception_SpecifiedCategory_ShouldPreserveFailure(
        RuntimeHostClientFailureCategory category)
    {
        var inner =
            new IOException(
                "inner");

        var exception =
            new RuntimeHostClientException(
                category,
                " Safe failure ",
                inner);

        Assert.Equal(
            category,
            exception.Category);
        Assert.Equal(
            "Safe failure",
            exception.Message);
        Assert.Same(
            inner,
            exception.InnerException);
    }

    [Fact]
    public void Exception_UnspecifiedCategory_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            "category",
            () => new RuntimeHostClientException(
                RuntimeHostClientFailureCategory.Unspecified,
                "Failure"));
    }

    [Fact]
    public void Exception_EmptyMessage_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "message",
            () => new RuntimeHostClientException(
                RuntimeHostClientFailureCategory.Unknown,
                " "));
    }

    [Fact]
    public void RecoveryPolicy_Conservative_ShouldExposeFiniteSchedule()
    {
        Assert.Equal(
            new TimeSpan[]
            {
                TimeSpan.Zero,
                TimeSpan.FromSeconds(
                    1),
                TimeSpan.FromSeconds(
                    2),
                TimeSpan.FromSeconds(
                    5),
                TimeSpan.FromSeconds(
                    10)
            },
            RuntimeHostClientRecoveryPolicy.Conservative.Delays);
    }

    [Theory]
    [InlineData(RuntimeHostClientFailureCategory.TransportUnavailable)]
    [InlineData(RuntimeHostClientFailureCategory.ObservationGap)]
    public void RecoveryPolicy_RecoverableFailure_ShouldReturnDelay(
        RuntimeHostClientFailureCategory category)
    {
        var policy =
            new RuntimeHostClientRecoveryPolicy(
                [
                    TimeSpan.FromSeconds(
                        3)
                ]);

        bool result =
            policy.TryGetDelay(
                category,
                0,
                out TimeSpan delay);

        Assert.True(
            result);
        Assert.Equal(
            TimeSpan.FromSeconds(
                3),
            delay);
    }

    [Theory]
    [InlineData(RuntimeHostClientFailureCategory.Authentication)]
    [InlineData(RuntimeHostClientFailureCategory.Authorization)]
    [InlineData(RuntimeHostClientFailureCategory.ApiCompatibility)]
    [InlineData(RuntimeHostClientFailureCategory.DeadlineExceeded)]
    [InlineData(RuntimeHostClientFailureCategory.Cancelled)]
    [InlineData(RuntimeHostClientFailureCategory.InvalidRemoteContract)]
    [InlineData(RuntimeHostClientFailureCategory.LocalConfiguration)]
    [InlineData(RuntimeHostClientFailureCategory.Unknown)]
    public void RecoveryPolicy_NonrecoverableFailure_ShouldReject(
        RuntimeHostClientFailureCategory category)
    {
        bool result =
            RuntimeHostClientRecoveryPolicy.Conservative.TryGetDelay(
                category,
                0,
                out _);

        Assert.False(
            result);
    }

    [Fact]
    public void RecoveryPolicy_ExhaustedSchedule_ShouldReject()
    {
        var policy =
            new RuntimeHostClientRecoveryPolicy(
                [TimeSpan.Zero]);

        Assert.False(
            policy.TryGetDelay(
                RuntimeHostClientFailureCategory.TransportUnavailable,
                1,
                out _));
    }

    [Fact]
    public void RecoveryPolicy_NegativeDelay_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            "delays",
            () => new RuntimeHostClientRecoveryPolicy(
                [
                    TimeSpan.FromSeconds(
                        -1)
                ]));
    }

    [Fact]
    public void RecoveryPolicy_NegativeAttempt_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            "attemptIndex",
            () => RuntimeHostClientRecoveryPolicy.Conservative.TryGetDelay(
                RuntimeHostClientFailureCategory.TransportUnavailable,
                -1,
                out _));
    }
}
