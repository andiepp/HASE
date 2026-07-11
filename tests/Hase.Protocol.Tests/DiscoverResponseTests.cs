using Hase.Core.Domain.Identity;
using Hase.Protocol;

namespace Hase.Protocol.Tests;

public sealed class DiscoverResponseTests
{
    [Fact]
    public void Constructor_SetsProtocolProperties()
    {
        EndpointId endpointId = new("Endpoint-1");
        InstrumentId instrumentId = new("Instrument-1");

        DiscoverResponse response = new(
            new CorrelationId(17),
            endpointId,
            new[] { instrumentId });

        Assert.Equal(
            ProtocolVersion.Current,
            response.Version);

        Assert.Equal(
            ProtocolMessageRole.Response,
            response.Role);

        Assert.Equal(
            ProtocolMessageType.DiscoverResponse,
            response.MessageType);

        Assert.Equal(
            new CorrelationId(17),
            response.CorrelationId);
    }

    [Fact]
    public void Constructor_StoresEndpointId()
    {
        EndpointId endpointId = new("Endpoint-1");

        DiscoverResponse response = new(
            CorrelationId.None,
            endpointId,
            Array.Empty<InstrumentId>());

        Assert.Equal(
            endpointId,
            response.EndpointId);
    }

    [Fact]
    public void Constructor_StoresInstrumentIds()
    {
        EndpointId endpointId = new("Endpoint-1");
        InstrumentId first = new("Instrument-1");
        InstrumentId second = new("Instrument-2");

        DiscoverResponse response = new(
            CorrelationId.None,
            endpointId,
            new[] { first, second });

        Assert.Collection(
            response.InstrumentIds,
            id => Assert.Equal(first, id),
            id => Assert.Equal(second, id));
    }

    [Fact]
    public void Constructor_StoresInstrumentCollectionReference()
    {
        EndpointId endpointId = new("Endpoint-1");
        InstrumentId first = new("Instrument-1");
        InstrumentId second = new("Instrument-2");

        IReadOnlyList<InstrumentId> instrumentIds =
            new[] { first, second };

        DiscoverResponse response = new(
            new CorrelationId(1),
            endpointId,
            instrumentIds);

        Assert.Same(
            instrumentIds,
            response.InstrumentIds);
    }
}