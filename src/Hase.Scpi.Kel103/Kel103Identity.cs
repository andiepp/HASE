namespace Hase.Scpi.Kel103;

/// <summary>
/// Contains the non-sensitive identity values published for a verified KEL-103.
/// </summary>
public sealed record Kel103Identity
{
    public Kel103Identity(string productIdentity, string firmwareVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(firmwareVersion);

        ProductIdentity = productIdentity;
        FirmwareVersion = firmwareVersion;
    }

    public string ProductIdentity { get; }

    public string FirmwareVersion { get; }

    public override string ToString() => $"{ProductIdentity} {FirmwareVersion}";
}
