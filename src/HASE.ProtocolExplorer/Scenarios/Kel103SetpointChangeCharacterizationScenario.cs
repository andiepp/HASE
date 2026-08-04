using System.Globalization;
using Hase.ProtocolExplorer.ScpiCharacterization;
using Hase.Transport.Serial;

namespace Hase.ProtocolExplorer.Scenarios;

internal sealed class Kel103SetpointChangeCharacterizationScenario
    : IParameterizedScenario
{
    private const int RequiredBaudRate = 115200;
    private readonly ISerialByteStreamFactory serialByteStreamFactory;

    public Kel103SetpointChangeCharacterizationScenario()
        : this(new SystemIoPortsSerialByteStreamFactory())
    {
    }

    internal Kel103SetpointChangeCharacterizationScenario(
        ISerialByteStreamFactory serialByteStreamFactory)
    {
        this.serialByteStreamFactory = serialByteStreamFactory
            ?? throw new ArgumentNullException(nameof(serialByteStreamFactory));
    }

    public string Name => "kel103-setpoint-change-characterize";

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
                "KEL-103 changed-value setpoint characterization requires a COM port, one fixed target, the characterized cr terminator, and baud rate 115200.",
                nameof(arguments));
        }

        Kel103StateCandidate candidate = ParseCandidate(arguments[1]);
        ValidateTerminator(arguments[2]);
        int baudRate = ParseBaudRate(arguments[3]);
        var transportOptions = new SerialTransportOptions(arguments[0], baudRate);

        WriteHeader(transportOptions, candidate);

        var characterizer = new Kel103SetpointChangeCharacterizer(
            serialByteStreamFactory);
        Kel103SetpointChangeCharacterizationResult result = await characterizer
            .CharacterizeAsync(transportOptions, candidate)
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
                $"'{value}' is not a supported KEL-103 setpoint target. Use target-voltage, target-current, target-resistance, or target-power.",
                nameof(value))
        };
    }

    internal static void ValidateTerminator(string value)
    {
        if (!string.Equals(value, "cr", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "KEL-103 changed-value setpoint characterization requires the physically established cr command terminator.",
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
                "KEL-103 changed-value setpoint characterization requires the physically established baud rate 115200.",
                nameof(value));
        }

        return baudRate;
    }

    private static void WriteHeader(
        SerialTransportOptions transportOptions,
        Kel103StateCandidate candidate)
    {
        const string title = "KEL-103 Input-OFF Changed-Value Setpoint Characterization";
        Console.WriteLine(title);
        Console.WriteLine(new string('=', title.Length));
        Console.WriteLine();
        Console.WriteLine("Derive one bounded response-scale change, verify it, restore the original setpoint, and restore CC when required.");
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
        Console.WriteLine("Candidate derivation  : One interior response-scale quantum");
        Console.WriteLine();
        Console.WriteLine("The input and external supply output must remain OFF. No setpoint or bound value is displayed.");
        Console.WriteLine();
    }

    private static void WriteResult(Kel103SetpointChangeCharacterizationResult result)
    {
        Console.WriteLine("Changed-value setpoint characterization succeeded.");
        Console.WriteLine();
        Console.WriteLine($"Product identity      : {result.Identity.ProductIdentity}");
        Console.WriteLine($"Firmware              : {result.Identity.FirmwareVersion}");
        Console.WriteLine("Instrument serial     : <redacted>");
        Console.WriteLine("Identity verification : Succeeded");
        Console.WriteLine($"Identity duration     : {result.IdentityDuration.TotalMilliseconds:F1} ms");
        Console.WriteLine($"Target category       : {result.Candidate.ToArgumentValue()}");
        Console.WriteLine("Changed setter        : Transmitted once and confirmed");
        Console.WriteLine("Changed input         : OFF");
        Console.WriteLine("Expected mode         : Confirmed");
        Console.WriteLine("Unrelated targets     : Unchanged");
        Console.WriteLine($"Changed verification  : {result.ChangedValueVerificationDuration.TotalMilliseconds:F1} ms");
        Console.WriteLine("Setpoint restoration  : Transmitted once and confirmed");
        Console.WriteLine("Restoration input     : OFF");
        Console.WriteLine("Original targets      : All confirmed");
        Console.WriteLine($"Restoration duration  : {result.SetpointRestorationDuration.TotalMilliseconds:F1} ms");
        Console.WriteLine($"CC restoration        : {(result.ModeRestorationCommandTransmitted ? "Transmitted once and confirmed" : "Not required and not transmitted")}");
        if (result.ModeRestorationCommandTransmitted)
        {
            Console.WriteLine($"CC restoration time   : {result.ModeRestorationDuration.TotalMilliseconds:F1} ms");
        }
        Console.WriteLine();
        Console.WriteLine("The serial connection has been closed in authoritative CC/OFF state with all original setpoints restored.");
    }
}
