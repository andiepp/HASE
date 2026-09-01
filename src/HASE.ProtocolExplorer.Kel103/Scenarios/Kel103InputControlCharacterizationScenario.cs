using System.Globalization;
using Hase.ProtocolExplorer.ScpiCharacterization;
using Hase.Transport.Serial;

namespace Hase.ProtocolExplorer.Scenarios;

internal sealed class Kel103InputControlCharacterizationScenario : IParameterizedScenario
{
    private const int RequiredBaudRate = 115200;
    private readonly ISerialByteStreamFactory serialByteStreamFactory;

    public Kel103InputControlCharacterizationScenario()
        : this(new SystemIoPortsSerialByteStreamFactory())
    {
    }

    internal Kel103InputControlCharacterizationScenario(
        ISerialByteStreamFactory serialByteStreamFactory)
    {
        this.serialByteStreamFactory = serialByteStreamFactory
            ?? throw new ArgumentNullException(nameof(serialByteStreamFactory));
    }

    public string Name => "kel103-input-control-characterize";

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
                "KEL-103 input-control characterization requires a COM port, explicit confirmation true, the characterized cr terminator, and baud rate 115200.",
                nameof(arguments));
        }

        bool confirmation = ParseConfirmation(arguments[1]);
        ValidateTerminator(arguments[2]);
        int baudRate = ParseBaudRate(arguments[3]);
        var options = new SerialTransportOptions(arguments[0], baudRate);
        WriteHeader(options);

        var characterizer = new Kel103InputControlCharacterizer(serialByteStreamFactory);
        Kel103InputControlCharacterizationResult result = await characterizer
            .CharacterizeAsync(options, confirmation).ConfigureAwait(false);
        WriteResult(result);
    }

    internal static bool ParseConfirmation(string value)
    {
        if (!bool.TryParse(value, out bool confirmation) || !confirmation)
        {
            throw new ArgumentException(
                "KEL-103 input activation requires the explicit Boolean confirmation true.",
                nameof(value));
        }

        return confirmation;
    }

    internal static void ValidateTerminator(string value)
    {
        if (!string.Equals(value, "cr", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "KEL-103 input-control characterization requires the physically established cr command terminator.",
                nameof(value));
        }
    }

    internal static int ParseBaudRate(string value)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int baudRate)
            || baudRate != RequiredBaudRate)
        {
            throw new ArgumentException(
                "KEL-103 input-control characterization requires the physically established baud rate 115200.",
                nameof(value));
        }

        return baudRate;
    }

    private static void WriteHeader(SerialTransportOptions options)
    {
        const string title = "KEL-103 Controlled Input Activation/Deactivation Characterization";
        Console.WriteLine(title);
        Console.WriteLine(new string('=', title.Length));
        Console.WriteLine();
        Console.WriteLine("Verify identity and CC/OFF state, activate once, verify unchanged state, then deactivate once and verify restoration.");
        Console.WriteLine();
        Console.WriteLine("Serial target          : External runtime argument");
        Console.WriteLine($"Baud rate             : {options.BaudRate}");
        Console.WriteLine($"Serial framing        : {options.DataBits} data, {options.Parity} parity, {options.StopBits} stop");
        Console.WriteLine($"Flow control          : {options.Handshake}");
        Console.WriteLine("Command terminator    : cr");
        Console.WriteLine("Response terminator   : lf");
        Console.WriteLine("Total timeout         : 3000 ms per query");
        Console.WriteLine("Maximum response      : 512 bytes per query");
        Console.WriteLine("Activation confirmed  : Explicit true");
        Console.WriteLine();
        Console.WriteLine("The external supply output must remain OFF. An uncertain activation outcome is not automatically deactivated.");
        Console.WriteLine();
    }

    private static void WriteResult(Kel103InputControlCharacterizationResult result)
    {
        Console.WriteLine("Controlled input activation/deactivation characterization succeeded.");
        Console.WriteLine();
        Console.WriteLine($"Product identity      : {result.Identity.ProductIdentity}");
        Console.WriteLine($"Firmware              : {result.Identity.FirmwareVersion}");
        Console.WriteLine("Instrument serial     : <redacted>");
        Console.WriteLine("Identity verification : Succeeded");
        Console.WriteLine($"Identity duration     : {result.IdentityDuration.TotalMilliseconds:F1} ms");
        Console.WriteLine("Activation command    : Transmitted once");
        Console.WriteLine("Activation readback   : ON confirmed");
        Console.WriteLine("Activated mode        : CC confirmed");
        Console.WriteLine("Activated setpoints   : Unchanged");
        Console.WriteLine($"Activation duration   : {result.ActivationDuration.TotalMilliseconds:F1} ms");
        Console.WriteLine("Deactivation command  : Transmitted once");
        Console.WriteLine("Deactivation readback : OFF confirmed");
        Console.WriteLine("Final mode            : CC confirmed");
        Console.WriteLine("Final setpoints       : Unchanged");
        Console.WriteLine($"Deactivation duration : {result.DeactivationDuration.TotalMilliseconds:F1} ms");
        Console.WriteLine();
        Console.WriteLine("The serial connection has been closed in authoritative CC/OFF state with all original setpoints confirmed.");
    }
}
