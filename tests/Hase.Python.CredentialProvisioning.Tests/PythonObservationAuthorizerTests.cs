using System.Security.Cryptography;
using System.Text.Json;
namespace Hase.Python.CredentialProvisioning.Tests;

public sealed class PythonObservationAuthorizerTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(),
        "hase-python-observation-auth-" + Guid.NewGuid());
    public PythonObservationAuthorizerTests() => Directory.CreateDirectory(directory);

    [Fact]
    public async Task AuthorizeAsync_ExactCommandState_AppendsObservationOnly()
    {
        string policy = CreatePolicy(); string rollback = Path.Combine(directory, "rollback.json");
        byte[] original = await File.ReadAllBytesAsync(policy);
        var result = await new PythonObservationAuthorizer().AuthorizeAsync(
            new(policy, Hash(original), rollback));
        Assert.Equal(original, await File.ReadAllBytesAsync(rollback));
        using JsonDocument document = JsonDocument.Parse(await File.ReadAllBytesAsync(policy));
        string[] permissions = document.RootElement.GetProperty("grants").EnumerateArray()
            .Where(x => x.GetProperty("principalId").GetString() == "hase-python-automation")
            .Select(x => x.GetProperty("permission").GetString()!).ToArray();
        Assert.Equal(["runtime-host.snapshot.read", "property.authoritative.read",
            "property.write", "command.execute", "observation.subscribe"], permissions);
        Assert.Equal(Hash(await File.ReadAllBytesAsync(policy)), result.AuthorizationPolicySha256);
    }

    [Fact]
    public async Task AuthorizeAsync_StaleRevision_MutatesNothing()
    {
        string policy = CreatePolicy(); string rollback = Path.Combine(directory, "rollback.json");
        byte[] original = await File.ReadAllBytesAsync(policy);
        var failure = await Assert.ThrowsAsync<PythonObservationAuthorizationException>(() =>
            new PythonObservationAuthorizer().AuthorizeAsync(new(policy, new string('0', 64), rollback)));
        Assert.Equal("authorization-input-revision-mismatch", failure.Code);
        Assert.Equal(original, await File.ReadAllBytesAsync(policy)); Assert.False(File.Exists(rollback));
    }

    private string CreatePolicy()
    {
        string path = Path.Combine(directory, "authorization.json");
        object[] grants = [
            new { principalId="hase-python-automation", permission="runtime-host.snapshot.read" },
            new { principalId="hase-python-automation", permission="property.authoritative.read" },
            new { principalId="hase-python-automation", permission="property.write" },
            new { principalId="hase-python-automation", permission="command.execute" }];
        File.WriteAllText(path, JsonSerializer.Serialize(new { formatVersion=1, grants })); return path;
    }
    private static string Hash(byte[] value) => Convert.ToHexStringLower(SHA256.HashData(value));
    public void Dispose() => Directory.Delete(directory, true);
}
