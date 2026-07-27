using System.Security.Cryptography.X509Certificates;

namespace Hase.Runtime.Remote.Grpc.Hosting;

/// <summary>
/// Selects exactly one certificate matching an external certificate-store
/// reference.
/// </summary>
public static class RuntimeHostCertificateStoreCertificateSelector
{
    /// <summary>
    /// Selects the uniquely matching certificate and optionally requires an
    /// accessible private key.
    /// </summary>
    public static X509Certificate2 Select(
        RuntimeHostCertificateStoreReference reference,
        IEnumerable<X509Certificate2> certificates,
        bool requirePrivateKey)
    {
        ArgumentNullException.ThrowIfNull(
            reference);
        ArgumentNullException.ThrowIfNull(
            certificates);

        List<X509Certificate2> matches =
            [];

        foreach (X509Certificate2 certificate in certificates)
        {
            if (certificate is null)
            {
                throw new ArgumentException(
                    "The certificate collection must not contain null.",
                    nameof(certificates));
            }

            if (string.Equals(
                    certificate.Thumbprint,
                    reference.Thumbprint,
                    StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(
                    certificate);
            }
        }

        if (matches.Count == 0)
        {
            throw new InvalidOperationException(
                "The configured certificate was not found in the "
                + "operating-system certificate store.");
        }

        if (matches.Count > 1)
        {
            throw new InvalidOperationException(
                "The configured certificate-store reference is ambiguous.");
        }

        X509Certificate2 selectedCertificate =
            matches[0];

        if (requirePrivateKey
            && !selectedCertificate.HasPrivateKey)
        {
            throw new InvalidOperationException(
                "The configured certificate does not have an accessible "
                + "private key.");
        }

        return selectedCertificate;
    }
}
