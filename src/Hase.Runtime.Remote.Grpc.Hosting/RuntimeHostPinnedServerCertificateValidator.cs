using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Hase.Runtime.Remote.Grpc.Hosting;

/// <summary>
/// Applies exact server-certificate pinning while preserving the TLS stack's
/// server-identity validation.
/// </summary>
public sealed class RuntimeHostPinnedServerCertificateValidator
{
    private readonly byte[] trustedCertificateHash;

    /// <summary>
    /// Initializes validation from one externally provisioned trusted server
    /// certificate.
    /// </summary>
    public RuntimeHostPinnedServerCertificateValidator(
        X509Certificate2 trustedServerCertificate)
    {
        ArgumentNullException.ThrowIfNull(
            trustedServerCertificate);

        trustedCertificateHash =
            trustedServerCertificate.GetCertHash(
                HashAlgorithmName.SHA256);
    }

    /// <summary>
    /// Validates the presented server certificate for use as an
    /// <see cref="RemoteCertificateValidationCallback"/>.
    /// </summary>
    public bool Validate(
        object sender,
        X509Certificate? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors)
    {
        if (certificate is null)
        {
            return false;
        }

        const SslPolicyErrors identityErrors =
            SslPolicyErrors.RemoteCertificateNotAvailable
            | SslPolicyErrors.RemoteCertificateNameMismatch;

        if ((sslPolicyErrors & identityErrors)
            != SslPolicyErrors.None)
        {
            return false;
        }

        byte[] presentedCertificateHash =
            certificate.GetCertHash(
                HashAlgorithmName.SHA256);

        return CryptographicOperations.FixedTimeEquals(
            trustedCertificateHash,
            presentedCertificateHash);
    }
}
