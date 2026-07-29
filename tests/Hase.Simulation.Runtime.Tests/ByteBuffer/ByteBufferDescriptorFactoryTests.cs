using Hase.Core.Domain.Data;
using Hase.Core.Domain.Properties;
using Hase.Simulation.Runtime.ByteBuffer;

namespace Hase.Simulation.Runtime.Tests.ByteBuffer;

public sealed class ByteBufferDescriptorFactoryTests
{
    [Fact]
    public void CreateDescriptor_ShouldDefineFourWritablePropertyTypesAndTypedCommand()
    {
        var descriptor =
            ByteBufferDescriptorFactory.CreateDescriptor();

        Assert.Equal(
            ByteBufferDescriptorFactory.InstrumentId,
            descriptor.Id);

        Assert.Equal(
            4,
            descriptor.Interface.Properties.Count);
        PropertyDescriptor property =
            descriptor.Interface.Properties.Single(
                candidate =>
                    candidate.Id
                    == ByteBufferDescriptorFactory.ValuePropertyId);
        Assert.Equal(
            ByteBufferDescriptorFactory.ValuePropertyId,
            property.Id);
        Assert.Equal(
            PropertyAccessMode.ReadWrite,
            property.AccessMode);
        Assert.IsType<ByteArrayDataDescriptor>(
            property.Data);
        Assert.IsType<BooleanDataDescriptor>(
            descriptor.Interface.Properties.Single(
                candidate =>
                    candidate.Id
                    == ByteBufferDescriptorFactory.EnabledPropertyId).Data);
        NumericDataDescriptor numeric =
            Assert.IsType<NumericDataDescriptor>(
                descriptor.Interface.Properties.Single(
                    candidate =>
                        candidate.Id
                        == ByteBufferDescriptorFactory.SetpointPropertyId).Data);
        Assert.Equal(
            -40.0,
            numeric.Range?.Minimum);
        Assert.Equal(
            125.0,
            numeric.Range?.Maximum);
        Assert.IsType<StringDataDescriptor>(
            descriptor.Interface.Properties.Single(
                candidate =>
                    candidate.Id
                    == ByteBufferDescriptorFactory.LabelPropertyId).Data);
        Assert.All(
            descriptor.Interface.Properties,
            candidate =>
                Assert.Equal(
                    PropertyAccessMode.ReadWrite,
                    candidate.AccessMode));

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
