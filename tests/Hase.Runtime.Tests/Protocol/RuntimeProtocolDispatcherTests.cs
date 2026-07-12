using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Protocol;
using Hase.Runtime.Protocol;
using Hase.Runtime.Runtime;

namespace Hase.Runtime.Tests.Protocol;

public class RuntimeProtocolDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_ShouldReturnDiscoverResponse()
    {
        // Arrange
        var context = new RuntimeContext();

        var endpointDescriptor = new EndpointDescriptor(
            new EndpointId("Endpoint1"));

        RuntimeEndpoint endpoint =
            context.AddEndpoint(endpointDescriptor);

        var dispatcher =
            new RuntimeProtocolDispatcher(endpoint);

        var request =
            new DiscoverRequest(CorrelationId.None);

        // Act
        DiscoverResponse response =
            await dispatcher.DispatchAsync(request);

        // Assert
        Assert.Equal(
            request.CorrelationId,
            response.CorrelationId);

        Assert.Equal(
            endpointDescriptor.Id,
            response.EndpointId);

        Assert.Empty(response.InstrumentIds);
    }
}
