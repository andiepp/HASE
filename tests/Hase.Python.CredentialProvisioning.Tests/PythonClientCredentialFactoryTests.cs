using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Hase.Python.CredentialProvisioning;

namespace Hase.Python.CredentialProvisioning.Tests;

public sealed class PythonClientCredentialFactoryTests
{
    private static readonly DateTimeOffset Now =
        new(
            2026,
            8,
            8,
            8,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public void Create_ValidRoot_ProducesMatchingClientCredential()
    {
        using X509Certificate2 root =
            CreateRsaRoot();
        using PythonClientCredentialMaterial material =
            PythonClientCredentialFactory.Create(
                root,
                Now,
                TimeSpan.FromDays(30));

        string certificatePem =
            Encoding.ASCII.GetString(material.CertificatePem.Span);
        string privateKeyPem =
            Encoding.ASCII.GetString(material.PrivateKeyPem.Span);

        using X509Certificate2 certificate =
            X509Certificate2.CreateFromPem(certificatePem);
        using RSA privateKey =
            RSA.Create();
        privateKey.ImportFromPem(privateKeyPem);
        using RSA? publicKey =
            certificate.GetRSAPublicKey();

        Assert.NotNull(publicKey);
        Assert.Equal(
            publicKey.ExportSubjectPublicKeyInfo(),
            privateKey.ExportSubjectPublicKeyInfo());
        Assert.StartsWith(
            "-----BEGIN CERTIFICATE-----",
            certificatePem,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "-----BEGIN PRIVATE KEY-----",
            privateKeyPem,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ENCRYPTED",
            privateKeyPem,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ValidRoot_ChainsOnlyToExactCustomRoot()
    {
        using X509Certificate2 root =
            CreateRsaRoot();
        using PythonClientCredentialMaterial material =
            PythonClientCredentialFactory.Create(
                root,
                Now,
                TimeSpan.FromDays(30));
        using X509Certificate2 certificate =
            X509Certificate2.CreateFromPem(
                Encoding.ASCII.GetString(material.CertificatePem.Span));
        using var chain =
            new X509Chain();
        chain.ChainPolicy.TrustMode =
            X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(root);
        chain.ChainPolicy.RevocationMode =
            X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationTime =
            Now.UtcDateTime;

        bool built =
            chain.Build(certificate);

        Assert.True(
            built,
            string.Join(
                ", ",
                chain.ChainStatus.Select(status => status.Status)));
        Assert.Equal(
            2,
            chain.ChainElements.Count);
        Assert.Equal(
            root.RawData,
            chain.ChainElements[1].Certificate.RawData);
    }

    [Fact]
    public void Create_ValidRoot_UsesApprovedExtensionsAndValidity()
    {
        using X509Certificate2 root =
            CreateRsaRoot();
        using PythonClientCredentialMaterial material =
            PythonClientCredentialFactory.Create(
                root,
                Now,
                PythonClientCredentialFactory.MaximumValidity);
        using X509Certificate2 certificate =
            X509Certificate2.CreateFromPem(
                Encoding.ASCII.GetString(material.CertificatePem.Span));

        X509BasicConstraintsExtension constraints =
            GetExtension<X509BasicConstraintsExtension>(
                certificate,
                "2.5.29.19");
        X509KeyUsageExtension keyUsage =
            GetExtension<X509KeyUsageExtension>(
                certificate,
                "2.5.29.15");
        X509EnhancedKeyUsageExtension enhancedKeyUsage =
            GetExtension<X509EnhancedKeyUsageExtension>(
                certificate,
                "2.5.29.37");

        Assert.False(constraints.CertificateAuthority);
        Assert.True(constraints.Critical);
        Assert.Equal(
            X509KeyUsageFlags.DigitalSignature,
            keyUsage.KeyUsages);
        Assert.True(keyUsage.Critical);
        Assert.True(enhancedKeyUsage.Critical);
        Assert.Equal(
            ["1.3.6.1.5.5.7.3.2"],
            enhancedKeyUsage.EnhancedKeyUsages
                .Cast<Oid>()
                .Select(oid => oid.Value!)
                .ToArray());
        Assert.Equal(
            PythonClientCredentialFactory.MaximumValidity,
            certificate.NotAfter.ToUniversalTime()
            - certificate.NotBefore.ToUniversalTime());
    }

    [Fact]
    public void Create_ValidRoot_ProducesPositiveIndependentSerialNumbers()
    {
        using X509Certificate2 root =
            CreateRsaRoot();
        using PythonClientCredentialMaterial first =
            PythonClientCredentialFactory.Create(
                root,
                Now,
                TimeSpan.FromDays(30));
        using PythonClientCredentialMaterial second =
            PythonClientCredentialFactory.Create(
                root,
                Now,
                TimeSpan.FromDays(30));
        using X509Certificate2 firstCertificate =
            X509Certificate2.CreateFromPem(
                Encoding.ASCII.GetString(first.CertificatePem.Span));
        using X509Certificate2 secondCertificate =
            X509Certificate2.CreateFromPem(
                Encoding.ASCII.GetString(second.CertificatePem.Span));

        var firstSerial =
            new BigInteger(
                firstCertificate.SerialNumberBytes.Span,
                isUnsigned: false,
                isBigEndian: true);
        var secondSerial =
            new BigInteger(
                secondCertificate.SerialNumberBytes.Span,
                isUnsigned: false,
                isBigEndian: true);

        Assert.True(firstSerial > BigInteger.Zero);
        Assert.True(secondSerial > BigInteger.Zero);
        Assert.NotEqual(
            firstCertificate.SerialNumber,
            secondCertificate.SerialNumber);
    }

    [Fact]
    public void Create_ValidRoot_CredentialIdMatchesLeafDer()
    {
        using X509Certificate2 root =
            CreateRsaRoot();
        using PythonClientCredentialMaterial material =
            PythonClientCredentialFactory.Create(
                root,
                Now,
                TimeSpan.FromDays(30));
        using X509Certificate2 certificate =
            X509Certificate2.CreateFromPem(
                Encoding.ASCII.GetString(material.CertificatePem.Span));

        string expected =
            "x509-sha256:"
            + Convert.ToHexString(
                SHA256.HashData(certificate.RawData))
                .ToLowerInvariant();

        Assert.Equal(
            expected,
            material.CredentialId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(91)]
    public void Create_InvalidValidity_RejectsBeforeOutput(
        int validityDays)
    {
        using X509Certificate2 root =
            CreateRsaRoot();

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                PythonClientCredentialFactory.Create(
                    root,
                    Now,
                    TimeSpan.FromDays(validityDays)));
    }

    [Fact]
    public void Create_NullRoot_RejectsBeforeOutput()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
                PythonClientCredentialFactory.Create(
                    null!,
                    Now,
                    TimeSpan.FromDays(30)));
    }

    [Fact]
    public void Create_KeylessRoot_RejectsBeforeOutput()
    {
        using X509Certificate2 rootWithKey =
            CreateRsaRoot();
        using var keylessRoot =
            X509CertificateLoader.LoadCertificate(
                rootWithKey.RawData);

        Assert.Throws<InvalidOperationException>(
            () =>
                PythonClientCredentialFactory.Create(
                    keylessRoot,
                    Now,
                    TimeSpan.FromDays(30)));
    }

    [Fact]
    public void Create_NonCaSigner_RejectsBeforeOutput()
    {
        using X509Certificate2 signer =
            CreateRsaRoot(
                certificateAuthority: false);

        Assert.Throws<InvalidOperationException>(
            () =>
                PythonClientCredentialFactory.Create(
                    signer,
                    Now,
                    TimeSpan.FromDays(30)));
    }

    [Fact]
    public void Create_RootWithoutCertificateSigningUsage_RejectsBeforeOutput()
    {
        using X509Certificate2 root =
            CreateRsaRoot(
                rootKeyUsage: X509KeyUsageFlags.CrlSign);

        Assert.Throws<InvalidOperationException>(
            () =>
                PythonClientCredentialFactory.Create(
                    root,
                    Now,
                    TimeSpan.FromDays(30)));
    }

    [Fact]
    public void Create_ExpiredRoot_RejectsBeforeOutput()
    {
        using X509Certificate2 root =
            CreateRsaRoot(
                notBefore: Now.AddYears(-2),
                notAfter: Now.AddYears(-1));

        Assert.Throws<InvalidOperationException>(
            () =>
                PythonClientCredentialFactory.Create(
                    root,
                    Now,
                    TimeSpan.FromDays(30)));
    }

    [Fact]
    public void Create_EcdsaRoot_RejectsBeforeOutput()
    {
        using ECDsa rootKey =
            ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request =
            new CertificateRequest(
                "CN=Test ECDSA Root",
                rootKey,
                HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(
                true,
                false,
                0,
                true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.KeyCertSign,
                true));
        using X509Certificate2 root =
            request.CreateSelfSigned(
                Now.AddDays(-1),
                Now.AddYears(1));

        Assert.Throws<InvalidOperationException>(
            () =>
                PythonClientCredentialFactory.Create(
                    root,
                    Now,
                    TimeSpan.FromDays(30)));
    }

    [Fact]
    public void Dispose_ZerosOwnedPemAndRejectsFurtherAccess()
    {
        using X509Certificate2 root =
            CreateRsaRoot();
        PythonClientCredentialMaterial material =
            PythonClientCredentialFactory.Create(
                root,
                Now,
                TimeSpan.FromDays(30));
        ReadOnlyMemory<byte> certificateMemory =
            material.CertificatePem;
        ReadOnlyMemory<byte> privateKeyMemory =
            material.PrivateKeyPem;

        material.Dispose();
        material.Dispose();

        Assert.All(
            certificateMemory.ToArray(),
            value => Assert.Equal(0, value));
        Assert.All(
            privateKeyMemory.ToArray(),
            value => Assert.Equal(0, value));
        Assert.Throws<ObjectDisposedException>(
            () => material.CertificatePem);
        Assert.Throws<ObjectDisposedException>(
            () => material.PrivateKeyPem);
        Assert.Throws<ObjectDisposedException>(
            () => material.CredentialId);
    }

    private static X509Certificate2 CreateRsaRoot(
        bool certificateAuthority = true,
        DateTimeOffset? notBefore = null,
        DateTimeOffset? notAfter = null,
        X509KeyUsageFlags rootKeyUsage =
            X509KeyUsageFlags.KeyCertSign
            | X509KeyUsageFlags.CrlSign)
    {
        using RSA rootKey =
            RSA.Create(3072);
        var request =
            new CertificateRequest(
                "CN=Test HASE Python Root",
                rootKey,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(
                certificateAuthority,
                false,
                0,
                true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                rootKeyUsage,
                true));

        return request.CreateSelfSigned(
            notBefore ?? Now.AddYears(-1),
            notAfter ?? Now.AddYears(1));
    }

    private static TExtension GetExtension<TExtension>(
        X509Certificate2 certificate,
        string oid)
        where TExtension : X509Extension
    {
        X509Extension source =
            Assert.Single(
                certificate.Extensions
                    .Cast<X509Extension>(),
                extension => extension.Oid?.Value == oid);

        return (TExtension)Activator.CreateInstance(
            typeof(TExtension),
            source,
            source.Critical)!;
    }
}
