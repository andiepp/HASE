using Hase.Protocol;

namespace Hase.Protocol.Tests;

public sealed class ProtocolMessageTypeTests
{
    [Theory]
    [InlineData(ProtocolMessageType.DiscoverRequest, 1)]
    [InlineData(ProtocolMessageType.DiscoverResponse, 2)]
    [InlineData(ProtocolMessageType.ReadPropertyRequest, 10)]
    [InlineData(ProtocolMessageType.ReadPropertyResponse, 11)]
    [InlineData(ProtocolMessageType.WritePropertyRequest, 20)]
    [InlineData(ProtocolMessageType.WritePropertyResponse, 21)]
    [InlineData(ProtocolMessageType.ExecuteCommandRequest, 30)]
    [InlineData(ProtocolMessageType.ExecuteCommandResponse, 31)]
    [InlineData(ProtocolMessageType.EventNotification, 40)]
    [InlineData(ProtocolMessageType.ReadEndpointDescriptorRequest, 52)]
    [InlineData(ProtocolMessageType.ReadEndpointDescriptorResponse, 53)]
    public void MessageType_HasStableNumericValue(
        ProtocolMessageType messageType,
        byte expectedValue)
    {
        Assert.Equal(expectedValue, (byte)messageType);
    }

    [Fact]
    public void Zero_IsNotDefined()
    {
        Assert.False(Enum.IsDefined((ProtocolMessageType)0));
    }

    [Theory]
    [InlineData(ProtocolMessageType.DiscoverRequest)]
    [InlineData(ProtocolMessageType.DiscoverResponse)]
    [InlineData(ProtocolMessageType.ReadPropertyRequest)]
    [InlineData(ProtocolMessageType.ReadPropertyResponse)]
    [InlineData(ProtocolMessageType.WritePropertyRequest)]
    [InlineData(ProtocolMessageType.WritePropertyResponse)]
    [InlineData(ProtocolMessageType.ExecuteCommandRequest)]
    [InlineData(ProtocolMessageType.ExecuteCommandResponse)]
    [InlineData(ProtocolMessageType.EventNotification)]
    [InlineData(ProtocolMessageType.ReadEndpointDescriptorRequest)]
    [InlineData(ProtocolMessageType.ReadEndpointDescriptorResponse)]
    public void DefinedValues_AreRecognized(
        ProtocolMessageType messageType)
    {
        Assert.True(Enum.IsDefined(messageType));
    }

    [Theory]
    [InlineData(50)]
    [InlineData(51)]
    public void ReservedValues_AreNotDefined(byte value)
    {
        Assert.False(
            Enum.IsDefined((ProtocolMessageType)value));
    }
}