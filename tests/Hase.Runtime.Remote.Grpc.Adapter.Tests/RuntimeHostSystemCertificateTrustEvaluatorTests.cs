using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostSystemCertificateTrustEvaluatorTests
{
    private static readonly DateTimeOffset ValidationTimeUtc =
        new(
            2026,
            7,
            25,
            22,
            30,
            0,
            TimeSpan.Zero);

    [Fact]
    public void IsTrusted_NullCertificate_ShouldThrow()
    {
        RuntimeHostSystemCertificateTrustEvaluator evaluator =
            new();

        Assert.Throws<ArgumentNullException>(
            "certificate",
            () =>
                evaluator.IsTrusted(
                    null!,
                    ValidationTimeUtc));
    }

    [Fact]
    public void IsTrusted_NonUtcTime_ShouldThrow()
    {
        using X509Certificate2 certificate =
            CreateSelfSignedCertificate();
        RuntimeHostSystemCertificateTrustEvaluator evaluator =
            new();
        DateTimeOffset nonUtcTime =
            new(
                2026,
                7,
                26,
                0,
                30,
                0,
                TimeSpan.FromHours(2));

        Assert.Throws<ArgumentException>(
            "validationTimeUtc",
            () =>
                evaluator.IsTrusted(
                    certificate,
                    nonUtcTime));
    }

    [Fact]
    public void IsTrusted_UnenrolledSelfSignedCertificate_ShouldNotBeTrusted()
    {
        using X509Certificate2 certificate =
            CreateSelfSignedCertificate();
        RuntimeHostSystemCertificateTrustEvaluator evaluator =
            new();

        bool trusted =
            evaluator.IsTrusted(
                certificate,
                ValidationTimeUtc);

        Assert.False(
            trusted);
    }

    private static X509Certificate2 CreateSelfSignedCertificate()
    {
        using RSA key =
            RSA.Create(
                2048);

        CertificateRequest request =
            new(
                new X500DistinguishedName(
                    "CN=hase-untrusted-client"),
                key,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(
                false,
                false,
                0,
                true));

        return request.CreateSelfSigned(
            ValidationTimeUtc.AddDays(-1),
            ValidationTimeUtc.AddDays(1));
    }
}
