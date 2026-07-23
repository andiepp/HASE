using Hase.Runtime.Northbound;

namespace Hase.Runtime.Tests.Northbound;

public sealed class RuntimeHostIdentityFileCreationTests
{
    [Fact]
    public async Task CreateIfMissingAsync_NullCandidate_Throws()
    {
        using var directory =
            new TemporaryDirectory();

        var identityFile =
            new RuntimeHostIdentityFile(
                Path.Combine(
                    directory.Path,
                    "runtime-host-identity.json"));

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => identityFile.CreateIfMissingAsync(
                null!));
    }

    [Fact]
    public async Task CreateIfMissingAsync_MissingTarget_CreatesDocument()
    {
        using var directory =
            new TemporaryDirectory();

        string filePath =
            Path.Combine(
                directory.Path,
                "nested",
                "runtime-host-identity.json");

        var candidate =
            new RuntimeHostId(
                "runtime-host-created");

        var identityFile =
            new RuntimeHostIdentityFile(
                filePath);

        RuntimeHostIdentityStoreCreateResult result =
            await identityFile.CreateIfMissingAsync(
                candidate);

        Assert.Same(
            candidate,
            result.RuntimeHostId);

        Assert.Equal(
            RuntimeHostIdentityStoreCreateOutcome.Created,
            result.Outcome);

        Assert.True(
            File.Exists(
                filePath));

        Assert.Equal(
            candidate,
            await identityFile.ReadAsync());
    }

    [Fact]
    public async Task CreateIfMissingAsync_ExistingTarget_ReturnsExistingIdentity()
    {
        using var directory =
            new TemporaryDirectory();

        string filePath =
            Path.Combine(
                directory.Path,
                "runtime-host-identity.json");

        var existingRuntimeHostId =
            new RuntimeHostId(
                "runtime-host-existing");

        byte[] existingDocument =
            RuntimeHostIdentityDocumentCodec.Serialize(
                existingRuntimeHostId);

        await File.WriteAllBytesAsync(
            filePath,
            existingDocument);

        var identityFile =
            new RuntimeHostIdentityFile(
                filePath);

        RuntimeHostIdentityStoreCreateResult result =
            await identityFile.CreateIfMissingAsync(
                new RuntimeHostId(
                    "runtime-host-candidate"));

        Assert.Equal(
            existingRuntimeHostId,
            result.RuntimeHostId);

        Assert.Equal(
            RuntimeHostIdentityStoreCreateOutcome.Existing,
            result.Outcome);

        Assert.Equal(
            existingDocument,
            await File.ReadAllBytesAsync(
                filePath));
    }

    [Fact]
    public async Task CreateIfMissingAsync_ConcurrentCallers_Converge()
    {
        using var directory =
            new TemporaryDirectory();

        string filePath =
            Path.Combine(
                directory.Path,
                "runtime-host-identity.json");

        Task<RuntimeHostIdentityStoreCreateResult>[] tasks =
            Enumerable.Range(
                    0,
                    8)
                .Select(
                    index =>
                        new RuntimeHostIdentityFile(
                                filePath)
                            .CreateIfMissingAsync(
                                new RuntimeHostId(
                                    $"runtime-host-candidate-{index}")))
                .ToArray();

        RuntimeHostIdentityStoreCreateResult[] results =
            await Task.WhenAll(
                tasks);

        Assert.Single(
            results.Where(
                result =>
                    result.Outcome
                    == RuntimeHostIdentityStoreCreateOutcome.Created));

        RuntimeHostId authoritativeRuntimeHostId =
            results[0].RuntimeHostId;

        Assert.All(
            results,
            result =>
                Assert.Equal(
                    authoritativeRuntimeHostId,
                    result.RuntimeHostId));

        Assert.DoesNotContain(
            Directory.EnumerateFiles(
                directory.Path),
            path =>
                path.EndsWith(
                    ".tmp",
                    StringComparison.Ordinal));
    }

    [Fact]
    public async Task CreateIfMissingAsync_InvalidExistingTarget_FailsWithoutReplacement()
    {
        using var directory =
            new TemporaryDirectory();

        string filePath =
            Path.Combine(
                directory.Path,
                "runtime-host-identity.json");

        byte[] invalidDocument =
        [
            (byte)'{',
            (byte)'}',
        ];

        await File.WriteAllBytesAsync(
            filePath,
            invalidDocument);

        var identityFile =
            new RuntimeHostIdentityFile(
                filePath);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => identityFile.CreateIfMissingAsync(
                new RuntimeHostId(
                    "runtime-host-candidate")));

        Assert.Equal(
            invalidDocument,
            await File.ReadAllBytesAsync(
                filePath));

        Assert.DoesNotContain(
            Directory.EnumerateFiles(
                directory.Path),
            path =>
                path.EndsWith(
                    ".tmp",
                    StringComparison.Ordinal));
    }

    [Fact]
    public async Task CreateIfMissingAsync_PreCancelled_DoesNotCreateDirectory()
    {
        using var directory =
            new TemporaryDirectory();

        string parentDirectoryPath =
            Path.Combine(
                directory.Path,
                "not-created");

        var identityFile =
            new RuntimeHostIdentityFile(
                Path.Combine(
                    parentDirectoryPath,
                    "runtime-host-identity.json"));

        using var cancellationSource =
            new CancellationTokenSource();

        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => identityFile.CreateIfMissingAsync(
                new RuntimeHostId(
                    "runtime-host-candidate"),
                cancellationSource.Token));

        Assert.False(
            Directory.Exists(
                parentDirectoryPath));
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