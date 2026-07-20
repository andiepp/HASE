using Hase.Transport.Serial;

namespace Hase.Transport.Tests;

public sealed class SerialByteStreamFactoryBoundaryTests
{
    [Fact]
    public void PublicBoundary_ShouldExposePhysicalSerialFactory()
    {
        ISerialByteStreamFactory factory =
            new SystemIoPortsSerialByteStreamFactory();

        Assert.NotNull(
            factory);

        Assert.True(
            typeof(ISerialByteStreamFactory)
                .IsPublic);

        Assert.True(
            typeof(SystemIoPortsSerialByteStreamFactory)
                .IsPublic);
    }
}