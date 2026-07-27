using System.Text;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostClientCredentialEnrollmentRegistryFileTests
{
    private const string CredentialId =
        "x509-sha256:"
        + "0123456789abcdef0123456789abcdef"
        + "0123456789abcdef0123456789abcdef";

    private static readonly DateTimeOffset AuthenticationTimeUtc =
        new(
            2026,
            7,
            26,
            12,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public async Task LoadAsync_NullPath_ShouldThrow()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            "filePath",
            () =>
                RuntimeHostClientCredentialEnrollmentRegistryFile.LoadAsync(
                    null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("client-enrollments.json")]
    public async Task LoadAsync_InvalidPath_ShouldThrow(
        string filePath)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            "filePath",
            () =>
                RuntimeHostClientCredentialEnrollmentRegistryFile.LoadAsync(
                    filePath));
    }

    [Fact]
    public async Task LoadAsync_MissingFile_ShouldThrow()
    {
        using var directory =
            new TemporaryDirectory();

        await Assert.ThrowsAsync<FileNotFoundException>(
            () =>
                RuntimeHostClientCredentialEnrollmentRegistryFile.LoadAsync(
                    Path.Combine(
                        directory.Path,
                        "missing.json")));
    }

    [Fact]
    public async Task LoadAsync_ValidDocument_ShouldResolvePrincipal()
    {
        using var document =
            await EnrollmentDocument.CreateAsync(
                ValidDocument());

        RuntimeHostClientCredentialEnrollmentRegistry registry =
            await RuntimeHostClientCredentialEnrollmentRegistryFile.LoadAsync(
                document.FilePath);

        bool resolved =
            registry.TryResolve(
                CreateCredentialIdentity(),
                AuthenticationTimeUtc,
                out RuntimeHostClientPrincipal? principal);

        Assert.True(
            resolved);
        Assert.NotNull(
            principal);
        Assert.Equal(
            "remote-client",
            principal.PrincipalId);
        Assert.Equal(
            "private-network-trust-v1",
            principal.TrustPolicyId);
    }

    [Theory]
    [InlineData("{\"formatVersion\":2,\"enrollments\":[]}")]
    [InlineData("{\"formatVersion\":1,\"enrollments\":[]}")]
    [InlineData("{\"formatVersion\":1}")]
    [InlineData("{\"formatVersion\":1,\"enrollments\":[null]}")]
    [InlineData(
        "{\"formatVersion\":1,\"enrollments\":["
        + "{\"credentialId\":\"invalid\","
        + "\"principalId\":\"remote-client\","
        + "\"trustPolicyId\":\"private-network-trust-v1\"}]}")]
    [InlineData(
        "{\"formatVersion\":1,\"enrollments\":[],"
        + "\"unexpected\":true}")]
    [InlineData("not-json")]
    public async Task LoadAsync_InvalidDocument_ShouldThrow(
        string contents)
    {
        using var document =
            await EnrollmentDocument.CreateAsync(
                contents);

        await Assert.ThrowsAsync<InvalidDataException>(
            () =>
                RuntimeHostClientCredentialEnrollmentRegistryFile.LoadAsync(
                    document.FilePath));
    }

    [Fact]
    public async Task LoadAsync_DuplicateCredential_ShouldThrow()
    {
        string enrollment =
            "{\"credentialId\":\""
            + CredentialId
            + "\",\"principalId\":\"remote-client\","
            + "\"trustPolicyId\":\"private-network-trust-v1\"}";
        using var document =
            await EnrollmentDocument.CreateAsync(
                "{\"formatVersion\":1,\"enrollments\":["
                + enrollment
                + ","
                + enrollment
                + "]}");

        await Assert.ThrowsAsync<InvalidDataException>(
            () =>
                RuntimeHostClientCredentialEnrollmentRegistryFile.LoadAsync(
                    document.FilePath));
    }

    [Fact]
    public async Task LoadAsync_OversizedDocument_ShouldThrow()
    {
        using var document =
            await EnrollmentDocument.CreateAsync(
                new string(
                    ' ',
                    (64 * 1024) + 1));

        await Assert.ThrowsAsync<InvalidDataException>(
            () =>
                RuntimeHostClientCredentialEnrollmentRegistryFile.LoadAsync(
                    document.FilePath));
    }

    [Fact]
    public async Task LoadAsync_PreCancelled_ShouldThrow()
    {
        using var document =
            await EnrollmentDocument.CreateAsync(
                ValidDocument());
        using var cancellationSource =
            new CancellationTokenSource();

        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                RuntimeHostClientCredentialEnrollmentRegistryFile.LoadAsync(
                    document.FilePath,
                    cancellationSource.Token));
    }

    private static RuntimeHostClientCredentialIdentity
        CreateCredentialIdentity()
    {
        return new RuntimeHostClientCredentialIdentity(
            RuntimeHostAuthenticationMechanism.MutualTls,
            new RuntimeHostClientCredentialId(
                CredentialId));
    }

    private static string ValidDocument()
    {
        return "{\"formatVersion\":1,\"enrollments\":["
            + "{\"credentialId\":\""
            + CredentialId
            + "\",\"principalId\":\"remote-client\","
            + "\"trustPolicyId\":\"private-network-trust-v1\"}]}";
    }

    private sealed class EnrollmentDocument
        : IDisposable
    {
        private EnrollmentDocument(
            string directoryPath,
            string filePath)
        {
            DirectoryPath =
                directoryPath;
            FilePath =
                filePath;
        }

        public string DirectoryPath
        {
            get;
        }

        public string FilePath
        {
            get;
        }

        public static async Task<EnrollmentDocument> CreateAsync(
            string contents)
        {
            string directoryPath =
                Path.Combine(
                    Path.GetTempPath(),
                    $"hase-client-enrollments-{Guid.NewGuid():N}");
            Directory.CreateDirectory(
                directoryPath);
            string filePath =
                Path.Combine(
                    directoryPath,
                    "client-enrollments.json");

            await File.WriteAllTextAsync(
                filePath,
                contents,
                Encoding.UTF8);

            return new EnrollmentDocument(
                directoryPath,
                filePath);
        }

        public void Dispose()
        {
            if (Directory.Exists(
                    DirectoryPath))
            {
                Directory.Delete(
                    DirectoryPath,
                    recursive: true);
            }
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
                    $"hase-client-enrollment-missing-{Guid.NewGuid():N}");

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
