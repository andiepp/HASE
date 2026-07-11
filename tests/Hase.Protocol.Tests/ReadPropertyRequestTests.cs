using Hase.Core.Domain.Identity;
using Hase.Protocol;

namespace Hase.Protocol.Tests;

public sealed class ReadPropertyRequestTests
{
    [Fact]
    public void Constructor_SetsProtocolProperties()
    {
        InstrumentId instrumentId = new("Instrument-1");
        PropertyId propertyId = new("Property-1");

        ReadPropertyRequest request = new(
            new CorrelationId(17),
            instrumentId,
            propertyId);

        Assert.Equal(
            ProtocolVersion.Current,
            request.Version);

        Assert.Equal(
            ProtocolMessageRole.Request,
            request.Role);

        Assert.Equal(
            ProtocolMessageType.ReadPropertyRequest,
            request.MessageType);

        Assert.Equal(
            new CorrelationId(17),
            request.CorrelationId);
    }

    [Fact]
    public void Constructor_StoresTargetIds()
    {
        InstrumentId instrumentId = new("Instrument-1");
        PropertyId propertyId = new("Property-1");

        ReadPropertyRequest request = new(
            CorrelationId.None,
            instrumentId,
            propertyId);

        Assert.Equal(instrumentId, request.InstrumentId);
        Assert.Equal(propertyId, request.PropertyId);
    }

    [Fact]
    public void EqualRequests_AreEqual()
    {
        InstrumentId instrumentId = new("Instrument-1");
        PropertyId propertyId = new("Property-1");

        ReadPropertyRequest first = new(
            new CorrelationId(5),
            instrumentId,
            propertyId);

        ReadPropertyRequest second = new(
            new CorrelationId(5),
            instrumentId,
            propertyId);

        Assert.Equal(first, second);
    }
}