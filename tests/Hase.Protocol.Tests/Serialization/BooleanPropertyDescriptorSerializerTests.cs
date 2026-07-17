using Hase.Core.Domain.Data;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Protocol.Serialization;

namespace Hase.Protocol.Tests.Serialization;

public sealed class BooleanPropertyDescriptorSerializerTests
{
    [Fact]
    public void RoundTrip_BooleanReadWriteProperty_PreservesDescriptor()
    {
        // Arrange
        var serializer =
            new PropertyDescriptorSerializer();

        var original =
            new PropertyDescriptor(
                new PropertyId(
                    "physical.controller.status-led-enabled"),
                DescriptorPath.Parse(
                    "Controller.StatusLedEnabled"),
                "Status LED Enabled",
                new BooleanDataDescriptor())
            {
                Description =
                    "Controls the active-high status LED on GPIO2.",
                AccessMode =
                    PropertyAccessMode.ReadWrite
            };

        var writer =
            new BinaryProtocolWriter();

        // Act
        serializer.Write(
            writer,
            original);

        byte[] encoded =
            writer.ToArray();

        var reader =
            new BinaryProtocolReader(
                encoded);

        PropertyDescriptor decoded =
            serializer.Read(
                reader);

        // Assert
        Assert.Equal(
            0x03,
            encoded[^1]);

        Assert.Equal(
            original,
            decoded);

        Assert.Equal(
            original.Id,
            decoded.Id);

        Assert.Equal(
            original.Path,
            decoded.Path);

        Assert.Equal(
            "Status LED Enabled",
            decoded.DisplayName);

        Assert.Equal(
            "Controls the active-high status LED on GPIO2.",
            decoded.Description);

        Assert.Equal(
            PropertyAccessMode.ReadWrite,
            decoded.AccessMode);

        Assert.IsType<BooleanDataDescriptor>(
            decoded.Data);

        Assert.Equal(
            0,
            reader.Remaining);
    }
}