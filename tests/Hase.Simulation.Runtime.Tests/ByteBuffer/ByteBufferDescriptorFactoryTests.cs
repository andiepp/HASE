using Hase.Core.Domain.Data;
using Hase.Core.Domain.Properties;
using Hase.Simulation.Runtime.ByteBuffer;

namespace Hase.Simulation.Runtime.Tests.ByteBuffer;

public sealed class ByteBufferDescriptorFactoryTests
{
    [Fact]
    public void CreateDescriptor_ShouldDefineReadOnlyValueAndTypedReplaceCommand()
    {
        var descriptor =
            ByteBufferDescriptorFactory.CreateDescriptor();

        Assert.Equal(
            ByteBufferDescriptorFactory.InstrumentId,
            descriptor.Id);

        PropertyDescriptor property =
            Assert.Single(
                descriptor.Interface.Properties);
        Assert.Equal(
            ByteBufferDescriptorFactory.ValuePropertyId,
            property.Id);
        Assert.Equal(
            PropertyAccessMode.Read,
            property.AccessMode);
        Assert.IsType<ByteArrayDataDescriptor>(
            property.Data);

        var command =
            Assert.Single(
                descriptor.Interface.Commands);
        Assert.Equal(
            ByteBufferDescriptorFactory.ReplaceCommandPath,
            command.Path);
        Assert.NotNull(
            command.Argument);
        Assert.Equal(
            "Payload",
            command.Argument.DisplayName);
        Assert.IsType<ByteArrayDataDescriptor>(
            command.Argument.Data);
    }
}
