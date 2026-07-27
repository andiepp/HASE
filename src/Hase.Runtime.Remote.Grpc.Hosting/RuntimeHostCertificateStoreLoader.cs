using System.Security.Cryptography.X509Certificates;

namespace Hase.Runtime.Remote.Grpc.Hosting;

/// <summary>
/// Resolves externally provisioned certificates from operating-system X.509
/// certificate stores without exporting certificate or private-key material.
/// </summary>
public static class RuntimeHostCertificateStoreLoader
{
    /// <summary>
    /// Loads the uniquely referenced certificate from its configured store.
    /// The caller owns and must dispose the returned certificate.
    /// </summary>
    public static X509Certificate2 Load(
        RuntimeHostCertificateStoreReference reference,
        bool requirePrivateKey)
    {
        ArgumentNullException.ThrowIfNull(
            reference);

        using var store =
            new X509Store(
                reference.StoreName,
                reference.StoreLocation);

        store.Open(
            OpenFlags.ReadOnly
            | OpenFlags.OpenExistingOnly);

        X509Certificate2 selectedCertificate =
            RuntimeHostCertificateStoreCertificateSelector.Select(
                reference,
                store.Certificates,
                requirePrivateKey);

        return selectedCertificate;
    }
}
