using Hase.Protocol;

namespace Hase.Protocol.Tests;

public sealed class ProtocolMessageRoleTests
{
    [Fact]
    public void Request_HasStableNumericValue()
    {
        Assert.Equal((byte)1, (byte)ProtocolMessageRole.Request);
    }

    [Fact]
    public void Response_HasStableNumericValue()
    {
        Assert.Equal((byte)2, (byte)ProtocolMessageRole.Response);
    }

    [Fact]
    public void Notification_HasStableNumericValue()
    {
        Assert.Equal((byte)3, (byte)ProtocolMessageRole.Notification);
    }

    [Theory]
    [InlineData(ProtocolMessageRole.Request)]
    [InlineData(ProtocolMessageRole.Response)]
    [InlineData(ProtocolMessageRole.Notification)]
    public void DefinedRoles_AreRecognized(
        ProtocolMessageRole role)
    {
        Assert.True(Enum.IsDefined(role));
    }

    [Fact]
    public void Zero_IsNotADefinedRole()
    {
        Assert.False(Enum.IsDefined((ProtocolMessageRole)0));
    }
}