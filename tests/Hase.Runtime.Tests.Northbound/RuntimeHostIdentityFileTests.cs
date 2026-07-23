using System.Text;
using Hase.Runtime.Northbound;

namespace Hase.Runtime.Tests.Northbound;

public sealed class RuntimeHostIdentityFileTests
{
    [Fact]
    public void Constructor_NullPath_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeHostIdentityFile(
                null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("runtime-host-identity.json")]
    public void Constructor_InvalidPath_Throws(
        string filePath)
    {
        Assert.Throws<ArgumentException>(
            () => new RuntimeHostIdentityFile(
                filePath));
    }

    [Fact]
    public void Constructor_FullyQualifiedPath_NormalizesPath()
    {
        using var directory =
            new TemporaryDirectory();

        string filePath =
            Path.Combine(
                directory.Path,
                "nested",
                "..",
                "runtime-host-identity.json");

        var identityFile =
            new RuntimeHostIdentityFile(
                filePath);

        Assert.Equal(
            Path.GetFullPath(
                filePath),
            identityFile.FilePath);
    }

    [Fact]
    public async Task ReadAsync_MissingTarget_ReturnsNull()
    {
        using var directory =
            new TemporaryDirectory();

        var identityFile =
            new RuntimeHostIdentityFile(
                Path.Combine(
                    directory.Path,
                    "missing",
                    "runtime-host-identity.json"));

        RuntimeHostId? runtimeHostId =
            await identityFile.ReadAsync();

        Assert.Null(
            runtimeHostId);
    }

    [Fact]
    public async Task ReadAsync_ValidDocument_ReturnsIdentity()
    {
        using var directory =
            new TemporaryDirectory();

        string filePath =
            Path.Combine(
                directory.Path,
                "runtime-host-identity.json");

        var expectedRuntimeHostId =
            new RuntimeHostId(
                "runtime-host-persisted");

        await File.WriteAllBytesAsync(
            filePath,
            RuntimeHostIdentityDocumentCodec.Serialize(
                expectedRuntimeHostId));

        var identityFile =
            new RuntimeHostIdentityFile(
                filePath);

        RuntimeHostId? actualRuntimeHostId =
            await identityFile.ReadAsync();

        Assert.Equal(
            expectedRuntimeHostId,
            actualRuntimeHostId);
    }

    [Fact]
    public async Task ReadAsync_InvalidDocument_Throws()
    {
        using var directory =
            new TemporaryDirectory();

        string filePath =
            Path.Combine(
                directory.Path,
                "runtime-host-identity.json");

        await File.WriteAllTextAsync(
            filePath,
            "{\"formatVersion\":2}",
            Encoding.UTF8);

        var identityFile =
            new RuntimeHostIdentityFile(
                filePath);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => identityFile.ReadAsync());
    }

    [Fact]
    public async Task ReadAsync_OversizedDocument_Throws()
    {
        using var directory =
            new TemporaryDirectory();

        string filePath =
            Path.Combine(
                directory.Path,
                "runtime-host-identity.json");

        await File.WriteAllBytesAsync(
            filePath,
            new byte[
                RuntimeHostIdentityDocumentCodec.MaximumDocumentByteCount
                + 1]);

        var identityFile =
            new RuntimeHostIdentityFile(
                filePath);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => identityFile.ReadAsync());
    }

    [Fact]
    public async Task ReadAsync_PreCancelled_Throws()
    {
        using var directory =
            new TemporaryDirectory();

        string filePath =
            Path.Combine(
                directory.Path,
                "runtime-host-identity.json");

        await File.WriteAllBytesAsync(
            filePath,
            RuntimeHostIdentityDocumentCodec.Serialize(
                new RuntimeHostId(
                    "runtime-host-persisted")));

        var identityFile =
            new RuntimeHostIdentityFile(
                filePath);

        using var cancellationSource =
            new CancellationTokenSource();

        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => identityFile.ReadAsync(
                cancellationSource.Token));
    }

    private sealed class TemporaryDirectory
        : IDisposable
    {
        public TemporaryDirectory()
        {
            Path =
                System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    $"hase-runtime-host-identity-{Guid.NewGuid():N}");

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