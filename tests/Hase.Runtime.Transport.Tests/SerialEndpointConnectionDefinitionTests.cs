using Hase.Core.Domain.Identity;
using Hase.Runtime.Transport.Attachment;
using Hase.Transport.Serial;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class SerialEndpointConnectionDefinitionTests
{
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
}