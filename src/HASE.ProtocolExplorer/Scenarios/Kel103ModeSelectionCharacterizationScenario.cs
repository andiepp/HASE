using System.Globalization;
using Hase.ProtocolExplorer.ScpiCharacterization;
using Hase.Transport.Serial;

namespace Hase.ProtocolExplorer.Scenarios;

internal sealed class Kel103ModeSelectionCharacterizationScenario
    : IParameterizedScenario
{
    private const int RequiredBaudRate = 115200;
    private readonly ISerialByteStreamFactory serialByteStreamFactory;

    public Kel103ModeSelectionCharacterizationScenario()
        : this(new SystemIoPortsSerialByteStreamFactory())
    {
    }

    internal Kel103ModeSelectionCharacterizationScenario(
        ISerialByteStreamFactory serialByteStreamFactory)
    {
        this.serialByteStreamFactory = serialByteStreamFactory
            ?? throw new ArgumentNullException(nameof(serialByteStreamFactory));
    }

    public string Name => "kel103-mode-select-characterize";

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
                "KEL-103 mode-selection characterization requires a COM port, cv, cr, cw, or short, the characterized cr terminator, and baud rate 115200.",
                nameof(arguments));
        }

        Kel103ModeSelection destination = ParseDestination(arguments[1]);
        ValidateTerminator(arguments[2]);
        int baudRate = ParseBaudRate(arguments[3]);
        var transportOptions = new SerialTransportOptions(arguments[0], baudRate);

        WriteHeader(transportOptions, destination);

        var characterizer = new Kel103ModeSelectionCharacterizer(
            serialByteStreamFactory);
        Kel103ModeSelectionCharacterizationResult result = await characterizer
            .CharacterizeAsync(transportOptions, destination)
            .ConfigureAwait(false);

        WriteResult(result);
    }

    internal static Kel103ModeSelection ParseDestination(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        return value.ToLowerInvariant() switch
        {
            "cv" => Kel103ModeSelection.ConstantVoltage,
            "cr" => Kel103ModeSelection.ConstantResistance,
            "cw" => Kel103ModeSelection.ConstantPower,
            "short" => Kel103ModeSelection.ShortCircuit,
            _ => throw new ArgumentException(
                $"'{value}' is not a supported KEL-103 characterization destination. Use cv, cr, cw, or short.",
                nameof(value))
        };
    }

    internal static void ValidateTerminator(string value)
    {
        if (!string.Equals(value, "cr", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "KEL-103 mode-selection characterization requires the physically established cr command terminator.",
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
                "KEL-103 mode-selection characterization requires the physically established baud rate 115200.",
                nameof(value));
        }

        return baudRate;
    }

    private static void WriteHeader(
        SerialTransportOptions transportOptions,
        Kel103ModeSelection destination)
    {
        const string title = "KEL-103 Controlled Mode-Selection Characterization";
        Console.WriteLine(title);
        Console.WriteLine(new string('=', title.Length));
        Console.WriteLine();
        Console.WriteLine("Verify identity, input OFF, CC baseline, and unchanged setpoints while selecting and restoring one mode.");
        Console.WriteLine();
        Console.WriteLine("Serial target          : External runtime argument");
        Console.WriteLine($"Baud rate             : {transportOptions.BaudRate}");
        Console.WriteLine($"Serial framing        : {transportOptions.DataBits} data, {transportOptions.Parity} parity, {transportOptions.StopBits} stop");
        Console.WriteLine($"Flow control          : {transportOptions.Handshake}");
        Console.WriteLine("Command terminator    : cr");
        Console.WriteLine("Response terminator   : lf");
        Console.WriteLine("Total timeout         : 3000 ms per query");
        Console.WriteLine("Maximum response      : 512 bytes per query");
        Console.WriteLine($"Requested mode        : {destination.ToArgumentValue()}");
        Console.WriteLine();
        Console.WriteLine("The input must remain OFF. Automatic restoration occurs only after exact destination confirmation.");
        Console.WriteLine();
    }

    private static void WriteResult(Kel103ModeSelectionCharacterizationResult result)
    {
        Console.WriteLine("Controlled mode-selection characterization succeeded.");
        Console.WriteLine();
        Console.WriteLine($"Product identity      : {result.Identity.ProductIdentity}");
        Console.WriteLine($"Firmware              : {result.Identity.FirmwareVersion}");
        Console.WriteLine("Instrument serial     : <redacted>");
        Console.WriteLine("Identity verification : Succeeded");
        Console.WriteLine($"Identity duration     : {result.IdentityDuration.TotalMilliseconds:F1} ms");
        Console.WriteLine($"Requested mode        : {result.RequestedMode.ToArgumentValue()}");
        Console.WriteLine("Destination command   : Transmitted once");
        Console.WriteLine("Destination readback  : Confirmed");
        Console.WriteLine("Destination input     : OFF");
        Console.WriteLine($"Destination duration  : {result.DestinationDuration.TotalMilliseconds:F1} ms");
        Console.WriteLine("Restoration command   : Transmitted once");
        Console.WriteLine("Restoration readback  : CC confirmed");
        Console.WriteLine("Restoration input     : OFF");
        Console.WriteLine("Setpoint comparison   : Unchanged");
        Console.WriteLine($"Restoration duration  : {result.RestorationDuration.TotalMilliseconds:F1} ms");
        Console.WriteLine();
        Console.WriteLine("The serial connection has been closed. The authoritative mode is CC and input is OFF.");
    }
}
