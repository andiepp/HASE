using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;

namespace Hase.DesktopHost.Configuration;

public sealed record DesktopRuntimeHostRfLabSerialEndpointProfile
{
    public const int SupportedBaudRate = 115200;

    public DesktopRuntimeHostRfLabSerialEndpointProfile(
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
                $"An RF-Lab serial endpoint requires baud rate {SupportedBaudRate}.");
        }

        SerialPort = serialPort.Trim();
        BaudRate = baudRate;
    }

    public string ExpectedEndpointId { get; }
    public DescriptorReference DefinitionReference { get; }
    public string SerialPort { get; }
    public int BaudRate { get; }

    public override string ToString() =>
        $"RF-Lab serial endpoint '{ExpectedEndpointId}'";
}
