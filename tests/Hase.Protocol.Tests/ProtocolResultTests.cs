using Hase.Protocol;

namespace Hase.Protocol.Tests;

public sealed class ProtocolResultTests
{
    [Fact]
    public void Constructor_SetsCodeAndMessage()
    {
        ProtocolResult result = new(
            ProtocolResultCode.NotFound,
            "Property was not found.");

        Assert.Equal(
            ProtocolResultCode.NotFound,
            result.Code);

        Assert.Equal(
            "Property was not found.",
            result.Message);
    }

    [Fact]
    public void Success_HasSuccessCodeAndNoMessage()
    {
        ProtocolResult result = ProtocolResult.Success;

        Assert.Equal(
            ProtocolResultCode.Success,
            result.Code);

        Assert.Null(result.Message);
    }

    [Fact]
    public void IsSuccess_ReturnsTrue_ForSuccessCode()
    {
        ProtocolResult result = new(
            ProtocolResultCode.Success,
            null);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(ProtocolResultCode.InvalidRequest)]
    [InlineData(ProtocolResultCode.NotFound)]
    [InlineData(ProtocolResultCode.NotSupported)]
    [InlineData(ProtocolResultCode.Rejected)]
    [InlineData(ProtocolResultCode.InternalError)]
    public void IsSuccess_ReturnsFalse_ForFailureCode(
        ProtocolResultCode code)
    {
        ProtocolResult result = new(code, null);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void EqualValues_AreEqual()
    {
        ProtocolResult first = new(
            ProtocolResultCode.Rejected,
            "Operation rejected.");

        ProtocolResult second = new(
            ProtocolResultCode.Rejected,
            "Operation rejected.");

        Assert.Equal(first, second);
    }

    [Fact]
    public void DifferentMessages_AreNotEqual()
    {
        ProtocolResult first = new(
            ProtocolResultCode.InternalError,
            "First error.");

        ProtocolResult second = new(
            ProtocolResultCode.InternalError,
            "Second error.");

        Assert.NotEqual(first, second);
    }
}