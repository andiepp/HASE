using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Data;

namespace Hase.Core.Tests;

public sealed class CommandArgumentDescriptorTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Constructor_EmptyDisplayName_ThrowsArgumentException(
        string displayName)
    {
        Assert.Throws<ArgumentException>(
            () => new CommandArgumentDescriptor(
                displayName,
                new BooleanDataDescriptor()));
    }

    [Fact]
    public void Constructor_NullData_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => new CommandArgumentDescriptor(
                "Requested State",
                null!));
    }

    [Fact]
    public void Constructor_ValidValues_PreservesNormalizedDescriptor()
    {
        ByteArrayDataDescriptor data =
            new();

        CommandArgumentDescriptor descriptor =
            new(
                "  Payload  ",
                data)
            {
                Description =
                    "Opaque command payload."
            };

        Assert.Equal(
            "Payload",
            descriptor.DisplayName);

        Assert.Equal(
            "Opaque command payload.",
            descriptor.Description);

        Assert.Same(
            data,
            descriptor.Data);
    }

    [Fact]
    public void Equality_EquivalentDescriptors_AreEqual()
    {
        CommandArgumentDescriptor first =
            new(
                "Payload",
                new ByteArrayDataDescriptor())
            {
                Description =
                    "Opaque bytes."
            };

        CommandArgumentDescriptor second =
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
