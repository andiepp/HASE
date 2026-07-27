using System.Security.Cryptography.X509Certificates;

namespace Hase.Runtime.Remote.Grpc.Hosting.Tests;

public sealed class RuntimeHostCertificateStoreReferenceTests
{
    private const string Thumbprint =
        "0123456789ABCDEF0123456789ABCDEF01234567";

    [Fact]
    public void Constructor_UndefinedStoreName_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            "storeName",
            () =>
                new RuntimeHostCertificateStoreReference(
                    (StoreName)(-1),
                    StoreLocation.CurrentUser,
                    Thumbprint));
    }

    [Fact]
    public void Constructor_UndefinedStoreLocation_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            "storeLocation",
            () =>
                new RuntimeHostCertificateStoreReference(
                    StoreName.My,
                    (StoreLocation)(-1),
                    Thumbprint));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_MissingThumbprint_ShouldThrow(
        string? thumbprint)
    {
        Assert.ThrowsAny<ArgumentException>(
            () =>
                new RuntimeHostCertificateStoreReference(
                    StoreName.My,
                    StoreLocation.CurrentUser,
                    thumbprint!));
    }

    [Theory]
    [InlineData("0123456789ABCDEF")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF0123456789")]
    [InlineData("G123456789ABCDEF0123456789ABCDEF01234567")]
    public void Constructor_InvalidThumbprint_ShouldThrow(
        string thumbprint)
    {
        ArgumentException exception =
            Assert.Throws<ArgumentException>(
                "thumbprint",
                () =>
                    new RuntimeHostCertificateStoreReference(
                        StoreName.My,
                        StoreLocation.CurrentUser,
                        thumbprint));

        Assert.Equal(
            "The certificate thumbprint must contain exactly 40 "
            + "hexadecimal characters. (Parameter 'thumbprint')",
            exception.Message);
    }

    [Fact]
    public void Constructor_ValidReference_ShouldPreserveStore()
    {
        var reference =
            new RuntimeHostCertificateStoreReference(
                StoreName.My,
                StoreLocation.LocalMachine,
                Thumbprint);

        Assert.Equal(
            StoreName.My,
            reference.StoreName);
        Assert.Equal(
            StoreLocation.LocalMachine,
            reference.StoreLocation);
    }

    [Fact]
    public void Constructor_FormattedThumbprint_ShouldNormalize()
    {
        var reference =
            new RuntimeHostCertificateStoreReference(
                StoreName.My,
                StoreLocation.CurrentUser,
                "01:23:45:67:89:ab:cd:ef:01:23:45:67:89:ab:cd:ef:"
                + "01:23:45:67");

        Assert.Equal(
            Thumbprint,
            reference.Thumbprint);
    }
}
