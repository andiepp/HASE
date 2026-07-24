using System.Globalization;

namespace Hase.ProtocolExplorer.Scenarios;

internal enum CapabilityC027EndpointFamily
{
    Esp32,
    Arduino
}

internal sealed record CapabilityC027Arguments
{
    private const int DefaultBaudRate =
        115200;

    private const int DefaultVerificationTimeoutSeconds =
        3;

    private CapabilityC027Arguments(
        CapabilityC027EndpointFamily endpointFamily,
        string? esp32Host,
        int baudRate,
        TimeSpan verificationTimeout)
    {
        EndpointFamily =
            endpointFamily;

        Esp32Host =
            esp32Host;

        BaudRate =
            baudRate;

        VerificationTimeout =
            verificationTimeout;
    }

    public CapabilityC027EndpointFamily EndpointFamily
    {
        get;
    }

    public string? Esp32Host
    {
        get;
    }

    public int BaudRate
    {
        get;
    }

    public TimeSpan VerificationTimeout
    {
        get;
    }

    public static CapabilityC027Arguments Parse(
        IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(
            arguments);

        if (arguments.Count == 0)
        {
            throw new ArgumentException(
                "Capability C-027 requires an endpoint family: "
                + "'esp32' or 'arduino'.",
                nameof(arguments));
        }

        if (string.Equals(
                arguments[0],
                "esp32",
                StringComparison.OrdinalIgnoreCase))
        {
            if (arguments.Count != 2
                || string.IsNullOrWhiteSpace(
                    arguments[1]))
            {
                throw new ArgumentException(
                    "Capability C-027 ESP32 validation requires exactly "
                    + "one host name or IP address.",
                    nameof(arguments));
            }

            return ForEsp32(
                arguments[1]);
        }

        if (string.Equals(
                arguments[0],
                "arduino",
                StringComparison.OrdinalIgnoreCase))
        {
            if (arguments.Count > 3)
            {
                throw new ArgumentException(
                    "Capability C-027 Arduino validation accepts an optional "
                    + "baud rate and an optional verification timeout in "
                    + "seconds.",
                    nameof(arguments));
            }

            int baudRate =
                arguments.Count >= 2
                    ? ParsePositiveInteger(
                        arguments[1],
                        "baud rate")
                    : DefaultBaudRate;

            int verificationTimeoutSeconds =
                arguments.Count == 3
                    ? ParsePositiveInteger(
                        arguments[2],
                        "verification timeout")
                    : DefaultVerificationTimeoutSeconds;

            return ForArduino(
                baudRate,
                TimeSpan.FromSeconds(
                    verificationTimeoutSeconds));
        }

        throw new ArgumentException(
            $"Unknown Capability C-027 endpoint family '{arguments[0]}'. "
            + "Expected 'esp32' or 'arduino'.",
            nameof(arguments));
    }

    private static CapabilityC027Arguments ForEsp32(
        string host)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            host);

        return new CapabilityC027Arguments(
            CapabilityC027EndpointFamily.Esp32,
            host,
            baudRate: 0,
            verificationTimeout:
                TimeSpan.Zero);
    }

    private static CapabilityC027Arguments ForArduino(
        int baudRate,
        TimeSpan verificationTimeout)
    {
        if (baudRate <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(baudRate));
        }

        if (verificationTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(verificationTimeout));
        }

        return new CapabilityC027Arguments(
            CapabilityC027EndpointFamily.Arduino,
            esp32Host: null,
            baudRate,
            verificationTimeout);
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