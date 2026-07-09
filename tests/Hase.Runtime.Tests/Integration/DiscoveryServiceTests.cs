using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Runtime;
using Hase.Runtime.Services;

namespace Hase.Runtime.Tests.Integration;

public class DiscoveryServiceTests
{
    [Fact]
    public async Task DiscoverAsync_ShouldAddEndpointToRuntimeContext()
    {
        // Arrange
        var context = new RuntimeContext();
        var service = new FakeDiscoveryService();

        // Act
        await service.DiscoverAsync(context);

        // Assert
        Assert.Single(context.Endpoints);
        Assert.Equal(
            new EndpointId("Endpoint1"),
            context.Endpoints[0].Descriptor.Id);
    }

    private sealed class FakeDiscoveryService : IDiscoveryService
    {
        public Task DiscoverAsync(
            RuntimeContext context,
            CancellationToken cancellationToken = default)
        {
            var descriptor = new EndpointDescriptor(
                new EndpointId("Endpoint1"));

            context.AddEndpoint(descriptor);

            return Task.CompletedTask;
        }
    }
}