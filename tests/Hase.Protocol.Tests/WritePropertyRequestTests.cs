using Hase.Core.Domain.Identity;
using Hase.Protocol;

namespace Hase.Protocol.Tests;

public sealed class WritePropertyRequestTests
{
    [Fact]
    public void Constructor_SetsProtocolProperties()
    {
        InstrumentId instrumentId = new("Instrument-1");
        PropertyId propertyId = new("Property-1");

        WritePropertyRequest request = new(
            new CorrelationId(17),
            instrumentId,
            propertyId,
            23.5);

        Assert.Equal(
            ProtocolVersion.Current,
            request.Version);

        Assert.Equal(
            ProtocolMessageRole.Request,
            request.Role);

        Assert.Equal(
            ProtocolMessageType.WritePropertyRequest,
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

        WritePropertyRequest request = new(
            CorrelationId.None,
            instrumentId,
            propertyId,
            23.5);

        Assert.Equal(
            instrumentId,
            request.InstrumentId);

        Assert.Equal(
            propertyId,
            request.PropertyId);
    }

    [Fact]
    public void Constructor_StoresRequestedValue()
    {
        WritePropertyRequest request = new(
            CorrelationId.None,
            new InstrumentId("Instrument-1"),
            new PropertyId("Property-1"),
            23.5);

        Assert.Equal(
            23.5,
            request.Value);
    }

    [Fact]
    public void Constructor_AllowsNullValue()
    {
        WritePropertyRequest request = new(
            CorrelationId.None,
            new InstrumentId("Instrument-1"),
            new PropertyId("Property-1"),
            null);

        Assert.Null(
            request.Value);
    }

    [Fact]
    public void EqualRequestsWithSameValues_AreEqual()
    {
        WritePropertyRequest first = new(
            new CorrelationId(5),
            new InstrumentId("Instrument-1"),
            new PropertyId("Property-1"),
            23.5);

        WritePropertyRequest second = new(
            new CorrelationId(5),
            new InstrumentId("Instrument-1"),
            new PropertyId("Property-1"),
            23.5);

        Assert.Equal(first, second);
    }
}
