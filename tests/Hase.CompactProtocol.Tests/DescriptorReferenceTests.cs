using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;

namespace Hase.CompactProtocol.Tests;

public sealed class DescriptorReferenceTests
{
    [Fact]
    public void DescriptorId_ValidValue_ShouldStoreTrimmedValue()
    {
        var id =
            new DescriptorId(
                "  arduino-uno-environment  ");

        Assert.Equal(
            "arduino-uno-environment",
            id.Value);
    }

    [Fact]
    public void DescriptorId_NullValue_ShouldThrow()
    {
        void Act()
        {
            _ = new DescriptorId(
                null!);
        }

        Assert.Throws<ArgumentException>(
            Act);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void DescriptorId_EmptyOrWhitespaceValue_ShouldThrow(
        string value)
    {
        void Act()
        {
            _ = new DescriptorId(
                value);
        }

        Assert.Throws<ArgumentException>(
            Act);
    }

    [Fact]
    public void Constructor_ValidReference_ShouldStoreExactIdentityAndVersion()
    {
        var id =
            new DescriptorId(
                "arduino-uno-environment");

        var reference =
            new DescriptorReference(
                id,
                version: 1);

        Assert.Same(
            id,
            reference.Id);

        Assert.Equal(
            1,
            reference.Version);
    }

    [Fact]
    public void Constructor_NullId_ShouldThrow()
    {
        void Act()
        {
            _ = new DescriptorReference(
                null!,
                version: 1);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_ZeroVersion_ShouldThrow()
    {
        void Act()
        {
            _ = new DescriptorReference(
                new DescriptorId(
                    "arduino-uno-environment"),
                version: 0);
        }

        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                Act);

        Assert.Equal(
            "version",
            exception.ParamName);
    }

    [Fact]
    public void Equality_SameIdentityAndVersion_ShouldBeEqual()
    {
        var first =
            new DescriptorReference(
                new DescriptorId(
                    "arduino-uno-environment"),
                version: 1);

        var second =
            new DescriptorReference(
                new DescriptorId(
                    "arduino-uno-environment"),
                version: 1);

        Assert.Equal(
            first,
            second);
    }

    [Fact]
    public void Equality_DifferentVersion_ShouldNotBeEqual()
    {
        var first =
            new DescriptorReference(
                new DescriptorId(
                    "arduino-uno-environment"),
                version: 1);

        var second =
            new DescriptorReference(
                new DescriptorId(
                    "arduino-uno-environment"),
                version: 2);

        Assert.NotEqual(
            first,
            second);
    }
}