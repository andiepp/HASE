using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hase.Python.CredentialProvisioning;

namespace Hase.Python.CredentialProvisioning.Tests;

public sealed class PythonLaptopMiniPcCommandExecutionAuthorizerTests
    : IDisposable
{
    private readonly string directory = Path.Combine(
        Path.GetTempPath(),
        "hase-laptop-minipc-command-" + Guid.NewGuid().ToString("N"));

    public PythonLaptopMiniPcCommandExecutionAuthorizerTests() =>
        Directory.CreateDirectory(directory);

    [Fact]
    public async Task AuthorizeAsync_AddsOnlyCommandExecute()
    {
        string policy = CreatePolicy();
        string originalHash = HashFile(policy);
        string rollback = Path.Combine(directory, "rollback.json");

        var result =
            await new PythonLaptopMiniPcCommandExecutionAuthorizer().AuthorizeAsync(
                new(policy, originalHash, rollback));

        Assert.Equal(rollback, result.RollbackPath);
        Assert.Equal(originalHash, HashFile(rollback));
        Assert.Equal(result.AuthorizationPolicySha256, HashFile(policy));
        Assert.Equal(
            new[]
            {
                "runtime-host.snapshot.read",
                "property.authoritative.read",
                "observation.subscribe",
                "property.write",
                "command.execute",
            },
            LaptopPermissions(policy));
    }

    [Fact]
    public async Task AuthorizeAsync_PreservesOtherPrincipals()
    {
        string policy = CreatePolicy();
        string rollback = Path.Combine(directory, "rollback.json");

        await new PythonLaptopMiniPcCommandExecutionAuthorizer().AuthorizeAsync(
            new(policy, HashFile(policy), rollback));

        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(policy));
        string[] permissions = document.RootElement.GetProperty("grants")
            .EnumerateArray()
            .Where(item =>
                item.GetProperty("principalId").GetString()
                    == "hase-python-automation")
            .Select(item => item.GetProperty("permission").GetString()!)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "runtime-host.snapshot.read",
                "property.authoritative.read",
            },
            permissions);
    }

    [Fact]
    public async Task AuthorizeAsync_RejectsAlreadyAuthorized()
    {
        await AssertFailure(
            new[]
            {
                "runtime-host.snapshot.read",
                "property.authoritative.read",
                "observation.subscribe",
                "property.write",
                "command.execute",
            },
            "command-execute-already-authorized");
    }

    [Theory]
    [MemberData(nameof(InvalidStates))]
    public async Task AuthorizeAsync_RejectsUnexpectedPrincipalState(
        string[] permissions)
    {
        await AssertFailure(
            permissions,
            "laptop-minipc-principal-state-invalid");
    }

    public static IEnumerable<object[]> InvalidStates()
    {
        yield return
        [
            new[]
            {
                "runtime-host.snapshot.read",
                "property.authoritative.read",
                "observation.subscribe",
            },
        ];
        yield return
        [
            new[]
            {
                "runtime-host.snapshot.read",
                "property.authoritative.read",
                "observation.subscribe",
                "property.write",
                "property.cached.read",
            },
        ];
        yield return
        [
            new[]
            {
                "runtime-host.snapshot.read",
                "property.authoritative.read",
                "observation.subscribe",
                "property.write",
                "diagnostics.subscribe",
            },
        ];
        yield return
        [
            new[]
            {
                "property.authoritative.read",
                "runtime-host.snapshot.read",
                "observation.subscribe",
                "property.write",
            },
        ];
    }

    [Fact]
    public async Task AuthorizeAsync_RejectsRevisionMismatchWithoutRollback()
    {
        string policy = CreatePolicy();
        string rollback = Path.Combine(directory, "rollback.json");

        var failure = await Assert.ThrowsAsync<
            PythonLaptopMiniPcCommandExecutionAuthorizationException>(
                () => new PythonLaptopMiniPcCommandExecutionAuthorizer()
                    .AuthorizeAsync(
                        new(policy, new string('0', 64), rollback)));

        Assert.Equal("authorization-input-revision-mismatch", failure.Code);
        Assert.False(File.Exists(rollback));
    }

    [Fact]
    public async Task AuthorizeAsync_RejectsExistingRollback()
    {
        string policy = CreatePolicy();
        string rollback = Path.Combine(directory, "rollback.json");
        File.WriteAllText(rollback, "occupied");

        var failure = await Assert.ThrowsAsync<
            PythonLaptopMiniPcCommandExecutionAuthorizationException>(
                () => new PythonLaptopMiniPcCommandExecutionAuthorizer()
                    .AuthorizeAsync(new(policy, HashFile(policy), rollback)));

        Assert.Equal("authorization-target-invalid", failure.Code);
    }

    [Fact]
    public async Task AuthorizeAsync_RollbackIsExactOriginalBytes()
    {
        string policy = CreatePolicy();
        byte[] original = File.ReadAllBytes(policy);
        string rollback = Path.Combine(directory, "rollback.json");

        await new PythonLaptopMiniPcCommandExecutionAuthorizer().AuthorizeAsync(
            new(policy, HashFile(policy), rollback));

        Assert.Equal(original, File.ReadAllBytes(rollback));
    }

    private async Task AssertFailure(
        string[] permissions,
        string expectedCode)
    {
        string policy = CreatePolicy(permissions);
        string rollback = Path.Combine(directory, "rollback.json");

        var failure = await Assert.ThrowsAsync<
            PythonLaptopMiniPcCommandExecutionAuthorizationException>(
                () => new PythonLaptopMiniPcCommandExecutionAuthorizer()
                    .AuthorizeAsync(new(policy, HashFile(policy), rollback)));

        Assert.Equal(expectedCode, failure.Code);
        Assert.False(File.Exists(rollback));
    }

    private string CreatePolicy(string[]? laptopPermissions = null)
    {
        laptopPermissions ??=
        [
            "runtime-host.snapshot.read",
            "property.authoritative.read",
            "observation.subscribe",
            "property.write",
        ];

        var grants = new List<object>
        {
            new
            {
                principalId = "laptop-validation-client",
                permission = "runtime-host.snapshot.read",
            },
            new
            {
                principalId = "hase-python-automation",
                permission = "runtime-host.snapshot.read",
            },
            new
            {
                principalId = "hase-python-automation",
                permission = "property.authoritative.read",
            },
        };
        grants.AddRange(laptopPermissions.Select(permission => new
        {
            principalId = "hase-laptop-python-minipc",
            permission,
        }));

        string path = Path.Combine(directory, "policy.json");
        File.WriteAllText(
            path,
            JsonSerializer.Serialize(
                new { formatVersion = 1, grants },
                new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(false));
        return path;
    }

    private static string[] LaptopPermissions(string path)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
        return document.RootElement.GetProperty("grants")
            .EnumerateArray()
            .Where(item =>
                item.GetProperty("principalId").GetString()
                    == "hase-laptop-python-minipc")
            .Select(item => item.GetProperty("permission").GetString()!)
            .ToArray();
    }

    private static string HashFile(string path) =>
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    public void Dispose()
    {
        try
        {
            Directory.Delete(directory, true);
        }
        catch
        {
        }
    }
}
