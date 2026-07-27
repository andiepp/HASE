using System.Text;

namespace Hase.Runtime.Remote.Grpc.Hosting.Tests;

public sealed class RuntimeHostPrivateNetworkDeploymentOptionsFileTests
{
    private const string Thumbprint =
        "0123456789ABCDEF0123456789ABCDEF01234567";

    [Fact]
    public async Task LoadAsync_NullPath_ShouldThrow()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            "filePath",
            () =>
                RuntimeHostPrivateNetworkDeploymentOptionsFile.LoadAsync(
                    null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("private-network-deployment.json")]
    public async Task LoadAsync_InvalidPath_ShouldThrow(
        string filePath)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            "filePath",
            () =>
                RuntimeHostPrivateNetworkDeploymentOptionsFile.LoadAsync(
                    filePath));
    }

    [Fact]
    public async Task LoadAsync_MissingFile_ShouldThrow()
    {
        using var directory =
            new TemporaryDirectory();

        await Assert.ThrowsAsync<FileNotFoundException>(
            () =>
                RuntimeHostPrivateNetworkDeploymentOptionsFile.LoadAsync(
                    Path.Combine(
                        directory.Path,
                        "missing.json")));
    }

    [Fact]
    public async Task LoadAsync_ValidDocument_ShouldReturnOptions()
    {
        using var directory =
            new TemporaryDirectory();
        string enrollmentFilePath =
            Path.Combine(
                directory.Path,
                "client-enrollments.json");
        using var document =
            await DeploymentDocument.CreateAsync(
                directory.Path,
                ValidDocument(
                    enrollmentFilePath));

        RuntimeHostPrivateNetworkDeploymentOptions options =
            await RuntimeHostPrivateNetworkDeploymentOptionsFile.LoadAsync(
                document.FilePath);

        Assert.Equal(
            "192.0.2.10",
            options.Binding.Address.ToString());
        Assert.Equal(
            5000,
            options.Binding.Port);
        Assert.Equal(
            Thumbprint,
            options.ServerCertificate.Thumbprint);
        Assert.Equal(
            enrollmentFilePath,
            options.ClientEnrollmentFilePath);
    }

    [Theory]
    [InlineData("{\"formatVersion\":2}")]
    [InlineData("{\"formatVersion\":1}")]
    [InlineData(
        "{\"formatVersion\":1,\"binding\":{"
        + "\"address\":\"0.0.0.0\",\"port\":5000},"
        + "\"serverCertificate\":{"
        + "\"storeName\":\"My\",\"storeLocation\":\"CurrentUser\","
        + "\"thumbprint\":\"0123456789ABCDEF0123456789ABCDEF01234567\"},"
        + "\"clientEnrollmentFilePath\":\"relative.json\"}")]
    [InlineData(
        "{\"formatVersion\":1,\"binding\":{"
        + "\"address\":\"host-name\",\"port\":5000},"
        + "\"serverCertificate\":{"
        + "\"storeName\":\"My\",\"storeLocation\":\"CurrentUser\","
        + "\"thumbprint\":\"0123456789ABCDEF0123456789ABCDEF01234567\"},"
        + "\"clientEnrollmentFilePath\":\"relative.json\"}")]
    [InlineData(
        "{\"formatVersion\":1,\"unexpected\":true}")]
    public async Task LoadAsync_InvalidDocument_ShouldThrow(
        string contents)
    {
        using var directory =
            new TemporaryDirectory();
        using var document =
            await DeploymentDocument.CreateAsync(
                directory.Path,
                contents);

        await Assert.ThrowsAsync<InvalidDataException>(
            () =>
                RuntimeHostPrivateNetworkDeploymentOptionsFile.LoadAsync(
                    document.FilePath));
    }

    [Fact]
    public async Task LoadAsync_OversizedDocument_ShouldThrow()
    {
        using var directory =
            new TemporaryDirectory();
        using var document =
            await DeploymentDocument.CreateAsync(
                directory.Path,
                new string(
                    ' ',
                    (64 * 1024) + 1));

        await Assert.ThrowsAsync<InvalidDataException>(
            () =>
                RuntimeHostPrivateNetworkDeploymentOptionsFile.LoadAsync(
                    document.FilePath));
    }

    [Fact]
    public async Task LoadAsync_PreCancelled_ShouldThrow()
    {
        using var directory =
            new TemporaryDirectory();
        using var document =
            await DeploymentDocument.CreateAsync(
                directory.Path,
                ValidDocument(
                    Path.Combine(
                        directory.Path,
                        "client-enrollments.json")));
        using var cancellationSource =
            new CancellationTokenSource();

        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                RuntimeHostPrivateNetworkDeploymentOptionsFile.LoadAsync(
                    document.FilePath,
                    cancellationSource.Token));
    }

    private static string ValidDocument(
        string enrollmentFilePath)
    {
        return "{\"formatVersion\":1,\"binding\":{"
            + "\"address\":\"192.0.2.10\",\"port\":5000},"
            + "\"serverCertificate\":{"
            + "\"storeName\":\"My\",\"storeLocation\":\"CurrentUser\","
            + "\"thumbprint\":\""
            + Thumbprint
            + "\"},\"clientEnrollmentFilePath\":"
            + System.Text.Json.JsonSerializer.Serialize(
                enrollmentFilePath)
            + "}";
    }

    private sealed class DeploymentDocument
        : IDisposable
    {
        private DeploymentDocument(
            string filePath)
        {
            FilePath =
                filePath;
        }

        public string FilePath
        {
            get;
        }

        public static async Task<DeploymentDocument> CreateAsync(
            string directoryPath,
            string contents)
        {
            string filePath =
                Path.Combine(
                    directoryPath,
                    $"private-network-deployment-{Guid.NewGuid():N}.json");

            await File.WriteAllTextAsync(
                filePath,
                contents,
                Encoding.UTF8);

            return new DeploymentDocument(
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
                    $"hase-private-network-options-{Guid.NewGuid():N}");

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
