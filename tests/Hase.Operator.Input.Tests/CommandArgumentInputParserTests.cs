using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Properties;

namespace Hase.Operator.Input.Tests;

public sealed class CommandArgumentInputParserTests
{
    [Fact]
    public void Parse_ParameterlessCommand_ShouldIgnoreInput()
    {
        CommandArgumentInputParseResult result =
            CommandArgumentInputParser.Parse(
                new CommandDescriptor(
                    DescriptorPath.Parse(
                        "Controller.Reset"),
                    "Reset"),
                "unused");

        Assert.True(
            result.IsSuccess);
        Assert.False(
            result.HasArgument);
        Assert.Null(
            result.Value);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("FALSE", false)]
    [InlineData(" True ", true)]
    public void Parse_BooleanArgument_ShouldReturnBoolean(
        string input,
        bool expected)
    {
        CommandArgumentInputParseResult result =
            CommandArgumentInputParser.Parse(
                CreateCommand(
                    new BooleanDataDescriptor()),
                input);

        Assert.True(
            result.IsSuccess);
        Assert.True(
            result.HasArgument);
        Assert.Equal(
            expected,
            Assert.IsType<bool>(
                result.Value));
    }

    [Theory]
    [InlineData(null, CommandArgumentInputFailure.MissingInput)]
    [InlineData("", CommandArgumentInputFailure.MissingInput)]
    [InlineData("yes", CommandArgumentInputFailure.InvalidFormat)]
    public void Parse_InvalidBooleanArgument_ShouldReturnStableFailure(
        string? input,
        CommandArgumentInputFailure expected)
    {
        CommandArgumentInputParseResult result =
            CommandArgumentInputParser.Parse(
                CreateCommand(
                    new BooleanDataDescriptor()),
                input);

        Assert.False(
            result.IsSuccess);
        Assert.Equal(
            expected,
            result.Failure);
        Assert.Null(
            result.Value);
        Assert.NotEmpty(
            result.Message);
    }

    [Theory]
    [InlineData("23.5", 23.5)]
    [InlineData("-1.25e2", -125.0)]
    [InlineData(" 7 ", 7.0)]
    public void Parse_InvariantFiniteNumericArgument_ShouldReturnDouble(
        string input,
        double expected)
    {
        CommandArgumentInputParseResult result =
            CommandArgumentInputParser.Parse(
                CreateCommand(
                    CreateNumericDescriptor()),
                input);

        Assert.True(
            result.IsSuccess);
        Assert.Equal(
            expected,
            Assert.IsType<double>(
                result.Value));
    }

    [Theory]
    [InlineData(null, CommandArgumentInputFailure.MissingInput)]
    [InlineData("", CommandArgumentInputFailure.MissingInput)]
    [InlineData("23,5", CommandArgumentInputFailure.InvalidFormat)]
    [InlineData("NaN", CommandArgumentInputFailure.InvalidFormat)]
    [InlineData("Infinity", CommandArgumentInputFailure.InvalidFormat)]
    public void Parse_InvalidNumericArgument_ShouldReturnStableFailure(
        string? input,
        CommandArgumentInputFailure expected)
    {
        CommandArgumentInputParseResult result =
            CommandArgumentInputParser.Parse(
                CreateCommand(
                    CreateNumericDescriptor()),
                input);

        Assert.False(
            result.IsSuccess);
        Assert.Equal(
            expected,
            result.Failure);
    }

    [Theory]
    [InlineData("-40", -40.0)]
    [InlineData("125", 125.0)]
    public void Parse_NumericBoundaryArgument_ShouldBeInclusive(
        string input,
        double expected)
    {
        CommandArgumentInputParseResult result =
            CommandArgumentInputParser.Parse(
                CreateCommand(
                    CreateNumericDescriptor(
                        new ValueRange(
                            -40,
                            125))),
                input);

        Assert.True(
            result.IsSuccess);
        Assert.Equal(
            expected,
            Assert.IsType<double>(
                result.Value));
    }

    [Theory]
    [InlineData("-40.1")]
    [InlineData("125.1")]
    public void Parse_NumericArgumentOutsideRange_ShouldFail(
        string input)
    {
        CommandArgumentInputParseResult result =
            CommandArgumentInputParser.Parse(
                CreateCommand(
                    CreateNumericDescriptor(
                        new ValueRange(
                            -40,
                            125))),
                input);

        Assert.False(
            result.IsSuccess);
        Assert.Equal(
            CommandArgumentInputFailure.ValueOutsideRange,
            result.Failure);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("  HASE  ")]
    public void Parse_StringArgument_ShouldPreserveExactInput(
        string input)
    {
        CommandArgumentInputParseResult result =
            CommandArgumentInputParser.Parse(
                CreateCommand(
                    new StringDataDescriptor()),
                input);

        Assert.True(
            result.IsSuccess);
        Assert.Equal(
            input,
            Assert.IsType<string>(
                result.Value));
    }

    [Fact]
    public void Parse_NullStringArgument_ShouldFailAsMissing()
    {
        CommandArgumentInputParseResult result =
            CommandArgumentInputParser.Parse(
                CreateCommand(
                    new StringDataDescriptor()),
                null);

        Assert.False(
            result.IsSuccess);
        Assert.Equal(
            CommandArgumentInputFailure.MissingInput,
            result.Failure);
    }

    [Theory]
    [InlineData("00 53 FF", new byte[] { 0x00, 0x53, 0xFF })]
    [InlineData("deadBEEF", new byte[] { 0xDE, 0xAD, 0xBE, 0xEF })]
    public void Parse_ByteArrayArgument_ShouldReturnExactBytes(
        string input,
        byte[] expected)
    {
        CommandArgumentInputParseResult result =
            CommandArgumentInputParser.Parse(
                CreateCommand(
                    new ByteArrayDataDescriptor()),
                input);

        Assert.True(
            result.IsSuccess);
        ByteArrayValue value =
            Assert.IsType<ByteArrayValue>(
                result.Value);
        Assert.Equal(
            expected,
            value.ToArray());
    }

    [Theory]
    [InlineData(null, CommandArgumentInputFailure.MissingInput)]
    [InlineData("", CommandArgumentInputFailure.MissingInput)]
    [InlineData("0", CommandArgumentInputFailure.InvalidFormat)]
    [InlineData("GG", CommandArgumentInputFailure.InvalidFormat)]
    public void Parse_InvalidByteArrayArgument_ShouldReturnStableFailure(
        string? input,
        CommandArgumentInputFailure expected)
    {
        CommandArgumentInputParseResult result =
            CommandArgumentInputParser.Parse(
                CreateCommand(
                    new ByteArrayDataDescriptor()),
                input);

        Assert.False(
            result.IsSuccess);
        Assert.Equal(
            expected,
            result.Failure);
    }

    [Fact]
    public void Parse_NullDescriptor_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
                CommandArgumentInputParser.Parse(
                    null!,
                    "value"));
    }

    private static CommandDescriptor CreateCommand(
        DataDescriptor data)
    {
        return new CommandDescriptor(
            DescriptorPath.Parse(
                "Validation.Execute"),
            "Execute",
            new CommandArgumentDescriptor(
                "Value",
                data));
    }

    private static NumericDataDescriptor CreateNumericDescriptor(
        ValueRange? range = null)
    {
        return new NumericDataDescriptor(
            Quantities.Temperature,
            Units.Celsius,
            range);
    }
}
