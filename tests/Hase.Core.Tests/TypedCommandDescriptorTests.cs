using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Properties;

namespace Hase.Core.Tests;

public sealed class TypedCommandDescriptorTests
{
    [Fact]
    public void ExistingConstructor_CreatesParameterlessCommand()
    {
        CommandDescriptor descriptor =
            new(
                DescriptorPath.Parse(
                    "Controller.Reset"),
                "Reset");

        Assert.Null(
            descriptor.Argument);
    }

    [Fact]
    public void ArgumentConstructor_NullArgument_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => new CommandDescriptor(
                DescriptorPath.Parse(
                    "Controller.Send"),
                "Send",
                null!));
    }

    [Fact]
    public void ArgumentConstructor_CreatesOneRequiredTypedArgument()
    {
        CommandArgumentDescriptor argument =
            new(
                "Payload",
                new ByteArrayDataDescriptor());

        CommandDescriptor descriptor =
            new(
                DescriptorPath.Parse(
                    "Controller.Send"),
                "Send",
                argument);

        Assert.Same(
            argument,
            descriptor.Argument);
    }

    [Fact]
    public void Equality_ParameterlessAndTypedCommands_AreNotEqual()
    {
        DescriptorPath path =
            DescriptorPath.Parse(
                "Controller.Send");

        CommandDescriptor parameterless =
            new(
                path,
                "Send");

        CommandDescriptor typed =
            new(
                path,
                "Send",
                new CommandArgumentDescriptor(
                    "Payload",
                    new ByteArrayDataDescriptor()));

        Assert.NotEqual(
            parameterless,
            typed);
    }
}
