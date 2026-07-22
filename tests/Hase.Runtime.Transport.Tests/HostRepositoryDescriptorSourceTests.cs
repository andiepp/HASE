using Hase.Runtime.Transport.Attachment;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class HostRepositoryDescriptorSourceTests
{
    [Fact]
    public void Instance_ShouldImplementDescriptorSourceContract()
    {
        // Act
        HostRepositoryDescriptorSource source =
            HostRepositoryDescriptorSource.Instance;

        // Assert
        Assert.IsAssignableFrom<IEndpointDescriptorSource>(
            source);
    }

    [Fact]
    public void Instance_ShouldReturnSharedInstance()
    {
        // Act
        HostRepositoryDescriptorSource first =
            HostRepositoryDescriptorSource.Instance;

        HostRepositoryDescriptorSource second =
            HostRepositoryDescriptorSource.Instance;

        // Assert
        Assert.Same(
            first,
            second);
    }
}