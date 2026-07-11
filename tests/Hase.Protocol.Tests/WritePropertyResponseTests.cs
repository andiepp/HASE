using Hase.Core.Domain.Properties;
using Hase.Protocol;

namespace Hase.Protocol.Tests;

public sealed class WritePropertyResponseTests
{
    [Fact]
    public void SuccessfulResponse_SetsProtocolProperties()
    {
        PropertyValue propertyValue = new(
            23.5,
            new DateTimeOffset(
                2026,
                7,
                11,
                10,
                0,
                0,
                TimeSpan.Zero));

        WritePropertyResponse response = new(
            new CorrelationId(17),
            ProtocolResult.Success,
            propertyValue);

        Assert.Equal(
            ProtocolVersion.Current,
            response.Version);

        Assert.Equal(
            ProtocolMessageRole.Response,
            response.Role);

        Assert.Equal(
            ProtocolMessageType.WritePropertyResponse,
            response.MessageType);

        Assert.Equal(
            new CorrelationId(17),
            response.CorrelationId);
    }

    [Fact]
    public void SuccessfulResponse_StoresResult()
    {
        PropertyValue propertyValue = new(
            23.5,
            new DateTimeOffset(
                2026,
                7,
                11,
                10,
                0,
                0,
                TimeSpan.Zero));

        WritePropertyResponse response = new(
            CorrelationId.None,
            ProtocolResult.Success,
            propertyValue);

        Assert.Equal(
            ProtocolResult.Success,
            response.Result);
    }

    [Fact]
    public void SuccessfulResponse_StoresAuthoritativePropertyValue()
    {
        PropertyValue propertyValue = new(
            23.4,
            new DateTimeOffset(
                2026,
                7,
                11,
                10,
                0,
                0,
                TimeSpan.Zero),
            PropertyQuality.Good);

        WritePropertyResponse response = new(
            CorrelationId.None,
            ProtocolResult.Success,
            propertyValue);

        Assert.Same(
            propertyValue,
            response.PropertyValue);
    }

    [Fact]
    public void FailedResponse_CanOmitPropertyValue()
    {
        ProtocolResult result = new(
            ProtocolResultCode.Rejected,
            "Requested value was rejected.");

        WritePropertyResponse response = new(
            new CorrelationId(5),
            result,
            null);

        Assert.Equal(
            result,
            response.Result);

        Assert.Null(
            response.PropertyValue);
    }
}