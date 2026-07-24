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

    [Fact]
    public void SharedResourcesContract_ShouldExposePropertyOperations()
    {
        Type? propertyType =
            typeof(IEndpointOperationalResources)
                .GetProperty(
                    nameof(
                        IEndpointOperationalResources.PropertyOperations))
                ?.PropertyType;

        Assert.Equal(
            typeof(IEndpointAttachmentPropertyOperations),
            propertyType);
    }

    [Fact]
    public async Task DefaultPropertyOperations_ShouldReturnUnavailable()
    {
        IEndpointOperationalResources resources =
            new TestOperationalResources();

        EndpointAttachmentPropertyOperationResult result =
            await resources.PropertyOperations.ReadAsync(
                new Hase.Core.Domain.Identity.InstrumentId(
                    "test-instrument"),
                new Hase.Core.Domain.Identity.PropertyId(
                    "test-property"));

        Assert.Equal(
            EndpointAttachmentPropertyOperationStatus.Unavailable,
            result.Status);
    }

    private sealed class TestOperationalResources
        : IEndpointOperationalResources
    {
        public EndpointConnectionSupervisionLifetime SupervisionLifetime =>
            throw new NotSupportedException();

        public IReadOnlyList<IAsyncDisposable> ResourcesAfterSupervision =>
            [];
    }
}