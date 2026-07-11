using Hase.Protocol;

namespace Hase.Protocol.Tests;

public sealed class ExecuteCommandResponseTests
{
    [Fact]
    public void SuccessfulResponse_SetsProtocolProperties()
    {
        ExecuteCommandResponse response = new(
            new CorrelationId(17),
            ProtocolResult.Success,
            null);

        Assert.Equal(
            ProtocolVersion.Current,
            response.Version);

        Assert.Equal(
            ProtocolMessageRole.Response,
            response.Role);

        Assert.Equal(
            ProtocolMessageType.ExecuteCommandResponse,
            response.MessageType);

        Assert.Equal(
            new CorrelationId(17),
            response.CorrelationId);
    }

    [Fact]
    public void SuccessfulResponse_CanOmitReturnValue()
    {
        ExecuteCommandResponse response = new(
            CorrelationId.None,
            ProtocolResult.Success,
            null);

        Assert.Equal(
            ProtocolResult.Success,
            response.Result);

        Assert.Null(
            response.ReturnValue);
    }

    [Fact]
    public void SuccessfulResponse_StoresReturnValue()
    {
        ExecuteCommandResponse response = new(
            CorrelationId.None,
            ProtocolResult.Success,
            42);

        Assert.Equal(
            42,
            response.ReturnValue);
    }

    [Fact]
    public void FailedResponse_CanOmitReturnValue()
    {
        ProtocolResult result = new(
            ProtocolResultCode.Rejected,
            "Command execution was rejected.");

        ExecuteCommandResponse response = new(
            new CorrelationId(5),
            result,
            null);

        Assert.Equal(
            result,
            response.Result);

        Assert.Null(
            response.ReturnValue);
    }
}