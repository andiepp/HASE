using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Northbound;

namespace Hase.Runtime.Tests.Northbound;

public sealed class RuntimeHostCommandArgumentValidatorTests
{
    [Fact]
    public void IsValid_ParameterlessCommandWithNull_ReturnsTrue()
    {
        Assert.True(
            RuntimeHostCommandArgumentValidator.IsValid(
                CreateParameterlessCommand(),
                argument: null));
    }

    [Fact]
    public void IsValid_ParameterlessCommandWithValue_ReturnsFalse()
    {
        Assert.False(
            RuntimeHostCommandArgumentValidator.IsValid(
                CreateParameterlessCommand(),
                true));
    }

    [Fact]
    public void IsValid_TypedCommandWithNull_ReturnsFalse()
    {
        Assert.False(
            RuntimeHostCommandArgumentValidator.IsValid(
                CreateTypedCommand(
                    new BooleanDataDescriptor()),
                argument: null));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData("true", false)]
    [InlineData(1, false)]
    public void IsValid_BooleanDescriptor_RequiresBoolean(
        object argument,
        bool expected)
    {
        Assert.Equal(
            expected,
            RuntimeHostCommandArgumentValidator.IsValid(
                CreateTypedCommand(
                    new BooleanDataDescriptor()),
                argument));
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("payload", true)]
    [InlineData(false, false)]
    public void IsValid_StringDescriptor_RequiresString(
        object argument,
        bool expected)
    {
        Assert.Equal(
            expected,
            RuntimeHostCommandArgumentValidator.IsValid(
                CreateTypedCommand(
                    new StringDataDescriptor()),
                argument));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(1L, true)]
    [InlineData(1.0, true)]
    [InlineData("1", false)]
    [InlineData(true, false)]
    public void IsValid_NumericDescriptor_RequiresSupportedNumericValue(
        object argument,
        bool expected)
    {
        Assert.Equal(
            expected,
            RuntimeHostCommandArgumentValidator.IsValid(
                CreateTypedCommand(
                    CreateNumericDataDescriptor()),
                argument));
    }

    [Fact]
    public void IsValid_ByteArrayDescriptorWithByteArrayValue_ReturnsTrue()
    {
        Assert.True(
            RuntimeHostCommandArgumentValidator.IsValid(
                CreateTypedCommand(
                    new ByteArrayDataDescriptor()),
                new ByteArrayValue(
                    new byte[]
                    {
                        0x00,
                        0xFF
                    })));
    }

    [Fact]
    public void IsValid_ByteArrayDescriptorWithMutableArray_ReturnsFalse()
    {
        Assert.False(
            RuntimeHostCommandArgumentValidator.IsValid(
                CreateTypedCommand(
                    new ByteArrayDataDescriptor()),
                new byte[]
                {
                    0x00,
                    0xFF
                }));
    }

    [Fact]
    public void IsValid_NullCommand_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => RuntimeHostCommandArgumentValidator.IsValid(
                null!,
                argument: null));
    }

    private static CommandDescriptor CreateParameterlessCommand()
    {
        return new CommandDescriptor(
            DescriptorPath.Parse(
                "Controller.Send"),
            "Send");
    }

    private static CommandDescriptor CreateTypedCommand(
        DataDescriptor data)
    {
        return new CommandDescriptor(
            DescriptorPath.Parse(
                "Controller.Send"),
            "Send",
            new CommandArgumentDescriptor(
                "Argument",
                data));
    }

    private static NumericDataDescriptor CreateNumericDataDescriptor()
    {
        Quantity quantity =
            new(
                "number",
                "Number");

        Unit unit =
            new(
                "one",
                "One",
                "1",
                quantity);

        return new NumericDataDescriptor(
            quantity,
            unit);
    }
}
