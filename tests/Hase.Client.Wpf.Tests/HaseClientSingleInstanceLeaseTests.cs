using Hase.Client.Wpf.AppHost.Hosting;

namespace Hase.Client.Wpf.Tests;

public sealed class HaseClientSingleInstanceLeaseTests
{
    [Fact]
    public void TryAcquire_FirstLease_ShouldSucceed()
    {
        using HaseClientSingleInstanceLease? lease =
            HaseClientSingleInstanceLease.TryAcquire(
                UniqueMutexName());

        Assert.NotNull(lease);
    }

    [Fact]
    public void TryAcquire_ConcurrentLeaseOnAnotherThread_ShouldFail()
    {
        string mutexName = UniqueMutexName();
        using HaseClientSingleInstanceLease? first =
            HaseClientSingleInstanceLease.TryAcquire(
                mutexName);

        HaseClientSingleInstanceLease? second =
            Task.Run(
                    () => HaseClientSingleInstanceLease.TryAcquire(
                        mutexName))
                .GetAwaiter()
                .GetResult();

        Assert.NotNull(first);
        Assert.Null(second);
    }

    [Fact]
    public void Dispose_ShouldAllowAcquisitionAgain()
    {
        string mutexName = UniqueMutexName();
        HaseClientSingleInstanceLease? first =
            HaseClientSingleInstanceLease.TryAcquire(
                mutexName);
        Assert.NotNull(first);

        first.Dispose();

        using HaseClientSingleInstanceLease? second =
            HaseClientSingleInstanceLease.TryAcquire(
                mutexName);
        Assert.NotNull(second);
    }

    [Fact]
    public void TryAcquire_DistinctNames_ShouldRemainIndependent()
    {
        using HaseClientSingleInstanceLease? first =
            HaseClientSingleInstanceLease.TryAcquire(
                UniqueMutexName());
        using HaseClientSingleInstanceLease? second =
            HaseClientSingleInstanceLease.TryAcquire(
                UniqueMutexName());

        Assert.NotNull(first);
        Assert.NotNull(second);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void TryAcquire_InvalidName_ShouldReject(string? mutexName)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => HaseClientSingleInstanceLease.TryAcquire(
                mutexName!));
    }

    private static string UniqueMutexName() =>
        $"Local\\HASE.Client.Tests.{Guid.NewGuid():N}";
}
