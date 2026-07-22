using System.Globalization;
using Hase.CompactProtocol;
using Hase.Core.Domain.Descriptors;
using Hase.Runtime.Transport.Discovery;
using Hase.Transport.Discovery;

namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Discovers and authoritatively verifies a physical Arduino Uno compact
/// endpoint without attaching it to runtime state.
/// </summary>
internal sealed class CapabilityC023Scenario
    : IParameterizedScenario
{
    private const int DefaultBaudRate =
        115200;

    private const int DefaultVerificationTimeoutSeconds =
        3;

    private const ushort ArduinoVendorId =
        0x2341;

    private const ushort ArduinoUnoProductId =
        0x0043;

    public string Name =>
        "c023";

    public void Execute(
        IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(
            arguments);

        (int baudRate, TimeSpan verificationTimeout) =
            ParseArguments(
                arguments);

        ExecuteAsync(
                baudRate,
                verificationTimeout)
            .GetAwaiter()
            .GetResult();
    }

    private static async Task ExecuteAsync(
        int baudRate,
        TimeSpan verificationTimeout)
    {
        EndpointDescriptorDefinition descriptorDefinition =
            PhysicalArduinoUnoCompactDescriptorFactory
                .CreateDefinition();

        var descriptorRepository =
            new InMemoryEndpointDescriptorRepository(
                [
                    new KeyValuePair<
                        DescriptorReference,
                        EndpointDescriptorDefinition>(
                            PhysicalArduinoUnoCompactDescriptorFactory
                                .DescriptorReference,
                            descriptorDefinition)
                ]);

        var candidateFilter =
            new UsbSerialEndpointMetadataFilter(
                vendorId:
                    ArduinoVendorId,
                productId:
                    ArduinoUnoProductId);

        UsbSerialEndpointDiscoveryService discoveryService =
            WindowsUsbSerialEndpointDiscovery.Create(
                descriptorRepository,
                candidateFilter);

        var discoveryOptions =
            new UsbSerialEndpointDiscoveryOptions(
                baudRate,
                verificationTimeout);

        WriteHeader(
            discoveryOptions);

        UsbSerialEndpointDiscoveryResult result =
            await discoveryService.DiscoverAsync(
                discoveryOptions);

        WriteCandidateResults(
            result.CandidateResults);

        WriteVerifiedInventory(
            result.VerifiedEndpoints);

        if (result.CandidateResults.Count == 0)
        {
            throw new InvalidOperationException(
                "Windows did not report a USB serial candidate matching "
                + "Arduino VID 0x2341 and PID 0x0043.");
        }

        if (result.VerifiedEndpoints.Count == 0)
        {
            throw new InvalidOperationException(
                "No matching USB serial candidate completed authoritative "
                + "Compact Serial Protocol bootstrap and exact descriptor "
                + "resolution.");
        }

        Console.WriteLine(
            "C-023 USB serial discovery completed successfully.");

        Console.WriteLine();

        Console.WriteLine(
            "Runtime attachment     : None");

        Console.WriteLine(
            "Runtime mutation       : None");

        Console.WriteLine(
            "Verification streams   : Disposed");
    }

    internal static (int BaudRate, TimeSpan VerificationTimeout)
        ParseArguments(
            IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(
            arguments);

        if (arguments.Count > 2)
        {
            throw new ArgumentException(
                "Capability C-023 accepts an optional baud rate and an "
                + "optional verification timeout in seconds.",
                nameof(arguments));
        }

        int baudRate =
            arguments.Count >= 1
                ? ParsePositiveInteger(
                    arguments[0],
                    "baud rate")
                : DefaultBaudRate;

        int timeoutSeconds =
            arguments.Count == 2
                ? ParsePositiveInteger(
                    arguments[1],
                    "verification timeout")
                : DefaultVerificationTimeoutSeconds;

        return (
            baudRate,
            TimeSpan.FromSeconds(
                timeoutSeconds));
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

    private static void WriteHeader(
        UsbSerialEndpointDiscoveryOptions options)
    {
        const string title =
            "Capability C-023";

        Console.WriteLine(
            title);

        Console.WriteLine(
            new string(
                '=',
                title.Length));

        Console.WriteLine();

        Console.WriteLine(
            "Discover Windows USB serial candidates, filter the physical "
            + "Arduino Uno candidate by USB metadata, and verify it through "
            + "authoritative Compact Serial Protocol bootstrap.");

        Console.WriteLine();

        Console.WriteLine(
            "Platform             : Windows");

        Console.WriteLine(
            "Candidate source     : Win32_PnPEntity");

        Console.WriteLine(
            "Candidate filter     : VID 0x2341, PID 0x0043");

        Console.WriteLine(
            $"Baud rate            : {options.BaudRate}");

        Console.WriteLine(
            $"Verification timeout : {options.VerificationTimeout}");

        Console.WriteLine(
            "Protocol             : Compact Serial Protocol V1");

        Console.WriteLine(
            "Descriptor source    : Host repository");

        Console.WriteLine(
            $"Descriptor reference : "
            + $"{PhysicalArduinoUnoCompactDescriptorFactory.DescriptorReference.Id.Value} "
            + $"v{PhysicalArduinoUnoCompactDescriptorFactory.DescriptorReference.Version}");

        Console.WriteLine(
            "Runtime attachment   : Never");

        Console.WriteLine();

        Console.WriteLine(
            "USB metadata identifies candidates only.");

        Console.WriteLine(
            "CompactBootstrapResponse.EndpointId remains authoritative.");

        Console.WriteLine();

        Console.WriteLine(
            "Beginning sequential candidate discovery and verification.");

        Console.WriteLine();
    }

    private static void WriteCandidateResults(
        IReadOnlyList<UsbSerialEndpointVerificationResult> results)
    {
        Console.WriteLine(
            $"Candidate outcomes     : {results.Count}");

        Console.WriteLine();

        for (
            int index = 0;
            index < results.Count;
            index++)
        {
            UsbSerialEndpointVerificationResult result =
                results[index];

            string heading =
                $"Candidate outcome {index + 1}";

            Console.WriteLine(
                heading);

            Console.WriteLine(
                new string(
                    '-',
                    heading.Length));

            Console.WriteLine(
                $"Port                  : {result.Candidate.PortName}");

            Console.WriteLine(
                $"VID                   : {FormatIdentifier(result.Candidate.VendorId)}");

            Console.WriteLine(
                $"PID                   : {FormatIdentifier(result.Candidate.ProductId)}");

            Console.WriteLine(
                $"Product               : {FormatOptional(result.Candidate.ProductName)}");

            Console.WriteLine(
                $"USB serial number     : {FormatOptional(result.Candidate.SerialNumber)}");

            switch (result)
            {
                case VerifiedUsbSerialEndpoint verified:
                    Console.WriteLine(
                        "Outcome               : Verified");

                    Console.WriteLine(
                        $"Authoritative endpoint: {verified.EndpointId.Value}");

                    Console.WriteLine(
                        $"Descriptor reference  : "
                        + $"{verified.DescriptorReference.Id.Value} "
                        + $"v{verified.DescriptorReference.Version}");
                    break;

                case RejectedUsbSerialEndpointCandidate rejected:
                    Console.WriteLine(
                        "Outcome               : Rejected");

                    Console.WriteLine(
                        $"Failure               : {rejected.Failure}");

                    Console.WriteLine(
                        $"Detail                : {rejected.Detail}");
                    break;
            }

            Console.WriteLine();
        }
    }

    private static void WriteVerifiedInventory(
        IReadOnlyList<VerifiedUsbSerialEndpoint> verifiedEndpoints)
    {
        Console.WriteLine(
            $"Unique verified endpoints: {verifiedEndpoints.Count}");

        Console.WriteLine();

        for (
            int index = 0;
            index < verifiedEndpoints.Count;
            index++)
        {
            VerifiedUsbSerialEndpoint endpoint =
                verifiedEndpoints[index];

            Console.WriteLine(
                $"Verified endpoint {index + 1}  : {endpoint.EndpointId.Value}");

            Console.WriteLine(
                $"Candidate port        : {endpoint.Candidate.PortName}");

            Console.WriteLine(
                $"Descriptor reference  : "
                + $"{endpoint.DescriptorReference.Id.Value} "
                + $"v{endpoint.DescriptorReference.Version}");

            Console.WriteLine();
        }
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