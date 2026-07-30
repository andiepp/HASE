using Hase.Core.Domain.Data;
using Hase.Core.Domain.Events;
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
            descriptor.Interface.Commands.Single(
                candidate =>
                    candidate.Path
                    == ByteBufferDescriptorFactory.ReplaceCommandPath);
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

    [Fact]
    public void CreateDescriptor_ShouldDefineFiveParameterlessEventTriggers()
    {
        var descriptor =
            ByteBufferDescriptorFactory.CreateDescriptor();

        DescriptorPath[] expectedPaths =
        [
            ByteBufferDescriptorFactory.EmitNoPayloadCommandPath,
            ByteBufferDescriptorFactory.EmitBooleanCommandPath,
            ByteBufferDescriptorFactory.EmitNumericCommandPath,
            ByteBufferDescriptorFactory.EmitStringCommandPath,
            ByteBufferDescriptorFactory.EmitByteArrayCommandPath
        ];

        Assert.Equal(
            6,
            descriptor.Interface.Commands.Count);

        foreach (DescriptorPath path in expectedPaths)
        {
            var command =
                descriptor.Interface.Commands.Single(
                    candidate =>
                        candidate.Path == path);

            Assert.Null(
                command.Argument);
        }
    }

    [Fact]
    public void CreateDescriptor_ShouldDefineAllEventPayloadTypes()
    {
        var descriptor =
            ByteBufferDescriptorFactory.CreateDescriptor();

        Assert.Equal(
            5,
            descriptor.Interface.Events.Count);

        EventDescriptor noPayload =
            descriptor.Interface.Events.Single(
                candidate =>
                    candidate.Path
                    == ByteBufferDescriptorFactory.NoPayloadEventPath);
        Assert.Null(
            noPayload.Payload);

        AssertPayloadType<BooleanDataDescriptor>(
            descriptor,
            ByteBufferDescriptorFactory.BooleanEventPath,
            "State");
        AssertPayloadType<NumericDataDescriptor>(
            descriptor,
            ByteBufferDescriptorFactory.NumericEventPath,
            "Temperature");
        AssertPayloadType<StringDataDescriptor>(
            descriptor,
            ByteBufferDescriptorFactory.StringEventPath,
            "Message");
        AssertPayloadType<ByteArrayDataDescriptor>(
            descriptor,
            ByteBufferDescriptorFactory.ByteArrayEventPath,
            "Bytes");

        NumericDataDescriptor numeric =
            Assert.IsType<NumericDataDescriptor>(
                descriptor.Interface.Events.Single(
                        candidate =>
                            candidate.Path
                            == ByteBufferDescriptorFactory.NumericEventPath)
                    .Payload!
                    .Data);
        Assert.Equal(
            Quantities.Temperature,
            numeric.Quantity);
        Assert.Equal(
            Units.Celsius,
            numeric.NativeUnit);
    }

    private static void AssertPayloadType<TData>(
        Hase.Core.Domain.Instruments.InstrumentDescriptor descriptor,
        DescriptorPath path,
        string displayName)
        where TData : DataDescriptor
    {
        EventPayloadDescriptor payload =
            descriptor.Interface.Events.Single(
                    candidate =>
                        candidate.Path == path)
                .Payload!;

        Assert.Equal(
            displayName,
            payload.DisplayName);
        Assert.IsType<TData>(
            payload.Data);
    }
}
