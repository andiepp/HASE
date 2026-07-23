using Hase.Runtime.Northbound;

namespace Hase.Runtime.Tests.Northbound;

public sealed class FileRuntimeHostIdentityStoreTests
{
    [Fact]
    public void Constructor_NullPath_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new FileRuntimeHostIdentityStore(
                null!));
    }

    [Fact]
    public void Constructor_RelativePath_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new FileRuntimeHostIdentityStore(
                "runtime-host-identity.json"));
    }

    [Fact]
    public async Task ReadAsync_MissingTarget_ReturnsNull()
    {
        using var directory =
            new TemporaryDirectory();

        IRuntimeHostIdentityStore store =
            new FileRuntimeHostIdentityStore(
                Path.Combine(
                    directory.Path,
                    "missing",
                    "runtime-host-identity.json"));

        RuntimeHostId? runtimeHostId =
            await store.ReadAsync();

        Assert.Null(
            runtimeHostId);
    }

    [Fact]
    public async Task ReadAsync_ExistingTarget_ReturnsPersistedIdentity()
    {
        using var directory =
            new TemporaryDirectory();

        string filePath =
            Path.Combine(
                directory.Path,
                "runtime-host-identity.json");

        var expectedRuntimeHostId =
            new RuntimeHostId(
                "runtime-host-existing");

        await File.WriteAllBytesAsync(
            filePath,
            RuntimeHostIdentityDocumentCodec.Serialize(
                expectedRuntimeHostId));

        IRuntimeHostIdentityStore store =
            new FileRuntimeHostIdentityStore(
                filePath);

        RuntimeHostId? actualRuntimeHostId =
            await store.ReadAsync();

        Assert.Equal(
            expectedRuntimeHostId,
            actualRuntimeHostId);
    }

    [Fact]
    public async Task CreateIfMissingAsync_RepeatedStores_PreserveFirstIdentity()
    {
        using var directory =
            new TemporaryDirectory();

        string filePath =
            Path.Combine(
                directory.Path,
                "runtime-host-identity.json");

        IRuntimeHostIdentityStore firstStore =
            new FileRuntimeHostIdentityStore(
                filePath);

        IRuntimeHostIdentityStore secondStore =
            new FileRuntimeHostIdentityStore(
                filePath);

        var firstCandidate =
            new RuntimeHostId(
                "runtime-host-first");

        RuntimeHostIdentityStoreCreateResult firstResult =
            await firstStore.CreateIfMissingAsync(
                firstCandidate);

        RuntimeHostIdentityStoreCreateResult secondResult =
            await secondStore.CreateIfMissingAsync(
                new RuntimeHostId(
                    "runtime-host-second"));

        Assert.Equal(
            RuntimeHostIdentityStoreCreateOutcome.Created,
            firstResult.Outcome);

        Assert.Equal(
            RuntimeHostIdentityStoreCreateOutcome.Existing,
            secondResult.Outcome);

        Assert.Equal(
            firstCandidate,
            firstResult.RuntimeHostId);

        Assert.Equal(
            firstCandidate,
            secondResult.RuntimeHostId);

        Assert.Equal(
            firstCandidate,
            await secondStore.ReadAsync());
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