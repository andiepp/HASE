using Hase.Protocol;

namespace Hase.Protocol.Tests;

public sealed class ProtocolResultResponseTests
{
    private sealed record TestResultResponse(
        ProtocolVersion Version,
        ProtocolMessageType MessageType,
        CorrelationId CorrelationId,
        ProtocolResult Result)
        : ProtocolResultResponse(
            Version,
            MessageType,
            CorrelationId,
            Result);

    [Fact]
    public void Constructor_SetsResponseRole()
    {
        TestResultResponse response = new(
            ProtocolVersion.Current,
            ProtocolMessageType.ReadPropertyResponse,
            new CorrelationId(17),
            ProtocolResult.Success);

        Assert.Equal(
            ProtocolMessageRole.Response,
            response.Role);
    }

    [Fact]
    public void Constructor_SetsProtocolProperties()
    {
        ProtocolResult result = new(
            ProtocolResultCode.NotFound,
            "Property not found.");

        TestResultResponse response = new(
            new ProtocolVersion(2, 3),
            ProtocolMessageType.ReadPropertyResponse,
            new CorrelationId(42),
            result);

        Assert.Equal(
            new ProtocolVersion(2, 3),
            response.Version);

        Assert.Equal(
            ProtocolMessageType.ReadPropertyResponse,
            response.MessageType);

        Assert.Equal(
            new CorrelationId(42),
            response.CorrelationId);

        Assert.Equal(
            result,
            response.Result);
    }

    [Fact]
    public void ResponsesWithSameValues_AreEqual()
    {
        ProtocolResult result = new(
            ProtocolResultCode.Rejected,
            "Request rejected.");

        TestResultResponse first = new(
            ProtocolVersion.Current,
            ProtocolMessageType.WritePropertyResponse,
            new CorrelationId(5),
            result);

        TestResultResponse second = new(
            ProtocolVersion.Current,
            ProtocolMessageType.WritePropertyResponse,
            new CorrelationId(5),
            result);

        Assert.Equal(first, second);
    }
}