using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Hase.ProtocolExplorer.Scenarios;
using Xunit;

namespace Hase.ProtocolExplorer.Tests;

public sealed class CapabilityC034SecureGrpcClientTests
{
    [Fact]
    public void Create_NonHttpsAddress_ShouldReject()
    {
        using X509Certificate2 clientCertificate =
            CreateCertificate(
                "CN=hase-c034-client");
        using X509Certificate2 serverCertificate =
            CreateCertificate(
                "CN=localhost");

        Assert.Throws<ArgumentException>(
            "address",
            () =>
                CapabilityC034SecureGrpcClient.Create(
                    new Uri(
                        "http://127.0.0.1:5000"),
                    clientCertificate,
                    serverCertificate));
    }

    [Fact]
    public void Dispose_CalledTwice_ShouldBeIdempotent()
    {
        using X509Certificate2 clientCertificate =
            CreateCertificate(
                "CN=hase-c034-client");
        using X509Certificate2 serverCertificate =
            CreateCertificate(
                "CN=localhost");
        CapabilityC034SecureGrpcClient client =
            CapabilityC034SecureGrpcClient.Create(
                new Uri(
                    "https://127.0.0.1:5001"),
                clientCertificate,
                serverCertificate);

        client.Dispose();
        client.Dispose();
    }

    private static X509Certificate2 CreateCertificate(
        string subjectName)
    {
        using RSA rsa =
            RSA.Create(
                2048);
        CertificateRequest request =
            new(
                subjectName,
                rsa,
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

        DateTimeOffset nowUtc =
            DateTimeOffset.UtcNow;

        return request.CreateSelfSigned(
            nowUtc.AddMinutes(
                -1),
            nowUtc.AddDays(
                1));
    }
}
