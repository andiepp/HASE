using Hase.DesktopHost.App.Hosting;

namespace Hase.DesktopHost.Tests;

public sealed class DesktopRuntimeHostSingleInstanceLeaseTests
{
    [Fact]
    public void TryAcquire_FirstLease_ShouldSucceed()
    {
        using DesktopRuntimeHostSingleInstanceLease? lease =
            DesktopRuntimeHostSingleInstanceLease.TryAcquire(
                UniqueMutexName());

        Assert.NotNull(lease);
    }

    [Fact]
    public void TryAcquire_ConcurrentLeaseOnAnotherThread_ShouldFail()
    {
        string mutexName = UniqueMutexName();
        using DesktopRuntimeHostSingleInstanceLease? first =
            DesktopRuntimeHostSingleInstanceLease.TryAcquire(
                mutexName);

        DesktopRuntimeHostSingleInstanceLease? second =
            Task.Run(
                    () => DesktopRuntimeHostSingleInstanceLease.TryAcquire(
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
        DesktopRuntimeHostSingleInstanceLease? first =
            DesktopRuntimeHostSingleInstanceLease.TryAcquire(
                mutexName);
        Assert.NotNull(first);

        first.Dispose();

        using DesktopRuntimeHostSingleInstanceLease? second =
            DesktopRuntimeHostSingleInstanceLease.TryAcquire(
                mutexName);
        Assert.NotNull(second);
    }

    [Fact]
    public void TryAcquire_DistinctNames_ShouldRemainIndependent()
    {
        using DesktopRuntimeHostSingleInstanceLease? first =
            DesktopRuntimeHostSingleInstanceLease.TryAcquire(
                UniqueMutexName());
        using DesktopRuntimeHostSingleInstanceLease? second =
            DesktopRuntimeHostSingleInstanceLease.TryAcquire(
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
            () => DesktopRuntimeHostSingleInstanceLease.TryAcquire(
                mutexName!));
    }

    private static string UniqueMutexName() =>
        $"Local\\HASE.DesktopRuntimeHost.Tests.{Guid.NewGuid():N}";
}
