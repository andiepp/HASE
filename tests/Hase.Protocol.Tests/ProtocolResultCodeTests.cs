using Hase.Protocol;

namespace Hase.Protocol.Tests;

public sealed class ProtocolResultCodeTests
{
    [Theory]
    [InlineData(ProtocolResultCode.Success, 0)]
    [InlineData(ProtocolResultCode.InvalidRequest, 1)]
    [InlineData(ProtocolResultCode.NotFound, 2)]
    [InlineData(ProtocolResultCode.NotSupported, 3)]
    [InlineData(ProtocolResultCode.Rejected, 4)]
    [InlineData(ProtocolResultCode.InternalError, 5)]
    public void ResultCode_HasStableNumericValue(
        ProtocolResultCode resultCode,
        byte expectedValue)
    {
        Assert.Equal(expectedValue, (byte)resultCode);
    }

    [Theory]
    [InlineData(ProtocolResultCode.Success)]
    [InlineData(ProtocolResultCode.InvalidRequest)]
    [InlineData(ProtocolResultCode.NotFound)]
    [InlineData(ProtocolResultCode.NotSupported)]
    [InlineData(ProtocolResultCode.Rejected)]
    [InlineData(ProtocolResultCode.InternalError)]
    public void DefinedValues_AreRecognized(
        ProtocolResultCode resultCode)
    {
        Assert.True(Enum.IsDefined(resultCode));
    }

    [Fact]
    public void UnknownValue_IsNotDefined()
    {
        Assert.False(
            Enum.IsDefined((ProtocolResultCode)255));
    }
}