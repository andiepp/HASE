using Hase.Core.Domain.Commands;

namespace Hase.Operator.Input;

/// <summary>
/// Converts descriptor-driven operator text into a normalized typed Command
/// argument without executing a Command.
/// </summary>
public static class CommandArgumentInputParser
{
    public static CommandArgumentInputParseResult Parse(
        CommandDescriptor descriptor,
        string? input)
    {
        ArgumentNullException.ThrowIfNull(
            descriptor);

        if (descriptor.Argument is null)
        {
            return CommandArgumentInputParseResult.Parameterless();
        }

        DescriptorInputParseResult result =
            DescriptorInputParser.Parse(
                descriptor.Argument.Data,
                input);

        if (result.IsSuccess)
        {
            return CommandArgumentInputParseResult.Success(
                result.Value!);
        }

        return CommandArgumentInputParseResult.Failed(
            MapFailure(
                result.Failure),
            result.Failure
                == DescriptorInputFailure.UnsupportedDataDescriptor
                    ? "This Command argument data type is not supported for editing."
                    : result.Message);
    }

    private static CommandArgumentInputFailure MapFailure(
        DescriptorInputFailure failure)
    {
        return failure switch
        {
            DescriptorInputFailure.MissingInput =>
                CommandArgumentInputFailure.MissingInput,
            DescriptorInputFailure.InvalidFormat =>
                CommandArgumentInputFailure.InvalidFormat,
            DescriptorInputFailure.ValueOutsideRange =>
                CommandArgumentInputFailure.ValueOutsideRange,
            DescriptorInputFailure.UnsupportedDataDescriptor =>
                CommandArgumentInputFailure.UnsupportedDataDescriptor,
            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(failure),
                    failure,
                    "Unexpected descriptor input failure.")
        };
    }
}
