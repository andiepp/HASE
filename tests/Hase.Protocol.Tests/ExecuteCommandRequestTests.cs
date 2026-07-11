using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Protocol;

namespace Hase.Protocol.Tests;

public sealed class ExecuteCommandRequestTests
{
    [Fact]
    public void Constructor_SetsProtocolProperties()
    {
        InstrumentId instrumentId = new("Instrument-1");
        DescriptorPath path =
            DescriptorPath.Parse("DDS.Sweep.Start");

        ExecuteCommandRequest request = new(
            new CorrelationId(17),
            instrumentId,
            path,
            null);

        Assert.Equal(
            ProtocolVersion.Current,
            request.Version);

        Assert.Equal(
            ProtocolMessageRole.Request,
            request.Role);

        Assert.Equal(
            ProtocolMessageType.ExecuteCommandRequest,
            request.MessageType);

        Assert.Equal(
            new CorrelationId(17),
            request.CorrelationId);
    }

    [Fact]
    public void Constructor_StoresTarget()
    {
        InstrumentId instrumentId = new("Instrument-1");
        DescriptorPath path =
            DescriptorPath.Parse("DDS.Sweep.Start");

        ExecuteCommandRequest request = new(
            CorrelationId.None,
            instrumentId,
            path,
            123);

        Assert.Equal(
            instrumentId,
            request.InstrumentId);

        Assert.Equal(
            path,
            request.CommandPath);

        Assert.Equal(
            123,
            request.Argument);
    }

    [Fact]
    public void Constructor_AllowsNullArgument()
    {
        ExecuteCommandRequest request = new(
            CorrelationId.None,
            new InstrumentId("Instrument-1"),
            DescriptorPath.Parse("DDS.Reset"),
            null);

        Assert.Null(
            request.Argument);
    }

    [Fact]
    public void EqualRequests_AreEqual()
    {
        ExecuteCommandRequest first = new(
            new CorrelationId(5),
            new InstrumentId("Instrument-1"),
            DescriptorPath.Parse("DDS.Reset"),
            null);

        ExecuteCommandRequest second = new(
            new CorrelationId(5),
            new InstrumentId("Instrument-1"),
            DescriptorPath.Parse("DDS.Reset"),
            null);

        Assert.Equal(first, second);
    }
}