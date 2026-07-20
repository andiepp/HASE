using System.Globalization;
using Hase.CompactProtocol;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Transport.Serial;

namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Validates physical Arduino Uno-class endpoint bootstrap and host-side
/// descriptor materialization through the Compact Serial Protocol.
/// </summary>
internal sealed class CapabilityC018Scenario
    : IParameterizedScenario
{
    private const int DefaultBaudRate =
        115200;

    private static readonly DescriptorReference ValidationDescriptorReference =
        new(
            new DescriptorId(
                "arduino-uno-validation"),
            version: 1);

    public string Name =>
        "c018";

    public void Execute(
        IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(
            arguments);

        ExecuteAsync(
                arguments)
            .GetAwaiter()
            .GetResult();
    }

    private static async Task ExecuteAsync(
        IReadOnlyList<string> arguments)
    {
        if (arguments.Count is < 1 or > 2)
        {
            throw new ArgumentException(
                "Capability C-018 requires a COM port and accepts an optional "
                + "baud rate.",
                nameof(arguments));
        }

        string portName =
            arguments[0];

        int baudRate =
            arguments.Count == 2
                ? ParseBaudRate(
                    arguments[1])
                : DefaultBaudRate;

        var transportOptions =
            new SerialTransportOptions(
                portName,
                baudRate);

        var descriptorDefinition =
            new EndpointDescriptorDefinition(
                new EndpointMetadata
                {
                    DisplayName =
                        "Arduino Uno Compact Validation Endpoint",
                    Description =
                        "Physical Arduino Uno-class endpoint used to validate "
                        + "Compact Serial Protocol bootstrap."
                },
                instruments: []);

        var descriptorRepository =
            new InMemoryEndpointDescriptorRepository(
                [
                    new KeyValuePair<
                        DescriptorReference,
                        EndpointDescriptorDefinition>(
                            ValidationDescriptorReference,
                            descriptorDefinition)
                ]);

        var connector =
            new CompactSerialEndpointConnector(
                new SystemIoPortsSerialByteStreamFactory(),
                descriptorRepository);

        WriteHeader(
            transportOptions);

        Console.WriteLine(
            "Opening serial endpoint.");

        Console.WriteLine();

        await using CompactEndpointConnection connection =
            await connector.ConnectAsync(
                transportOptions,
                expectedEndpointId: null);

        WriteResult(
            connection);
    }

    private static int ParseBaudRate(
        string value)
    {
        if (!int.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int baudRate)
            || baudRate <= 0)
        {
            throw new ArgumentException(
                $"'{value}' is not a valid positive baud rate.",
                nameof(value));
        }

        return baudRate;
    }

    private static void WriteResult(
        CompactEndpointConnection connection)
    {
        EndpointDescriptor descriptor =
            connection.Descriptor;

        Console.WriteLine(
            "Compact endpoint initialized.");

        Console.WriteLine();

        Console.WriteLine(
            $"Authoritative endpoint : {descriptor.Id.Value}");

        Console.WriteLine(
            $"Descriptor id          : "
            + $"{ValidationDescriptorReference.Id.Value}");

        Console.WriteLine(
            $"Descriptor version     : "
            + $"{ValidationDescriptorReference.Version}");

        Console.WriteLine(
            $"Display name           : "
            + $"{descriptor.Metadata.DisplayName}");

        Console.WriteLine(
            $"Instrument count       : "
            + $"{descriptor.Instruments.Count}");

        Console.WriteLine(
            $"Connection state       : "
            + $"{connection.Connection.State}");

        Console.WriteLine();

        Console.WriteLine(
            "The serial connection will now be closed.");
    }

    private static void WriteHeader(
        SerialTransportOptions transportOptions)
    {
        const string title =
            "Capability C-018";

        Console.WriteLine(
            title);

        Console.WriteLine(
            new string(
                '=',
                title.Length));

        Console.WriteLine();

        Console.WriteLine(
            "Validate a physical Arduino Uno-class endpoint through the "
            + "Compact Serial Protocol.");

        Console.WriteLine();

        Console.WriteLine(
            $"Port                 : {transportOptions.PortName}");

        Console.WriteLine(
            $"Baud rate            : {transportOptions.BaudRate}");

        Console.WriteLine(
            "Connection origin    : Configured");

        Console.WriteLine(
            "Protocol             : Compact Serial Protocol V1");

        Console.WriteLine(
            "Descriptor source    : Host repository");

        Console.WriteLine(
            "Expected endpoint    : <none>");

        Console.WriteLine();
    }
}