using System.Security.Cryptography.X509Certificates;

namespace Hase.Runtime.Remote.Grpc.Hosting;

/// <summary>
/// Identifies one externally provisioned certificate in an operating-system
/// X.509 certificate store.
/// </summary>
public sealed record RuntimeHostCertificateStoreReference
{
    /// <summary>
    /// Initializes one certificate-store reference.
    /// </summary>
    public RuntimeHostCertificateStoreReference(
        StoreName storeName,
        StoreLocation storeLocation,
        string thumbprint)
    {
        if (!Enum.IsDefined(
                storeName))
        {
            throw new ArgumentOutOfRangeException(
                nameof(storeName),
                storeName,
                "The certificate store name must be defined.");
        }

        if (!Enum.IsDefined(
                storeLocation))
        {
            throw new ArgumentOutOfRangeException(
                nameof(storeLocation),
                storeLocation,
                "The certificate store location must be defined.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(
            thumbprint,
            nameof(thumbprint));

        string normalizedThumbprint =
            NormalizeThumbprint(
                thumbprint);

        if (normalizedThumbprint.Length != 40
            || !normalizedThumbprint.All(
                Uri.IsHexDigit))
        {
            throw new ArgumentException(
                "The certificate thumbprint must contain exactly 40 "
                + "hexadecimal characters.",
                nameof(thumbprint));
        }

        StoreName = storeName;
        StoreLocation = storeLocation;
        Thumbprint = normalizedThumbprint;
    }

    /// <summary>
    /// Gets the operating-system certificate store name.
    /// </summary>
    public StoreName StoreName
    {
        get;
    }

    /// <summary>
    /// Gets the operating-system certificate store location.
    /// </summary>
    public StoreLocation StoreLocation
    {
        get;
    }

    /// <summary>
    /// Gets the normalized uppercase SHA-1 certificate thumbprint.
    /// </summary>
    public string Thumbprint
    {
        get;
    }

    private static string NormalizeThumbprint(
        string thumbprint)
    {
        return string.Concat(
                thumbprint.Where(
                    character =>
                        !char.IsWhiteSpace(
                            character)
                        && character != ':'))
            .ToUpperInvariant();
    }
}
