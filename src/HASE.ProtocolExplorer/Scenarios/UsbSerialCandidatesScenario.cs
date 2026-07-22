using Hase.Transport.Discovery;

namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Displays one read-only snapshot of Windows USB serial candidates.
/// </summary>
internal sealed class UsbSerialCandidatesScenario
    : IParameterizedScenario
{
    public string Name =>
        "usb-serial-candidates";

    public void Execute(
        IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(
            arguments);

        if (arguments.Count != 0)
        {
            throw new ArgumentException(
                "USB serial candidate enumeration does not accept arguments.",
                nameof(arguments));
        }

        ExecuteAsync()
            .GetAwaiter()
            .GetResult();
    }

    private static async Task ExecuteAsync()
    {
        WriteHeader();

        IUsbSerialEndpointCandidateSource candidateSource =
            new WindowsUsbSerialEndpointCandidateSource();

        var candidates =
            new List<UsbSerialEndpointCandidate>();

        await foreach (
            UsbSerialEndpointCandidate candidate
            in candidateSource.EnumerateAsync())
        {
            candidates.Add(
                candidate);
        }

        if (candidates.Count == 0)
        {
            Console.WriteLine(
                "No serial candidates were reported by Windows.");

            return;
        }

        Console.WriteLine(
            $"Candidates reported    : {candidates.Count}");

        Console.WriteLine();

        for (
            int index = 0;
            index < candidates.Count;
            index++)
        {
            WriteCandidate(
                index + 1,
                candidates[index]);
        }

        Console.WriteLine(
            "Candidate enumeration completed.");

        Console.WriteLine();

        Console.WriteLine(
            "No serial ports were opened.");

        Console.WriteLine(
            "No endpoint identities were assigned.");
    }

    private static void WriteHeader()
    {
        const string title =
            "USB Serial Candidates";

        Console.WriteLine(
            title);

        Console.WriteLine(
            new string(
                '=',
                title.Length));

        Console.WriteLine();

        Console.WriteLine(
            "Enumerate one read-only snapshot of Windows serial candidates "
            + "and display their optional USB metadata.");

        Console.WriteLine();

        Console.WriteLine(
            "Platform             : Windows");

        Console.WriteLine(
            "Candidate source     : Win32_PnPEntity");

        Console.WriteLine(
            "Port access          : None");

        Console.WriteLine(
            "Protocol traffic     : None");

        Console.WriteLine(
            "Runtime attachment   : Never");

        Console.WriteLine();

        Console.WriteLine(
            "USB metadata identifies candidates only.");

        Console.WriteLine(
            "CompactBootstrapResponse.EndpointId remains authoritative.");

        Console.WriteLine();

        Console.WriteLine(
            "Enumerating serial candidates.");

        Console.WriteLine();
    }

    private static void WriteCandidate(
        int number,
        UsbSerialEndpointCandidate candidate)
    {
        Console.WriteLine(
            $"Candidate {number}");

        Console.WriteLine(
            new string(
                '-',
                $"Candidate {number}".Length));

        Console.WriteLine(
            $"Port                 : {candidate.PortName}");

        Console.WriteLine(
            $"VID                  : {FormatIdentifier(candidate.VendorId)}");

        Console.WriteLine(
            $"PID                  : {FormatIdentifier(candidate.ProductId)}");

        Console.WriteLine(
            $"Product              : {FormatOptional(candidate.ProductName)}");

        Console.WriteLine(
            $"Manufacturer         : {FormatOptional(candidate.ManufacturerName)}");

        Console.WriteLine(
            $"USB serial number    : {FormatOptional(candidate.SerialNumber)}");

        Console.WriteLine();
    }

    private static string FormatIdentifier(
        ushort? value)
    {
        return value.HasValue
            ? $"0x{value.Value:X4}"
            : "Not reported";
    }

    private static string FormatOptional(
        string? value)
    {
        return value
            ?? "Not reported";
    }
}