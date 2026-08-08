using System.Text;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostAuthorizationPolicyFileTests
{
    [Fact]
    public async Task Load_InMemoryAndFileDocuments_ShouldHaveExactParity()
    {
        byte[] bytes = Encoding.UTF8.GetBytes(ValidDocument());
        using var document = await PolicyDocument.CreateAsync(ValidDocument());

        RuntimeHostAuthorizationPolicy fromMemory =
            RuntimeHostAuthorizationPolicyFile.Load(bytes);
        RuntimeHostAuthorizationPolicy fromFile =
            await RuntimeHostAuthorizationPolicyFile.LoadAsync(document.FilePath);

        Assert.Equal(
            fromFile.IsGranted("remote-client", RuntimeHostPermission.SubscribeDiagnostics),
            fromMemory.IsGranted("remote-client", RuntimeHostPermission.SubscribeDiagnostics));
    }

    [Fact]
    public void Load_OversizedInMemoryDocument_ShouldThrow()
    {
        Assert.Throws<InvalidDataException>(() =>
            RuntimeHostAuthorizationPolicyFile.Load(new byte[(64 * 1024) + 1]));
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{\"formatVersion\":1,\"grants\":[],\"unknown\":true}")]
    [InlineData("{\"formatVersion\":1,\"grants\":[null]}")]
    public void Load_InvalidInMemoryDocument_ShouldThrow(string contents)
    {
        Assert.Throws<InvalidDataException>(() =>
            RuntimeHostAuthorizationPolicyFile.Load(
                Encoding.UTF8.GetBytes(contents)));
    }

    [Fact]
    public async Task LoadAsync_NullPath_ShouldThrow()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            "filePath",
            () => RuntimeHostAuthorizationPolicyFile.LoadAsync(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("runtime-host-authorization.json")]
    public async Task LoadAsync_InvalidPath_ShouldThrow(string filePath)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            "filePath",
            () => RuntimeHostAuthorizationPolicyFile.LoadAsync(filePath));
    }

    [Fact]
    public async Task LoadAsync_MissingFile_ShouldThrow()
    {
        using var directory = new TemporaryDirectory();

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            RuntimeHostAuthorizationPolicyFile.LoadAsync(
                Path.Combine(directory.Path, "missing.json")));
    }

    [Fact]
    public async Task LoadAsync_AllExactPermissions_ShouldCreatePolicy()
    {
        (string Name, RuntimeHostPermission Permission)[] permissions =
        [
            ("runtime-host.snapshot.read", RuntimeHostPermission.ReadSnapshot),
            ("property.cached.read", RuntimeHostPermission.ReadCachedProperty),
            ("property.authoritative.read",
                RuntimeHostPermission.ReadAuthoritativeProperty),
            ("property.write", RuntimeHostPermission.WriteProperty),
            ("command.execute", RuntimeHostPermission.ExecuteCommand),
            ("observation.subscribe",
                RuntimeHostPermission.SubscribeObservation),
            ("diagnostics.subscribe",
                RuntimeHostPermission.SubscribeDiagnostics)
        ];
        string grants = string.Join(
            ",",
            permissions.Select(permission =>
                "{\"principalId\":\"remote-client\",\"permission\":\""
                + permission.Name
                + "\"}"));
        using var document = await PolicyDocument.CreateAsync(
            "{\"formatVersion\":1,\"grants\":[" + grants + "]}");

        RuntimeHostAuthorizationPolicy policy =
            await RuntimeHostAuthorizationPolicyFile.LoadAsync(
                document.FilePath);

        foreach ((string _, RuntimeHostPermission permission) in permissions)
        {
            Assert.True(policy.IsGranted("remote-client", permission));
        }
        Assert.False(
            policy.IsGranted("another-client", RuntimeHostPermission.ReadSnapshot));
    }

    [Fact]
    public async Task LoadAsync_Utf8Bom_ShouldCreatePolicy()
    {
        using var document = await PolicyDocument.CreateAsync(
            ValidDocument(),
            includeUtf8Bom: true);

        RuntimeHostAuthorizationPolicy policy =
            await RuntimeHostAuthorizationPolicyFile.LoadAsync(
                document.FilePath);

        Assert.True(
            policy.IsGranted(
                "remote-client",
                RuntimeHostPermission.SubscribeDiagnostics));
    }

    [Fact]
    public async Task LoadAsync_EmptyGrants_ShouldCreateDefaultDenyPolicy()
    {
        using var document = await PolicyDocument.CreateAsync(
            "{\"formatVersion\":1,\"grants\":[]}");

        RuntimeHostAuthorizationPolicy policy =
            await RuntimeHostAuthorizationPolicyFile.LoadAsync(
                document.FilePath);

        Assert.False(
            policy.IsGranted(
                "remote-client",
                RuntimeHostPermission.SubscribeDiagnostics));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{\"grants\":[]}")]
    [InlineData("{\"formatVersion\":2,\"grants\":[]}")]
    [InlineData("{\"formatVersion\":1}")]
    [InlineData("{\"formatVersion\":1,\"grants\":null}")]
    [InlineData("{\"formatVersion\":1,\"grants\":[null]}")]
    [InlineData(
        "{\"formatVersion\":1,\"grants\":["
        + "{\"permission\":\"diagnostics.subscribe\"}]}")]
    [InlineData(
        "{\"formatVersion\":1,\"grants\":["
        + "{\"principalId\":\" \",\"permission\":\"diagnostics.subscribe\"}]}")]
    [InlineData(
        "{\"formatVersion\":1,\"grants\":["
        + "{\"principalId\":\"remote-client\"}]}")]
    [InlineData(
        "{\"formatVersion\":1,\"grants\":["
        + "{\"principalId\":\"remote-client\",\"permission\":\" \"}]}")]
    [InlineData(
        "{\"formatVersion\":1,\"grants\":["
        + "{\"principalId\":\"remote-client\",\"permission\":\"unknown\"}]}")]
    [InlineData(
        "{\"formatVersion\":1,\"grants\":["
        + "{\"principalId\":\"remote-client\","
        + "\"permission\":\"Diagnostics.Subscribe\"}]}")]
    [InlineData("{\"formatVersion\":1,\"grants\":[],\"unexpected\":true}")]
    [InlineData("not-json")]
    public async Task LoadAsync_InvalidDocument_ShouldThrow(string contents)
    {
        using var document = await PolicyDocument.CreateAsync(contents);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            RuntimeHostAuthorizationPolicyFile.LoadAsync(document.FilePath));
    }

    [Fact]
    public async Task LoadAsync_DuplicateGrant_ShouldThrow()
    {
        const string grant =
            "{\"principalId\":\"remote-client\","
            + "\"permission\":\"diagnostics.subscribe\"}";
        using var document = await PolicyDocument.CreateAsync(
            "{\"formatVersion\":1,\"grants\":["
            + grant
            + ","
            + grant
            + "]}");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            RuntimeHostAuthorizationPolicyFile.LoadAsync(document.FilePath));
    }

    [Fact]
    public async Task LoadAsync_OversizedDocument_ShouldThrow()
    {
        using var document = await PolicyDocument.CreateAsync(
            new string(' ', (64 * 1024) + 1));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            RuntimeHostAuthorizationPolicyFile.LoadAsync(document.FilePath));
    }

    [Fact]
    public async Task LoadAsync_PreCancelled_ShouldThrow()
    {
        using var document = await PolicyDocument.CreateAsync(ValidDocument());
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            RuntimeHostAuthorizationPolicyFile.LoadAsync(
                document.FilePath,
                cancellationSource.Token));
    }

    private static string ValidDocument() =>
        "{\"formatVersion\":1,\"grants\":["
        + "{\"principalId\":\"remote-client\","
        + "\"permission\":\"diagnostics.subscribe\"}]}";

    private sealed class PolicyDocument : IDisposable
    {
        private PolicyDocument(string directoryPath, string filePath)
        {
            DirectoryPath = directoryPath;
            FilePath = filePath;
        }

        public string DirectoryPath { get; }
        public string FilePath { get; }

        public static async Task<PolicyDocument> CreateAsync(
            string contents,
            bool includeUtf8Bom = false)
        {
            string directoryPath = Path.Combine(
                Path.GetTempPath(),
                $"hase-authorization-policy-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directoryPath);
            string filePath = Path.Combine(
                directoryPath,
                "runtime-host-authorization.json");
            var encoding = new UTF8Encoding(includeUtf8Bom);

            await File.WriteAllTextAsync(filePath, contents, encoding);

            return new PolicyDocument(directoryPath, filePath);
        }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"hase-authorization-policy-missing-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
