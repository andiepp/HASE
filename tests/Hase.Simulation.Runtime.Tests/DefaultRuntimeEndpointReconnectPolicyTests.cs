using Hase.Runtime.Transport;

namespace Hase.Runtime.Transport.Tests;

public sealed class DefaultRuntimeEndpointReconnectPolicyTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 5)]
    [InlineData(4, 10)]
    [InlineData(5, 10)]
    [InlineData(10, 10)]
    [InlineData(int.MaxValue, 10)]
    public void GetDelay_ShouldReturnConfiguredDelay(
        int retryAttempt,
        int expectedDelaySeconds)
    {
        var policy =
            new DefaultRuntimeEndpointReconnectPolicy();

        TimeSpan delay =
            policy.GetDelay(
                retryAttempt);

        Assert.Equal(
            TimeSpan.FromSeconds(expectedDelaySeconds),
            delay);
    }

    [Fact]
    public void GetDelay_NegativeRetryAttempt_ShouldThrow()
    {
        var policy =
            new DefaultRuntimeEndpointReconnectPolicy();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => policy.GetDelay(-1));
    }

    [Fact]
    public void GetDelay_AfterMaximumIsReached_ShouldRemainAtMaximum()
    {
        var policy =
            new DefaultRuntimeEndpointReconnectPolicy();

        TimeSpan firstMaximumDelay =
            policy.GetDelay(4);

        for (int retryAttempt = 5;
             retryAttempt < 100;
             retryAttempt++)
        {
            Assert.Equal(
                firstMaximumDelay,
                policy.GetDelay(retryAttempt));
        }
    }
}
