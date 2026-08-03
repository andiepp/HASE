using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;

namespace Hase.DesktopHost.Configuration;

public sealed record DesktopRuntimeHostKel103SerialEndpointProfile
{
    public const int SupportedBaudRate = 115200;

    public DesktopRuntimeHostKel103SerialEndpointProfile(
        string expectedEndpointId,
        string definitionId,
        ushort definitionVersion,
        string serialPort,
        int baudRate)
    {
        ExpectedEndpointId = new EndpointId(expectedEndpointId).Value;
        DefinitionReference = new DescriptorReference(
            new DescriptorId(definitionId),
            definitionVersion);

        ArgumentException.ThrowIfNullOrWhiteSpace(serialPort);

        if (baudRate != SupportedBaudRate)
        {
            throw new ArgumentOutOfRangeException(
                nameof(baudRate),
                baudRate,
                $"A KEL-103 serial endpoint requires baud rate {SupportedBaudRate}.");
        }

        SerialPort = serialPort.Trim();
        BaudRate = baudRate;
    }

    public string ExpectedEndpointId { get; }
    public DescriptorReference DefinitionReference { get; }
    public string SerialPort { get; }
    public int BaudRate { get; }

    public override string ToString() =>
        $"KEL-103 serial endpoint '{ExpectedEndpointId}'";
}
