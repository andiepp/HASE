using Hase.Runtime.Transport.Attachment;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class EndpointOperationalResourcesContractTests
{
    [Fact]
    public void NativeResourcesContract_ShouldUseSharedOwnershipContract()
    {
        // Assert
        Assert.True(
            typeof(IEndpointOperationalResources)
                .IsAssignableFrom(
                    typeof(INativeEndpointOperationalResources)));
    }

    [Fact]
    public void CompactResourcesContract_ShouldUseSharedOwnershipContract()
    {
        // Assert
        Assert.True(
            typeof(IEndpointOperationalResources)
                .IsAssignableFrom(
                    typeof(ICompactEndpointOperationalResources)));
    }
}