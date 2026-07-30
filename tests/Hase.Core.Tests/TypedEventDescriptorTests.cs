using Hase.Core.Domain.Data;
using Hase.Core.Domain.Events;
using Hase.Core.Domain.Properties;

namespace Hase.Core.Tests;

public sealed class TypedEventDescriptorTests
{
    [Fact]
    public void Constructor_WithoutPayload_DescribesParameterlessEvent()
    {
        EventDescriptor descriptor =
            new(
                DescriptorPath.Parse("Controller.ButtonPressed"),
                "Button pressed");

        Assert.Null(
            descriptor.Payload);
    }

    [Fact]
    public void Payload_TypedDescriptor_IsPreserved()
    {
        EventPayloadDescriptor payload =
            new(
                "Pressed State",
                new BooleanDataDescriptor())
            {
                Description =
                    "State reported by the event."
            };

        EventDescriptor descriptor =
            new(
                DescriptorPath.Parse("Controller.ButtonPressed"),
                "Button pressed")
            {
                Payload = payload
            };

        Assert.Same(
            payload,
            descriptor.Payload);
    }

    [Fact]
    public void Equality_EquivalentTypedEvents_AreEqual()
    {
        EventDescriptor first =
            new(
                DescriptorPath.Parse("Buffer.Replaced"),
                "Buffer replaced")
            {
                Payload =
                    new EventPayloadDescriptor(
                        "Buffer",
                        new ByteArrayDataDescriptor())
            };

        EventDescriptor second =
            new(
                DescriptorPath.Parse("Buffer.Replaced"),
                "Buffer replaced")
            {
                Payload =
                    new EventPayloadDescriptor(
                        "Buffer",
                        new ByteArrayDataDescriptor())
            };

        Assert.Equal(
            first,
            second);
    }
}
