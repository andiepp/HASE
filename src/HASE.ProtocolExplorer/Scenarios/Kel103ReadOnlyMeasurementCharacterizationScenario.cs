using System.Globalization;
using Hase.ProtocolExplorer.ScpiCharacterization;
using Hase.Transport.Serial;

namespace Hase.ProtocolExplorer.Scenarios;

internal sealed class Kel103ReadOnlyMeasurementCharacterizationScenario
    : IParameterizedScenario
{
    private const int RequiredBaudRate = 115200;
    private readonly ISerialByteStreamFactory serialByteStreamFactory;

    public Kel103ReadOnlyMeasurementCharacterizationScenario()
        : this(new SystemIoPortsSerialByteStreamFactory())
    {
    }

    internal Kel103ReadOnlyMeasurementCharacterizationScenario(
        ISerialByteStreamFactory serialByteStreamFactory)
    {
        this.serialByteStreamFactory = serialByteStreamFactory
            ?? throw new ArgumentNullException(nameof(serialByteStreamFactory));
    }

    public string Name => "kel103-measure-characterize";

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
                "KEL-103 measurement characterization requires a COM port, one candidate (voltage, current, or power), the characterized cr terminator, and baud rate 115200.",
                nameof(arguments));
        }

        string portName = arguments[0];
        Kel103MeasurementCandidate candidate = ParseCandidate(arguments[1]);
        ValidateTerminator(arguments[2]);
        int baudRate = ParseBaudRate(arguments[3]);
        var transportOptions = new SerialTransportOptions(portName, baudRate);

        WriteHeader(transportOptions, candidate);

        var characterizer = new Kel103ReadOnlyMeasurementCharacterizer(
            serialByteStreamFactory);
        Kel103MeasurementCharacterizationResult result = await characterizer
            .CharacterizeAsync(transportOptions, candidate)
            .ConfigureAwait(false);

        WriteResult(result);
    }

    internal static Kel103MeasurementCandidate ParseCandidate(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        return value.ToLowerInvariant() switch
        {
            "voltage" => Kel103MeasurementCandidate.Voltage,
            "current" => Kel103MeasurementCandidate.Current,
            "power" => Kel103MeasurementCandidate.Power,
            _ => throw new ArgumentException(
                $"'{value}' is not a supported KEL-103 measurement candidate. Use voltage, current, or power.",
                nameof(value))
        };
    }

    internal static void ValidateTerminator(string value)
    {
        if (!string.Equals(value, "cr", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "KEL-103 measurement characterization requires the physically established cr command terminator.",
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
                "KEL-103 measurement characterization requires the physically established baud rate 115200.",
                nameof(value));
        }

        return baudRate;
    }

    internal static void WriteHeader(
        SerialTransportOptions transportOptions,
        Kel103MeasurementCandidate candidate)
    {
        const string title = "KEL-103 Read-Only Measurement Characterization";
        Console.WriteLine(title);
        Console.WriteLine(new string('=', title.Length));
        Console.WriteLine();
        Console.WriteLine("Verify identity, then send exactly one selected fixed read-only measurement query.");
        Console.WriteLine();
        Console.WriteLine("Serial target          : External runtime argument");
        Console.WriteLine($"Baud rate             : {transportOptions.BaudRate}");
        Console.WriteLine($"Serial framing        : {transportOptions.DataBits} data, {transportOptions.Parity} parity, {transportOptions.StopBits} stop");
        Console.WriteLine($"Flow control          : {transportOptions.Handshake}");
        Console.WriteLine("Command terminator    : cr");
        Console.WriteLine("Response terminator   : lf");
        Console.WriteLine("Total timeout         : 3000 ms per query");
        Console.WriteLine("Maximum response      : 512 bytes per query");
        Console.WriteLine($"Measurement candidate : {candidate.ToArgumentValue()}");
        Console.WriteLine();
        Console.WriteLine("Opening the external serial target and sending two read-only queries.");
        Console.WriteLine();
    }

    private static void WriteResult(Kel103MeasurementCharacterizationResult result)
    {
        Console.WriteLine("Read-only measurement characterization succeeded.");
        Console.WriteLine();
        Console.WriteLine($"Product identity      : {result.Identity.ProductIdentity}");
        Console.WriteLine($"Firmware              : {result.Identity.FirmwareVersion}");
        Console.WriteLine("Instrument serial     : <redacted>");
        Console.WriteLine("Identity verification : Succeeded");
        Console.WriteLine($"Identity duration     : {result.IdentityDuration.TotalMilliseconds:F1} ms");
        Console.WriteLine($"Measurement category  : {result.Candidate.ToArgumentValue()}");
        Console.WriteLine($"Measurement value     : {result.Value.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"Measurement unit      : {result.UnitSymbol}");
        Console.WriteLine($"Measurement duration  : {result.MeasurementDuration.TotalMilliseconds:F1} ms");
        Console.WriteLine("Measurement parsing   : Succeeded");
        Console.WriteLine();
        Console.WriteLine("The serial connection has been closed. No state-changing command was sent.");
    }
}
