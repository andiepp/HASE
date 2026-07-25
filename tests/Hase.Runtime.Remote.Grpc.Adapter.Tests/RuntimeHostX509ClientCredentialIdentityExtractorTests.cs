using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostX509ClientCredentialIdentityExtractorTests
{
    [Fact]
    public void Extract_NullCertificate_ShouldThrow()
    {
        RuntimeHostX509ClientCredentialIdentityExtractor extractor =
            new();

        Assert.Throws<ArgumentNullException>(
            "certificate",
            () =>
                extractor.Extract(
                    null!));
    }

    [Fact]
    public void Extract_ValidCertificate_ShouldUseMutualTlsAndSha256Identity()
    {
        using X509Certificate2 certificate =
            CreateCertificate(
                "client-01");
        RuntimeHostX509ClientCredentialIdentityExtractor extractor =
            new();

        RuntimeHostClientCredentialIdentity identity =
            extractor.Extract(
                certificate);

        string expectedCredentialId =
            "x509-sha256:"
            + Convert.ToHexString(
                certificate.GetCertHash(
                    HashAlgorithmName.SHA256))
                .ToLowerInvariant();

        Assert.Equal(
            RuntimeHostAuthenticationMechanism.MutualTls,
            identity.AuthenticationMechanism);
        Assert.Equal(
            expectedCredentialId,
            identity.CredentialId.Value);
    }

    [Fact]
    public void Extract_SameCertificateRepeatedly_ShouldBeDeterministic()
    {
        using X509Certificate2 certificate =
            CreateCertificate(
                "client-01");
        RuntimeHostX509ClientCredentialIdentityExtractor extractor =
            new();

        RuntimeHostClientCredentialIdentity first =
            extractor.Extract(
                certificate);
        RuntimeHostClientCredentialIdentity second =
            extractor.Extract(
                certificate);

        Assert.Equal(
            first,
            second);
    }

    [Fact]
    public void Extract_DifferentCertificates_ShouldProduceDifferentIdentities()
    {
        using X509Certificate2 firstCertificate =
            CreateCertificate(
                "client-01");
        using X509Certificate2 secondCertificate =
            CreateCertificate(
                "client-02");
        RuntimeHostX509ClientCredentialIdentityExtractor extractor =
            new();

        RuntimeHostClientCredentialIdentity first =
            extractor.Extract(
                firstCertificate);
        RuntimeHostClientCredentialIdentity second =
            extractor.Extract(
                secondCertificate);

        Assert.NotEqual(
            first,
            second);
    }

    [Fact]
    public void Extract_ShouldNotDependOnCertificatePrivateKeyAvailability()
    {
        using X509Certificate2 certificateWithPrivateKey =
            CreateCertificate(
                "client-01");
        byte[] publicCertificateBytes =
            certificateWithPrivateKey.Export(
                X509ContentType.Cert);
        using X509Certificate2 publicCertificate =
            X509CertificateLoader.LoadCertificate(
                publicCertificateBytes);
        RuntimeHostX509ClientCredentialIdentityExtractor extractor =
            new();

        RuntimeHostClientCredentialIdentity privateKeyIdentity =
            extractor.Extract(
                certificateWithPrivateKey);
        RuntimeHostClientCredentialIdentity publicIdentity =
            extractor.Extract(
                publicCertificate);

        Assert.True(
            certificateWithPrivateKey.HasPrivateKey);
        Assert.False(
            publicCertificate.HasPrivateKey);
        Assert.Equal(
            privateKeyIdentity,
            publicIdentity);
    }

    private static X509Certificate2 CreateCertificate(
        string commonName)
    {
        using RSA key =
            RSA.Create(
                2048);

        CertificateRequest request =
            new(
                new X500DistinguishedName(
                    $"CN={commonName}"),
                key,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(
                false,
                false,
                0,
                true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature,
                true));
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection
                {
                    new Oid(
                        "1.3.6.1.5.5.7.3.2")
                },
                true));

        DateTimeOffset notBefore =
            new(
                2026,
                7,
                25,
                20,
                0,
                0,
                TimeSpan.Zero);
        DateTimeOffset notAfter =
            notBefore.AddDays(
                30);

        return request.CreateSelfSigned(
            notBefore,
            notAfter);
    }
}
