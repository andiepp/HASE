using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Hase.Runtime.Remote.Grpc.Hosting.Tests;

public sealed class RuntimeHostPinnedServerCertificateValidatorTests
{
    [Fact]
    public void Constructor_MissingTrustedCertificate_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "trustedServerCertificate",
            () =>
                new RuntimeHostPinnedServerCertificateValidator(
                    null!));
    }

    [Fact]
    public void Validate_MissingPresentedCertificate_ShouldReject()
    {
        using X509Certificate2 trustedCertificate =
            CreateCertificate();
        var validator =
            new RuntimeHostPinnedServerCertificateValidator(
                trustedCertificate);

        bool accepted =
            validator.Validate(
                this,
                null,
                null,
                SslPolicyErrors.RemoteCertificateNotAvailable);

        Assert.False(
            accepted);
    }

    [Fact]
    public void Validate_NameMismatch_ShouldRejectPinnedCertificate()
    {
        using X509Certificate2 trustedCertificate =
            CreateCertificate();
        var validator =
            new RuntimeHostPinnedServerCertificateValidator(
                trustedCertificate);

        bool accepted =
            validator.Validate(
                this,
                trustedCertificate,
                null,
                SslPolicyErrors.RemoteCertificateNameMismatch);

        Assert.False(
            accepted);
    }

    [Fact]
    public void Validate_DifferentCertificate_ShouldReject()
    {
        using X509Certificate2 trustedCertificate =
            CreateCertificate();
        using X509Certificate2 presentedCertificate =
            CreateCertificate();
        var validator =
            new RuntimeHostPinnedServerCertificateValidator(
                trustedCertificate);

        bool accepted =
            validator.Validate(
                this,
                presentedCertificate,
                null,
                SslPolicyErrors.None);

        Assert.False(
            accepted);
    }

    [Fact]
    public void Validate_ExactCertificateWithoutTlsErrors_ShouldAccept()
    {
        using X509Certificate2 trustedCertificate =
            CreateCertificate();
        var validator =
            new RuntimeHostPinnedServerCertificateValidator(
                trustedCertificate);

        bool accepted =
            validator.Validate(
                this,
                trustedCertificate,
                null,
                SslPolicyErrors.None);

        Assert.True(
            accepted);
    }

    [Fact]
    public void Validate_ExactCertificateWithChainErrors_ShouldAccept()
    {
        using X509Certificate2 trustedCertificate =
            CreateCertificate();
        var validator =
            new RuntimeHostPinnedServerCertificateValidator(
                trustedCertificate);

        bool accepted =
            validator.Validate(
                this,
                trustedCertificate,
                null,
                SslPolicyErrors.RemoteCertificateChainErrors);

        Assert.True(
            accepted);
    }

    [Fact]
    public void Validate_ExactCertificateWithNameAndChainErrors_ShouldReject()
    {
        using X509Certificate2 trustedCertificate =
            CreateCertificate();
        var validator =
            new RuntimeHostPinnedServerCertificateValidator(
                trustedCertificate);

        bool accepted =
            validator.Validate(
                this,
                trustedCertificate,
                null,
                SslPolicyErrors.RemoteCertificateNameMismatch
                | SslPolicyErrors.RemoteCertificateChainErrors);

        Assert.False(
            accepted);
    }

    private static X509Certificate2 CreateCertificate()
    {
        using RSA rsa =
            RSA.Create(
                2048);
        CertificateRequest request =
            new(
                "CN=HASE generated pinned-server test",
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
