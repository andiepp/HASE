using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Data;

namespace Hase.Runtime.Northbound;

/// <summary>
/// Validates one Command argument against the authoritative Command descriptor.
/// </summary>
internal static class RuntimeHostCommandArgumentValidator
{
    public static bool IsValid(
        CommandDescriptor command,
        object? argument)
    {
        ArgumentNullException.ThrowIfNull(command);

        CommandArgumentDescriptor? argumentDescriptor =
            command.Argument;

        if (argumentDescriptor is null)
        {
            return argument is null;
        }

        if (argument is null)
        {
            return false;
        }

        return argumentDescriptor.Data switch
        {
            BooleanDataDescriptor =>
                argument is bool,

            StringDataDescriptor =>
                argument is string,

            NumericDataDescriptor =>
                argument is int
                or long
                or double,

            ByteArrayDataDescriptor =>
                argument is ByteArrayValue,

            _ =>
                false
        };
    }
}
