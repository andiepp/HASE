using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hase.Python.CredentialProvisioning;

namespace Hase.Python.CredentialProvisioning.Tests;

public sealed class PythonLaptopMiniPcPropertyWriteAuthorizerTests
    : IDisposable
{
    private readonly string directory = Path.Combine(
        Path.GetTempPath(),
        "hase-laptop-minipc-property-write-" + Guid.NewGuid().ToString("N"));

    public PythonLaptopMiniPcPropertyWriteAuthorizerTests() =>
        Directory.CreateDirectory(directory);

    [Fact]
    public async Task AuthorizeAsync_AddsOnlyPropertyWriteToLaptopPrincipal()
    {
        string policy = CreatePolicy();
        string originalHash = HashFile(policy);
        string rollback = Path.Combine(directory, "rollback.json");

        PythonLaptopMiniPcPropertyWriteAuthorizationResult result =
            await new PythonLaptopMiniPcPropertyWriteAuthorizer().AuthorizeAsync(
                new(policy, originalHash, rollback));

        Assert.Equal(rollback, result.RollbackPath);
        Assert.Equal(originalHash, HashFile(rollback));
        Assert.Equal(result.AuthorizationPolicySha256, HashFile(policy));

        string[] permissions = LaptopPermissions(policy);
        Assert.Equal(
            new[]
            {
                "runtime-host.snapshot.read",
                "property.authoritative.read",
                "observation.subscribe",
                "property.write",
            },
            permissions);
    }

    [Fact]
    public async Task AuthorizeAsync_PreservesOtherPrincipalGrants()
    {
        string policy = CreatePolicy();
        string rollback = Path.Combine(directory, "rollback.json");

        await new PythonLaptopMiniPcPropertyWriteAuthorizer().AuthorizeAsync(
            new(policy, HashFile(policy), rollback));

        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllBytes(policy));
        string[] permissions = document.RootElement.GetProperty("grants")
            .EnumerateArray()
            .Where(item =>
                item.GetProperty("principalId").GetString()
                    == "hase-python-automation")
            .Select(item =>
                item.GetProperty("permission").GetString()!)
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
    public async Task AuthorizeAsync_RejectsAlreadyAuthorizedLaptopPrincipal()
    {
        await AssertFailure(
            new[]
            {
                "runtime-host.snapshot.read",
                "property.authoritative.read",
                "observation.subscribe",
                "property.write",
            },
            "property-write-already-authorized");
    }

    [Theory]
    [MemberData(nameof(InvalidStates))]
    public async Task AuthorizeAsync_RejectsUnexpectedLaptopPrincipalState(
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
            },
        ];
        yield return
        [
            new[]
            {
                "runtime-host.snapshot.read",
                "property.authoritative.read",
                "observation.subscribe",
                "command.execute",
            },
        ];
        yield return
        [
            new[]
            {
                "runtime-host.snapshot.read",
                "property.authoritative.read",
                "observation.subscribe",
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
            },
        ];
    }

    [Fact]
    public async Task AuthorizeAsync_RejectsRevisionMismatchWithoutRollback()
    {
        string policy = CreatePolicy();
        string rollback = Path.Combine(directory, "rollback.json");

        var failure = await Assert.ThrowsAsync<
            PythonLaptopMiniPcPropertyWriteAuthorizationException>(
                () => new PythonLaptopMiniPcPropertyWriteAuthorizer()
                    .AuthorizeAsync(
                        new(policy, new string('0', 64), rollback)));

        Assert.Equal(
            "authorization-input-revision-mismatch",
            failure.Code);
        Assert.False(File.Exists(rollback));
        Assert.Equal(
            new[]
            {
                "runtime-host.snapshot.read",
                "property.authoritative.read",
                "observation.subscribe",
            },
            LaptopPermissions(policy));
    }

    [Fact]
    public async Task AuthorizeAsync_RejectsExistingRollbackPath()
    {
        string policy = CreatePolicy();
        string rollback = Path.Combine(directory, "rollback.json");
        File.WriteAllText(rollback, "occupied");

        var failure = await Assert.ThrowsAsync<
            PythonLaptopMiniPcPropertyWriteAuthorizationException>(
                () => new PythonLaptopMiniPcPropertyWriteAuthorizer()
                    .AuthorizeAsync(
                        new(policy, HashFile(policy), rollback)));

        Assert.Equal("authorization-target-invalid", failure.Code);
    }

    [Fact]
    public async Task AuthorizeAsync_RollbackIsExactOriginalBytes()
    {
        string policy = CreatePolicy();
        byte[] original = File.ReadAllBytes(policy);
        string rollback = Path.Combine(directory, "rollback.json");

        await new PythonLaptopMiniPcPropertyWriteAuthorizer().AuthorizeAsync(
            new(policy, HashFile(policy), rollback));

        Assert.Equal(original, File.ReadAllBytes(rollback));
    }

    private async Task AssertFailure(
        string[] laptopPermissions,
        string expectedCode)
    {
        string policy = CreatePolicy(laptopPermissions);
        string rollback = Path.Combine(directory, "rollback.json");

        var failure = await Assert.ThrowsAsync<
            PythonLaptopMiniPcPropertyWriteAuthorizationException>(
                () => new PythonLaptopMiniPcPropertyWriteAuthorizer()
                    .AuthorizeAsync(
                        new(policy, HashFile(policy), rollback)));

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
                principalId = "laptop-validation-client",
                permission = "observation.subscribe",
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
        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllBytes(path));
        return document.RootElement.GetProperty("grants")
            .EnumerateArray()
            .Where(item =>
                item.GetProperty("principalId").GetString()
                    == "hase-laptop-python-minipc")
            .Select(item =>
                item.GetProperty("permission").GetString()!)
            .ToArray();
    }

    private static string HashFile(string path) =>
        Convert.ToHexStringLower(
            SHA256.HashData(File.ReadAllBytes(path)));

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
