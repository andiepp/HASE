using Hase.Core.Domain.Data;
using Hase.Core.Domain.Events;

namespace Hase.Core.Tests;

public sealed class EventPayloadDescriptorTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Constructor_EmptyDisplayName_ThrowsArgumentException(
        string displayName)
    {
        Assert.Throws<ArgumentException>(
            () => new EventPayloadDescriptor(
                displayName,
                new BooleanDataDescriptor()));
    }

    [Fact]
    public void Constructor_NullData_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => new EventPayloadDescriptor(
                "State",
                null!));
    }

    [Fact]
    public void Constructor_ValidValues_PreservesNormalizedDescriptor()
    {
        ByteArrayDataDescriptor data =
            new();

        EventPayloadDescriptor descriptor =
            new(
                "  Payload  ",
                data)
            {
                Description =
                    "Opaque event payload."
            };

        Assert.Equal(
            "Payload",
            descriptor.DisplayName);

        Assert.Equal(
            "Opaque event payload.",
            descriptor.Description);

        Assert.Same(
            data,
            descriptor.Data);
    }

    [Fact]
    public void Equality_EquivalentDescriptors_AreEqual()
    {
        EventPayloadDescriptor first =
            new(
                "Payload",
                new ByteArrayDataDescriptor())
            {
                Description =
                    "Opaque bytes."
            };

        EventPayloadDescriptor second =
            new(
                "Payload",
                new ByteArrayDataDescriptor())
            {
                Description =
                    "Opaque bytes."
            };

        Assert.Equal(
            first,
            second);
    }
}
