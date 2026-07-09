namespace Hase.Core.Domain.Instruments;

/// <summary>
/// Contains descriptive metadata about an instrument.
/// </summary>
public sealed record InstrumentMetadata
{
    public string? Manufacturer { get; init; }

    public string? Model { get; init; }

    public string? SerialNumber { get; init; }

    public string? FirmwareVersion { get; init; }

    public string? HardwareRevision { get; init; }

    public string? Description { get; init; }
}