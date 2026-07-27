using System.Text;

namespace Hase.Runtime.Remote.Grpc.Hosting.Tests;

public sealed class RuntimeHostPrivateNetworkClientOptionsFileTests
{
    private const string ClientThumbprint =
        "0123456789ABCDEF0123456789ABCDEF01234567";

    private const string ServerThumbprint =
        "89ABCDEF0123456789ABCDEF0123456789ABCDEF";

    [Fact]
    public async Task LoadAsync_NullPath_ShouldThrow()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            "filePath",
            () =>
                RuntimeHostPrivateNetworkClientOptionsFile.LoadAsync(
                    null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("private-network-client.json")]
    public async Task LoadAsync_InvalidPath_ShouldThrow(
        string filePath)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            "filePath",
            () =>
                RuntimeHostPrivateNetworkClientOptionsFile.LoadAsync(
                    filePath));
    }

    [Fact]
    public async Task LoadAsync_MissingFile_ShouldThrow()
    {
        using var directory =
            new TemporaryDirectory();

        await Assert.ThrowsAsync<FileNotFoundException>(
            () =>
                RuntimeHostPrivateNetworkClientOptionsFile.LoadAsync(
                    Path.Combine(
                        directory.Path,
                        "missing.json")));
    }

    [Fact]
    public async Task LoadAsync_ValidDocument_ShouldReturnOptions()
    {
        using var directory =
            new TemporaryDirectory();
        using var document =
            await ClientDocument.CreateAsync(
                directory.Path,
                ValidDocument());

        RuntimeHostPrivateNetworkClientOptions options =
            await RuntimeHostPrivateNetworkClientOptionsFile.LoadAsync(
                document.FilePath);

        Assert.Equal(
            new Uri(
                "https://192.0.2.10:5000"),
            options.Address);
        Assert.Equal(
            ClientThumbprint,
            options.ClientCertificate.Thumbprint);
        Assert.Equal(
            ServerThumbprint,
            options.TrustedServerCertificate.Thumbprint);
    }

    [Theory]
    [InlineData("{\"formatVersion\":2}")]
    [InlineData("{\"formatVersion\":1}")]
    [InlineData(
        "{\"formatVersion\":1,"
        + "\"address\":\"http://192.0.2.10:5000\","
        + "\"clientCertificate\":{"
        + "\"storeName\":\"My\",\"storeLocation\":\"CurrentUser\","
        + "\"thumbprint\":\"0123456789ABCDEF0123456789ABCDEF01234567\"},"
        + "\"trustedServerCertificate\":{"
        + "\"storeName\":\"CertificateAuthority\","
        + "\"storeLocation\":\"CurrentUser\","
        + "\"thumbprint\":\"89ABCDEF0123456789ABCDEF0123456789ABCDEF\"}}")]
    [InlineData(
        "{\"formatVersion\":1,"
        + "\"address\":\"https://runtime-host.example:5000\","
        + "\"clientCertificate\":{},"
        + "\"trustedServerCertificate\":{}}")]
    [InlineData(
        "{\"formatVersion\":1,\"unexpected\":true}")]
    public async Task LoadAsync_InvalidDocument_ShouldThrow(
        string contents)
    {
        using var directory =
            new TemporaryDirectory();
        using var document =
            await ClientDocument.CreateAsync(
                directory.Path,
                contents);

        await Assert.ThrowsAsync<InvalidDataException>(
            () =>
                RuntimeHostPrivateNetworkClientOptionsFile.LoadAsync(
                    document.FilePath));
    }

    [Fact]
    public async Task LoadAsync_OversizedDocument_ShouldThrow()
    {
        using var directory =
            new TemporaryDirectory();
        using var document =
            await ClientDocument.CreateAsync(
                directory.Path,
                new string(
                    ' ',
                    (64 * 1024) + 1));

        await Assert.ThrowsAsync<InvalidDataException>(
            () =>
                RuntimeHostPrivateNetworkClientOptionsFile.LoadAsync(
                    document.FilePath));
    }

    [Fact]
    public async Task LoadAsync_PreCancelled_ShouldThrow()
    {
        using var directory =
            new TemporaryDirectory();
        using var document =
            await ClientDocument.CreateAsync(
                directory.Path,
                ValidDocument());
        using var cancellationSource =
            new CancellationTokenSource();

        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                RuntimeHostPrivateNetworkClientOptionsFile.LoadAsync(
                    document.FilePath,
                    cancellationSource.Token));
    }

    private static string ValidDocument()
    {
        return "{\"formatVersion\":1,"
            + "\"address\":\"https://192.0.2.10:5000\","
            + "\"clientCertificate\":{"
            + "\"storeName\":\"My\",\"storeLocation\":\"CurrentUser\","
            + "\"thumbprint\":\""
            + ClientThumbprint
            + "\"},\"trustedServerCertificate\":{"
            + "\"storeName\":\"CertificateAuthority\","
            + "\"storeLocation\":\"CurrentUser\","
            + "\"thumbprint\":\""
            + ServerThumbprint
            + "\"}}";
    }

    private sealed class ClientDocument
        : IDisposable
    {
        private ClientDocument(
            string filePath)
        {
            FilePath =
                filePath;
        }

        public string FilePath
        {
            get;
        }

        public static async Task<ClientDocument> CreateAsync(
            string directoryPath,
            string contents)
        {
            string filePath =
                Path.Combine(
                    directoryPath,
                    $"private-network-client-{Guid.NewGuid():N}.json");

            await File.WriteAllTextAsync(
                filePath,
                contents,
                Encoding.UTF8);

            return new ClientDocument(
                filePath);
        }

        public void Dispose()
        {
            File.Delete(
                FilePath);
        }
    }

    private sealed class TemporaryDirectory
        : IDisposable
    {
        public TemporaryDirectory()
        {
            Path =
                System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    $"hase-private-network-client-{Guid.NewGuid():N}");

            Directory.CreateDirectory(
                Path);
        }

        public string Path
        {
            get;
        }

        public void Dispose()
        {
            if (Directory.Exists(
                    Path))
            {
                Directory.Delete(
                    Path,
                    recursive: true);
            }
        }
    }
}
