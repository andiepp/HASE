using System.Globalization;
using Hase.ProtocolExplorer.ScpiCharacterization;
using Hase.Transport.Serial;

namespace Hase.ProtocolExplorer.Scenarios;

internal sealed class Kel103ReadOnlyStateCharacterizationScenario
    : IParameterizedScenario
{
    private const int RequiredBaudRate = 115200;
    private readonly ISerialByteStreamFactory serialByteStreamFactory;

    public Kel103ReadOnlyStateCharacterizationScenario()
        : this(new SystemIoPortsSerialByteStreamFactory())
    {
    }

    internal Kel103ReadOnlyStateCharacterizationScenario(
        ISerialByteStreamFactory serialByteStreamFactory)
    {
        this.serialByteStreamFactory = serialByteStreamFactory
            ?? throw new ArgumentNullException(nameof(serialByteStreamFactory));
    }

    public string Name => "kel103-state-characterize";

    public void Execute(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ExecuteAsync(arguments).GetAwaiter().GetResult();
    }

    private async Task ExecuteAsync(IReadOnlyList<string> arguments)
    {
        if (arguments.Count != 4)
        {
            throw new ArgumentException(
                "KEL-103 state characterization requires a COM port, one fixed state candidate, the characterized cr terminator, and baud rate 115200.",
                nameof(arguments));
        }

        string portName = arguments[0];
        Kel103StateCandidate candidate = ParseCandidate(arguments[1]);
        ValidateTerminator(arguments[2]);
        int baudRate = ParseBaudRate(arguments[3]);
        var transportOptions = new SerialTransportOptions(portName, baudRate);

        WriteHeader(transportOptions, candidate);

        var characterizer = new Kel103ReadOnlyStateCharacterizer(
            serialByteStreamFactory);
        Kel103StateCharacterizationResult result = await characterizer
            .CharacterizeAsync(transportOptions, candidate)
            .ConfigureAwait(false);

        WriteResult(result);
    }

    internal static Kel103StateCandidate ParseCandidate(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        return value.ToLowerInvariant() switch
        {
            "mode" => Kel103StateCandidate.Mode,
            "input-state" => Kel103StateCandidate.InputState,
            "target-voltage" => Kel103StateCandidate.TargetVoltage,
            "target-current" => Kel103StateCandidate.TargetCurrent,
            "target-resistance" => Kel103StateCandidate.TargetResistance,
            "target-power" => Kel103StateCandidate.TargetPower,
            _ => throw new ArgumentException(
                $"'{value}' is not a supported KEL-103 state candidate. Use mode, input-state, target-voltage, target-current, target-resistance, or target-power.",
                nameof(value))
        };
    }

    internal static void ValidateTerminator(string value)
    {
        if (!string.Equals(value, "cr", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "KEL-103 state characterization requires the physically established cr command terminator.",
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
                "KEL-103 state characterization requires the physically established baud rate 115200.",
                nameof(value));
        }

        return baudRate;
    }

    private static void WriteHeader(
        SerialTransportOptions transportOptions,
        Kel103StateCandidate candidate)
    {
        const string title = "KEL-103 Read-Only State Characterization";
        Console.WriteLine(title);
        Console.WriteLine(new string('=', title.Length));
        Console.WriteLine();
        Console.WriteLine("Verify identity, then send exactly one selected fixed read-only state query.");
        Console.WriteLine();
        Console.WriteLine("Serial target          : External runtime argument");
        Console.WriteLine($"Baud rate             : {transportOptions.BaudRate}");
        Console.WriteLine($"Serial framing        : {transportOptions.DataBits} data, {transportOptions.Parity} parity, {transportOptions.StopBits} stop");
        Console.WriteLine($"Flow control          : {transportOptions.Handshake}");
        Console.WriteLine("Command terminator    : cr");
        Console.WriteLine("Response terminator   : lf");
        Console.WriteLine("Total timeout         : 3000 ms per query");
        Console.WriteLine("Maximum response      : 512 bytes per query");
        Console.WriteLine($"State candidate       : {candidate.ToArgumentValue()}");
        Console.WriteLine();
        Console.WriteLine("Opening the external serial target and sending two read-only queries.");
        Console.WriteLine();
    }

    private static void WriteResult(Kel103StateCharacterizationResult result)
    {
        Console.WriteLine("Read-only state characterization succeeded.");
        Console.WriteLine();
        Console.WriteLine($"Product identity      : {result.Identity.ProductIdentity}");
        Console.WriteLine($"Firmware              : {result.Identity.FirmwareVersion}");
        Console.WriteLine("Instrument serial     : <redacted>");
        Console.WriteLine("Identity verification : Succeeded");
        Console.WriteLine($"Identity duration     : {result.IdentityDuration.TotalMilliseconds:F1} ms");
        Console.WriteLine($"State category        : {result.Candidate.ToArgumentValue()}");
        Console.WriteLine($"State duration        : {result.StateDuration.TotalMilliseconds:F1} ms");

        if (result.UnrecognizedResponse is null)
        {
            Console.WriteLine($"Normalized result     : {result.NormalizedValue}");
            Console.WriteLine($"Result unit           : {result.UnitSymbol ?? "None"}");
            Console.WriteLine("State parsing         : Succeeded");
        }
        else
        {
            WriteUnrecognizedResponse(result.Candidate, result.UnrecognizedResponse);
        }

        Console.WriteLine();
        Console.WriteLine("The serial connection has been closed. No state-changing command was sent.");
    }

    private static void WriteUnrecognizedResponse(
        Kel103StateCandidate candidate,
        Kel103UnrecognizedStateResponseObservation observation)
    {
        Console.WriteLine("State parsing         : Unrecognized response format");
        Console.WriteLine($"Response characters   : {observation.ResponseCharacterCount}");

        if (candidate is Kel103StateCandidate.Mode or Kel103StateCandidate.InputState)
        {
            Console.WriteLine($"Observed token        : {observation.ObservedToken ?? "<not printable>"}");
            Console.WriteLine($"Contains whitespace   : {observation.ContainsWhitespace}");
            Console.WriteLine($"Unexpected characters : {observation.ContainsUnexpectedCharacters}");
            return;
        }

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
