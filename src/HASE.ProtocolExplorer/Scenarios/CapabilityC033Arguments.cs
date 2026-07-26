using System.Globalization;

namespace Hase.ProtocolExplorer.Scenarios;

internal sealed record CapabilityC033Arguments
{
    private const int DefaultBaudRate =
        115200;

    private const int DefaultVerificationTimeoutSeconds =
        3;

    private CapabilityC033Arguments(
        int baudRate,
        TimeSpan verificationTimeout)
    {
        BaudRate =
            baudRate;

        VerificationTimeout =
            verificationTimeout;
    }

    public int BaudRate
    {
        get;
    }

    public TimeSpan VerificationTimeout
    {
        get;
    }

    public static CapabilityC033Arguments Parse(
        IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(
            arguments);

        if (arguments.Count > 2)
        {
            throw new ArgumentException(
                "Capability C-033 accepts an optional baud rate and an "
                + "optional verification timeout in seconds.",
                nameof(arguments));
        }

        int baudRate =
            arguments.Count >= 1
                ? ParsePositiveInteger(
                    arguments[0],
                    "baud rate")
                : DefaultBaudRate;

        int verificationTimeoutSeconds =
            arguments.Count == 2
                ? ParsePositiveInteger(
                    arguments[1],
                    "verification timeout")
                : DefaultVerificationTimeoutSeconds;

        return new CapabilityC033Arguments(
            baudRate,
            TimeSpan.FromSeconds(
                verificationTimeoutSeconds));
    }

    private static int ParsePositiveInteger(
        string value,
        string fieldName)
    {
        if (!int.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int parsedValue)
            || parsedValue <= 0)
        {
            throw new ArgumentException(
                $"'{value}' is not a valid positive {fieldName}.",
                nameof(value));
        }

        return parsedValue;
    }
}
