using System.Text;

namespace Hase.CompactProtocol.Tests;

public sealed class CompactSerialProtocolV1InspectionTests
{
    [Fact]
    public void Constants_ShouldExposeFrozenWireFormat()
    {
        Assert.Equal(0x48, CompactSerialProtocolV1Inspection.StartMarkerFirstByte);
        Assert.Equal(0x53, CompactSerialProtocolV1Inspection.StartMarkerSecondByte);
        Assert.Equal(0x01, CompactSerialProtocolV1Inspection.ProtocolVersion);
        Assert.Equal(8, CompactSerialProtocolV1Inspection.FrameOverheadLength);
        Assert.Equal(byte.MaxValue, CompactSerialProtocolV1Inspection.MaximumPayloadLength);
    }

    [Fact]
    public void TryGetMessageTypeName_ShouldResolveKnownAndRejectUnknown()
    {
        Assert.True(
            CompactSerialProtocolV1Inspection.TryGetMessageTypeName(
                0x09,
                out string eventName));
        Assert.Equal("EventNotification", eventName);

        Assert.False(
            CompactSerialProtocolV1Inspection.TryGetMessageTypeName(
                0xFF,
                out string unknownName));
        Assert.Empty(unknownName);
    }

    [Fact]
    public void CorrelationRules_ShouldDistinguishNotifications()
    {
        Assert.True(
            CompactSerialProtocolV1Inspection.RequiresZeroCorrelationId(0x09));
        Assert.False(
            CompactSerialProtocolV1Inspection.RequiresNonZeroCorrelationId(0x09));
        Assert.True(
            CompactSerialProtocolV1Inspection.RequiresNonZeroCorrelationId(0x01));
    }

    [Fact]
    public void CalculateCrc_ShouldUseCcittFalseReferenceVector()
    {
        byte[] bytes = Encoding.ASCII.GetBytes("123456789");

        Assert.Equal(
            0x29B1,
            CompactSerialProtocolV1Inspection.CalculateCrc(bytes));
    }
}
