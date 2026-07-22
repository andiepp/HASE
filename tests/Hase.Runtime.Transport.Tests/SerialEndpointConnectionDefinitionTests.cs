using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Transport.Attachment;
using Hase.Runtime.Transport.Discovery;
using Hase.Transport.Discovery;
using Hase.Transport.Serial;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class SerialEndpointConnectionDefinitionTests
{
    [Fact]
    public void FromVerifiedEndpoint_ValidEndpoint_ShouldCreateDefinition()
    {
        // Arrange
        var endpointId =
            new EndpointId(
                "arduino-uno-01");

        var verifiedEndpoint =
            new VerifiedUsbSerialEndpoint(
                new UsbSerialEndpointCandidate(
                    "COM10",
                    vendorId: 0x2341,
                    productId: 0x0043,
                    productName: "Arduino Uno",
                    manufacturerName: "Arduino LLC",
                    serialNumber: "75836333537351D06110"),
                endpointId,
                new DescriptorReference(
                    new DescriptorId(
                        "arduino-uno-validation"),
                    version: 1),
                new EndpointDescriptorDefinition());

        var discoveryOptions =
            new UsbSerialEndpointDiscoveryOptions(
                baudRate: 115200,
                dataBits: 8,
                SerialParity.Even,
                SerialStopBits.Two,
                SerialHandshake.RequestToSend,
                verificationTimeout:
                    TimeSpan.FromSeconds(
                        3));

        // Act
        SerialEndpointConnectionDefinition definition =
            SerialEndpointConnectionDefinition
                .FromVerifiedEndpoint(
                    verifiedEndpoint,
                    discoveryOptions);

        // Assert
        Assert.Equal(
            EndpointConnectionOrigin.Discovered,
            definition.Origin);

        Assert.Equal(
            "COM10",
            definition.TransportOptions.PortName);

        Assert.Equal(
            115200,
            definition.TransportOptions.BaudRate);

        Assert.Equal(
            8,
            definition.TransportOptions.DataBits);

        Assert.Equal(
            SerialParity.Even,
            definition.TransportOptions.Parity);

        Assert.Equal(
            SerialStopBits.Two,
            definition.TransportOptions.StopBits);

        Assert.Equal(
            SerialHandshake.RequestToSend,
            definition.TransportOptions.Handshake);

        Assert.Same(
            endpointId,
            definition.ExpectedEndpointId);
    }

    [Fact]
    public void FromVerifiedEndpoint_NullEndpoint_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = SerialEndpointConnectionDefinition
                .FromVerifiedEndpoint(
                    null!,
                    new UsbSerialEndpointDiscoveryOptions(
                        115200,
                        TimeSpan.FromSeconds(
                            3)));
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void FromVerifiedEndpoint_NullDiscoveryOptions_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = SerialEndpointConnectionDefinition
                .FromVerifiedEndpoint(
                    CreateVerifiedEndpoint(),
                    null!);
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void FromConfiguration_ExpectedIdentity_ShouldCreateDefinition()
    {
        var transportOptions =
            new SerialTransportOptions(
                "COM5",
                115200);

        var endpointId =
            new EndpointId(
                "arduino-uno-01");

        SerialEndpointConnectionDefinition definition =
            SerialEndpointConnectionDefinition
                .FromConfiguration(
                    transportOptions,
                    endpointId);

        Assert.Equal(
            EndpointConnectionOrigin.Configured,
            definition.Origin);

        Assert.Same(
            transportOptions,
            definition.TransportOptions);

        Assert.Same(
            endpointId,
            definition.ExpectedEndpointId);
    }

    [Fact]
    public void FromConfiguration_Result_ShouldImplementCommonDefinition()
    {
        SerialEndpointConnectionDefinition definition =
            SerialEndpointConnectionDefinition
                .FromConfiguration(
                    new SerialTransportOptions(
                        "/dev/ttyUSB0",
                        115200));

        Assert.IsAssignableFrom<IEndpointConnectionDefinition>(
            definition);
    }

    [Fact]
    public void FromConfiguration_WithoutIdentity_ShouldAllowNullIdentity()
    {
        var transportOptions =
            new SerialTransportOptions(
                "COM5",
                115200);

        SerialEndpointConnectionDefinition definition =
            SerialEndpointConnectionDefinition
                .FromConfiguration(
                    transportOptions);

        Assert.Equal(
            EndpointConnectionOrigin.Configured,
            definition.Origin);

        Assert.Same(
            transportOptions,
            definition.TransportOptions);

        Assert.Null(
            definition.ExpectedEndpointId);
    }

    [Fact]
    public void FromConfiguration_NullTransportOptions_ShouldThrow()
    {
        void Act()
        {
            _ = SerialEndpointConnectionDefinition
                .FromConfiguration(
                    null!);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    private static VerifiedUsbSerialEndpoint CreateVerifiedEndpoint()
    {
        return new VerifiedUsbSerialEndpoint(
            new UsbSerialEndpointCandidate(
                "COM10"),
            new EndpointId(
                "arduino-uno-01"),
            new DescriptorReference(
                new DescriptorId(
                    "arduino-uno-validation"),
                version: 1),
            new EndpointDescriptorDefinition());
    }
}