using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Hase.Runtime.Remote.Grpc.Hosting.Tests;

public sealed class RuntimeHostCertificateStoreCertificateSelectorTests
{
    [Fact]
    public void Select_MissingReference_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "reference",
            () =>
                RuntimeHostCertificateStoreCertificateSelector.Select(
                    null!,
                    Array.Empty<X509Certificate2>(),
                    requirePrivateKey: false));
    }

    [Fact]
    public void Select_MissingCertificateCollection_ShouldThrow()
    {
        using X509Certificate2 certificate =
            CreateCertificate();
        RuntimeHostCertificateStoreReference reference =
            CreateReference(
                certificate);

        Assert.Throws<ArgumentNullException>(
            "certificates",
            () =>
                RuntimeHostCertificateStoreCertificateSelector.Select(
                    reference,
                    null!,
                    requirePrivateKey: false));
    }

    [Fact]
    public void Select_NullCertificate_ShouldThrow()
    {
        using X509Certificate2 certificate =
            CreateCertificate();
        RuntimeHostCertificateStoreReference reference =
            CreateReference(
                certificate);

        Assert.Throws<ArgumentException>(
            "certificates",
            () =>
                RuntimeHostCertificateStoreCertificateSelector.Select(
                    reference,
                    new X509Certificate2[]
                    {
                        null!
                    },
                    requirePrivateKey: false));
    }

    [Fact]
    public void Select_MissingCertificate_ShouldThrow()
    {
        using X509Certificate2 referencedCertificate =
            CreateCertificate();
        using X509Certificate2 differentCertificate =
            CreateCertificate();
        RuntimeHostCertificateStoreReference reference =
            CreateReference(
                referencedCertificate);

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    RuntimeHostCertificateStoreCertificateSelector.Select(
                        reference,
                        new[]
                        {
                            differentCertificate
                        },
                        requirePrivateKey: false));

        Assert.Equal(
            "The configured certificate was not found in the "
            + "operating-system certificate store.",
            exception.Message);
    }

    [Fact]
    public void Select_DuplicateCertificate_ShouldThrow()
    {
        using X509Certificate2 certificate =
            CreateCertificate();
        RuntimeHostCertificateStoreReference reference =
            CreateReference(
                certificate);

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    RuntimeHostCertificateStoreCertificateSelector.Select(
                        reference,
                        new[]
                        {
                            certificate,
                            certificate
                        },
                        requirePrivateKey: false));

        Assert.Equal(
            "The configured certificate-store reference is ambiguous.",
            exception.Message);
    }

    [Fact]
    public void Select_MissingPrivateKeyWhenRequired_ShouldThrow()
    {
        using X509Certificate2 certificateWithPrivateKey =
            CreateCertificate();
        using X509Certificate2 publicCertificate =
            X509CertificateLoader.LoadCertificate(
                certificateWithPrivateKey.Export(
                    X509ContentType.Cert));
        RuntimeHostCertificateStoreReference reference =
            CreateReference(
                publicCertificate);

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    RuntimeHostCertificateStoreCertificateSelector.Select(
                        reference,
                        new[]
                        {
                            publicCertificate
                        },
                        requirePrivateKey: true));

        Assert.Equal(
            "The configured certificate does not have an accessible "
            + "private key.",
            exception.Message);
    }

    [Fact]
    public void Select_PublicCertificateWhenPrivateKeyNotRequired_ShouldReturn()
    {
        using X509Certificate2 certificateWithPrivateKey =
            CreateCertificate();
        using X509Certificate2 publicCertificate =
            X509CertificateLoader.LoadCertificate(
                certificateWithPrivateKey.Export(
                    X509ContentType.Cert));
        RuntimeHostCertificateStoreReference reference =
            CreateReference(
                publicCertificate);

        X509Certificate2 selectedCertificate =
            RuntimeHostCertificateStoreCertificateSelector.Select(
                reference,
                new[]
                {
                    publicCertificate
                },
                requirePrivateKey: false);

        Assert.Same(
            publicCertificate,
            selectedCertificate);
    }

    [Fact]
    public void Select_CertificateWithPrivateKey_ShouldReturn()
    {
        using X509Certificate2 certificate =
            CreateCertificate();
        RuntimeHostCertificateStoreReference reference =
            CreateReference(
                certificate);

        X509Certificate2 selectedCertificate =
            RuntimeHostCertificateStoreCertificateSelector.Select(
                reference,
                new[]
                {
                    certificate
                },
                requirePrivateKey: true);

        Assert.Same(
            certificate,
            selectedCertificate);
    }

    private static RuntimeHostCertificateStoreReference CreateReference(
        X509Certificate2 certificate)
    {
        return new RuntimeHostCertificateStoreReference(
            StoreName.My,
            StoreLocation.CurrentUser,
            certificate.Thumbprint);
    }

    private static X509Certificate2 CreateCertificate()
    {
        using RSA rsa =
            RSA.Create(
                2048);
        CertificateRequest request =
            new(
                "CN=HASE generated certificate-store test",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(
                -1),
            DateTimeOffset.UtcNow.AddMinutes(
                5));
    }
}
