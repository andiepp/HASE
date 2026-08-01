using Hase.Core.Domain.Identity;

namespace Hase.DesktopHost.Configuration;

public sealed record DesktopRuntimeHostCompactSerialEndpointProfile
{
    public DesktopRuntimeHostCompactSerialEndpointProfile(
        string expectedEndpointId,
        ushort vendorId,
        ushort productId,
        int baudRate,
        TimeSpan verificationTimeout)
    {
        ExpectedEndpointId = new EndpointId(expectedEndpointId).Value;

        if (baudRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(baudRate));
        }

        if (verificationTimeout <= TimeSpan.Zero
            || verificationTimeout > TimeSpan.FromMinutes(1))
        {
            throw new ArgumentOutOfRangeException(nameof(verificationTimeout));
        }

        VendorId = vendorId;
        ProductId = productId;
        BaudRate = baudRate;
        VerificationTimeout = verificationTimeout;
    }

    public string ExpectedEndpointId { get; }
    public ushort VendorId { get; }
    public ushort ProductId { get; }
    public int BaudRate { get; }
    public TimeSpan VerificationTimeout { get; }

    public override string ToString() =>
        $"Compact serial endpoint '{ExpectedEndpointId}'";
}
