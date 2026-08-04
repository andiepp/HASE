using System.Globalization;
using Hase.ProtocolExplorer.ScpiCharacterization;
using Hase.Transport.Serial;

namespace Hase.ProtocolExplorer.Scenarios;

internal sealed class Kel103ReadOnlyLimitCharacterizationScenario
    : IParameterizedScenario
{
    private const int RequiredBaudRate = 115200;
    private readonly ISerialByteStreamFactory serialByteStreamFactory;

    public Kel103ReadOnlyLimitCharacterizationScenario()
        : this(new SystemIoPortsSerialByteStreamFactory())
    {
    }

    internal Kel103ReadOnlyLimitCharacterizationScenario(
        ISerialByteStreamFactory serialByteStreamFactory)
    {
        this.serialByteStreamFactory = serialByteStreamFactory
            ?? throw new ArgumentNullException(nameof(serialByteStreamFactory));
    }

    public string Name => "kel103-limit-characterize";

    public void Execute(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ExecuteAsync(arguments).GetAwaiter().GetResult();
    }

    private async Task ExecuteAsync(IReadOnlyList<string> arguments)
    {
        if (arguments.Count != 5)
        {
            throw new ArgumentException(
                "KEL-103 limit characterization requires a COM port, one fixed target, lower or upper, the characterized cr terminator, and baud rate 115200.",
                nameof(arguments));
        }

        string portName = arguments[0];
        Kel103StateCandidate candidate = ParseCandidate(arguments[1]);
        Kel103SetpointLimit limit = ParseLimit(arguments[2]);
        ValidateTerminator(arguments[3]);
        int baudRate = ParseBaudRate(arguments[4]);
        var transportOptions = new SerialTransportOptions(portName, baudRate);

        WriteHeader(transportOptions, candidate, limit);

        var characterizer = new Kel103ReadOnlyLimitCharacterizer(
            serialByteStreamFactory);
        Kel103LimitCharacterizationResult result = await characterizer
            .CharacterizeAsync(transportOptions, candidate, limit)
            .ConfigureAwait(false);

        WriteResult(result);
    }

    internal static Kel103StateCandidate ParseCandidate(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        return value.ToLowerInvariant() switch
        {
            "target-voltage" => Kel103StateCandidate.TargetVoltage,
            "target-current" => Kel103StateCandidate.TargetCurrent,
            "target-resistance" => Kel103StateCandidate.TargetResistance,
            "target-power" => Kel103StateCandidate.TargetPower,
            _ => throw new ArgumentException(
                $"'{value}' is not a supported KEL-103 limit target. Use target-voltage, target-current, target-resistance, or target-power.",
                nameof(value))
        };
    }

    internal static Kel103SetpointLimit ParseLimit(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        return value.ToLowerInvariant() switch
        {
            "lower" => Kel103SetpointLimit.Lower,
            "upper" => Kel103SetpointLimit.Upper,
            _ => throw new ArgumentException(
                $"'{value}' is not a supported KEL-103 limit selector. Use lower or upper.",
                nameof(value))
        };
    }

    internal static void ValidateTerminator(string value)
    {
        if (!string.Equals(value, "cr", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "KEL-103 limit characterization requires the physically established cr command terminator.",
                nameof(value));
        }
    }

    internal static int ParseBaudRate(string value)
    {
        if (!int.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int baudRate)
            || baudRate != RequiredBaudRate)
        {
            throw new ArgumentException(
                "KEL-103 limit characterization requires the physically established baud rate 115200.",
                nameof(value));
        }

        return baudRate;
    }

    private static void WriteHeader(
        SerialTransportOptions transportOptions,
        Kel103StateCandidate candidate,
        Kel103SetpointLimit limit)
    {
        const string title = "KEL-103 Read-Only Setpoint-Limit Characterization";
        Console.WriteLine(title);
        Console.WriteLine(new string('=', title.Length));
        Console.WriteLine();
        Console.WriteLine("Verify identity, then send exactly one selected fixed read-only limit query.");
        Console.WriteLine();
        Console.WriteLine("Serial target          : External runtime argument");
        Console.WriteLine($"Baud rate             : {transportOptions.BaudRate}");
        Console.WriteLine($"Serial framing        : {transportOptions.DataBits} data, {transportOptions.Parity} parity, {transportOptions.StopBits} stop");
        Console.WriteLine($"Flow control          : {transportOptions.Handshake}");
        Console.WriteLine("Command terminator    : cr");
        Console.WriteLine("Response terminator   : lf");
        Console.WriteLine("Total timeout         : 3000 ms per query");
        Console.WriteLine("Maximum response      : 512 bytes per query");
        Console.WriteLine($"Target category       : {candidate.ToArgumentValue()}");
        Console.WriteLine($"Limit selector        : {limit.ToArgumentValue()}");
        Console.WriteLine();
        Console.WriteLine("Opening the external serial target and sending two read-only queries.");
        Console.WriteLine();
    }

    private static void WriteResult(Kel103LimitCharacterizationResult result)
    {
        Console.WriteLine("Read-only setpoint-limit characterization succeeded.");
        Console.WriteLine();
        Console.WriteLine($"Product identity      : {result.Identity.ProductIdentity}");
        Console.WriteLine($"Firmware              : {result.Identity.FirmwareVersion}");
        Console.WriteLine("Instrument serial     : <redacted>");
        Console.WriteLine("Identity verification : Succeeded");
        Console.WriteLine($"Identity duration     : {result.IdentityDuration.TotalMilliseconds:F1} ms");
        Console.WriteLine($"Target category       : {result.Candidate.ToArgumentValue()}");
        Console.WriteLine($"Limit selector        : {result.Limit.ToArgumentValue()}");
        Console.WriteLine($"Limit duration        : {result.LimitDuration.TotalMilliseconds:F1} ms");

        if (result.UnrecognizedResponse is null)
        {
            Console.WriteLine($"Normalized result     : {result.NormalizedValue}");
            Console.WriteLine($"Result unit           : {result.UnitSymbol}");
            Console.WriteLine("Limit parsing         : Succeeded");
        }
        else
        {
            WriteUnrecognizedResponse(result.UnrecognizedResponse);
        }

        Console.WriteLine();
        Console.WriteLine("The serial connection has been closed. No state-changing command was sent.");
    }

    private static void WriteUnrecognizedResponse(
        Kel103UnrecognizedStateResponseObservation observation)
    {
        Console.WriteLine("Limit parsing         : Unrecognized response format");
        Console.WriteLine($"Response characters   : {observation.ResponseCharacterCount}");
        Console.WriteLine($"Leading sign          : {observation.HasLeadingSign}");
        Console.WriteLine($"Integer digits        : {observation.IntegerDigitCount}");
        Console.WriteLine($"Decimal separator     : {observation.DecimalSeparator}");
        Console.WriteLine($"Fractional digits     : {observation.FractionalDigitCount}");
        Console.WriteLine($"Observed suffix       : {observation.Suffix}");
        Console.WriteLine($"Contains whitespace   : {observation.ContainsWhitespace}");
        Console.WriteLine($"Unexpected characters : {observation.ContainsUnexpectedCharacters}");
        Console.WriteLine("Numeric value         : <redacted>");
    }
}
