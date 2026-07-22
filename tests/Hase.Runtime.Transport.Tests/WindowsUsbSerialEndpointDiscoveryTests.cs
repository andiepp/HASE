using Hase.Core.Domain.Descriptors;
using Hase.Runtime.Transport.Discovery;
using Hase.Transport.Discovery;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class WindowsUsbSerialEndpointDiscoveryTests
{
    [Fact]
    public void Create_DescriptorRepository_ShouldReturnDiscoveryService()
    {
        var repository =
            new TestEndpointDescriptorRepository();

        UsbSerialEndpointDiscoveryService service =
            WindowsUsbSerialEndpointDiscovery.Create(
                repository);

        Assert.NotNull(
            service);

        Assert.Equal(
            0,
            repository.CallCount);
    }

    [Fact]
    public void Create_CandidateFilter_ShouldReturnDiscoveryServiceWithoutEvaluatingFilter()
    {
        var repository =
            new TestEndpointDescriptorRepository();

        var filter =
            new TestCandidateFilter();

        UsbSerialEndpointDiscoveryService service =
            WindowsUsbSerialEndpointDiscovery.Create(
                repository,
                filter);

        Assert.NotNull(
            service);

        Assert.Equal(
            0,
            filter.CallCount);

        Assert.Equal(
            0,
            repository.CallCount);
    }

    [Fact]
    public void Create_NullDescriptorRepository_ShouldThrow()
    {
        void Act()
        {
            _ = WindowsUsbSerialEndpointDiscovery.Create(
                null!);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    private sealed class TestCandidateFilter
        : IUsbSerialEndpointCandidateFilter
    {
        public int CallCount
        {
            get;
            private set;
        }

        public bool IsMatch(
            UsbSerialEndpointCandidate candidate)
        {
            CallCount++;

            return true;
        }
    }

    private sealed class TestEndpointDescriptorRepository
        : IEndpointDescriptorRepository
    {
        public int CallCount
        {
            get;
            private set;
        }

        public ValueTask<EndpointDescriptorDefinition?> FindAsync(
            DescriptorReference reference,
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            return ValueTask.FromResult<
                EndpointDescriptorDefinition?>(
                    null);
        }
    }
}