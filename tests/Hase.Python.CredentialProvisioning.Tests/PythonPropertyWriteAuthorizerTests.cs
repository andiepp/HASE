using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text.Json;

namespace Hase.Python.CredentialProvisioning.Tests;

public sealed class PythonPropertyWriteAuthorizerTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(),
        "hase-python-write-authorization-" + Guid.NewGuid());

    public PythonPropertyWriteAuthorizerTests() => Directory.CreateDirectory(directory);

    [Fact]
    public async Task AuthorizeAsync_ReadOnlyLegacyState_PublishesBothAndRetainsRollbacks()
    {
        TestFiles files = Create();
        byte[] oldPolicy = await File.ReadAllBytesAsync(files.Policy);
        byte[] oldProfile = await File.ReadAllBytesAsync(files.Profile);
        string policySecurity = Security(files.Policy);
        string profileSecurity = Security(files.Profile);

        PythonPropertyWriteAuthorizationResult result =
            await new PythonPropertyWriteAuthorizer().AuthorizeAsync(
                Request(files, oldPolicy, oldProfile));

        Assert.Equal(oldPolicy, await File.ReadAllBytesAsync(files.PolicyBackup));
        Assert.Equal(oldProfile, await File.ReadAllBytesAsync(files.ProfileBackup));
        Assert.Equal(policySecurity, Security(files.Policy));
        Assert.Equal(policySecurity, Security(files.PolicyBackup));
        Assert.Equal(profileSecurity, Security(files.Profile));
        Assert.Equal(profileSecurity, Security(files.ProfileBackup));
        using JsonDocument policy = JsonDocument.Parse(
            await File.ReadAllBytesAsync(files.Policy));
        Assert.Equal("property.write", policy.RootElement.GetProperty("grants")
            .EnumerateArray().Last().GetProperty("permission").GetString());
        using JsonDocument profile = JsonDocument.Parse(
            await File.ReadAllBytesAsync(files.Profile));
        Assert.Equal(files.Policy, profile.RootElement
            .GetProperty("authorizationPolicyFilePath").GetString());
        Assert.Equal(Hash(await File.ReadAllBytesAsync(files.Policy)),
            result.AuthorizationPolicySha256);
        Assert.Equal(Hash(await File.ReadAllBytesAsync(files.Profile)),
            result.ApplicationProfileSha256);
    }

    [Fact]
    public async Task AuthorizeAsync_Utf8BomProfile_PublishesAndRetainsExactRollback()
    {
        TestFiles files = Create();
        byte[] policy = await File.ReadAllBytesAsync(files.Policy);
        byte[] profile = await File.ReadAllBytesAsync(files.Profile);
        byte[] bomProfile = [0xef, 0xbb, 0xbf, .. profile];
        await File.WriteAllBytesAsync(files.Profile, bomProfile);

        await new PythonPropertyWriteAuthorizer().AuthorizeAsync(
            Request(files, policy, bomProfile));

        Assert.Equal(bomProfile,
            await File.ReadAllBytesAsync(files.ProfileBackup));
        using JsonDocument published = JsonDocument.Parse(
            await File.ReadAllBytesAsync(files.Profile));
        Assert.Equal(files.Policy, published.RootElement
            .GetProperty("authorizationPolicyFilePath").GetString());
    }

    [Fact]
    public async Task AuthorizeAsync_StaleProfileRevision_MutatesNeitherFile()
    {
        TestFiles files = Create();
        byte[] policy = await File.ReadAllBytesAsync(files.Policy);
        byte[] profile = await File.ReadAllBytesAsync(files.Profile);
        var request = Request(files, policy, profile) with
        { ExpectedApplicationProfileSha256 = new string('0', 64) };

        var failure = await Assert.ThrowsAsync<
            PythonPropertyWriteAuthorizationException>(() =>
                new PythonPropertyWriteAuthorizer().AuthorizeAsync(request));

        Assert.Equal("authorization-input-revision-mismatch", failure.Code);
        Assert.Equal(policy, await File.ReadAllBytesAsync(files.Policy));
        Assert.Equal(profile, await File.ReadAllBytesAsync(files.Profile));
        Assert.False(File.Exists(files.PolicyBackup));
        Assert.False(File.Exists(files.ProfileBackup));
    }

    [Fact]
    public async Task AuthorizeAsync_ActivePolicyProfile_Rejects()
    {
        TestFiles files = Create(profileAlreadyActive: true);
        byte[] policy = await File.ReadAllBytesAsync(files.Policy);
        byte[] profile = await File.ReadAllBytesAsync(files.Profile);

        var failure = await Assert.ThrowsAsync<
            PythonPropertyWriteAuthorizationException>(() =>
                new PythonPropertyWriteAuthorizer().AuthorizeAsync(
                    Request(files, policy, profile)));

        Assert.Equal("application-profile-state-invalid", failure.Code);
        Assert.Equal(policy, await File.ReadAllBytesAsync(files.Policy));
        Assert.Equal(profile, await File.ReadAllBytesAsync(files.Profile));
    }

    [Theory]
    [InlineData("property.write", "property-write-already-authorized")]
    [InlineData("command.execute", "python-principal-state-invalid")]
    public async Task AuthorizeAsync_UnexpectedPythonGrant_Rejects(
        string grant, string code)
    {
        TestFiles files = Create(extraGrant: grant);
        byte[] policy = await File.ReadAllBytesAsync(files.Policy);
        byte[] profile = await File.ReadAllBytesAsync(files.Profile);

        var failure = await Assert.ThrowsAsync<
            PythonPropertyWriteAuthorizationException>(() =>
                new PythonPropertyWriteAuthorizer().AuthorizeAsync(
                    Request(files, policy, profile)));

        Assert.Equal(code, failure.Code);
        Assert.Equal(profile, await File.ReadAllBytesAsync(files.Profile));
    }

    [Fact]
    public async Task AuthorizeAsync_ExistingRollback_RejectsBeforeMutation()
    {
        TestFiles files = Create();
        await File.WriteAllTextAsync(files.PolicyBackup, "retained");
        byte[] policy = await File.ReadAllBytesAsync(files.Policy);
        byte[] profile = await File.ReadAllBytesAsync(files.Profile);

        var failure = await Assert.ThrowsAsync<
            PythonPropertyWriteAuthorizationException>(() =>
                new PythonPropertyWriteAuthorizer().AuthorizeAsync(
                    Request(files, policy, profile)));

        Assert.Equal("authorization-target-invalid", failure.Code);
        Assert.Equal("retained", await File.ReadAllTextAsync(files.PolicyBackup));
    }

    private TestFiles Create(string? extraGrant = null,
        bool profileAlreadyActive = false)
    {
        string policy = Path.Combine(directory, "authorization.json");
        var grants = new List<object>
        {
            new { principalId = "hase-python-automation",
                permission = "runtime-host.snapshot.read" },
            new { principalId = "hase-python-automation",
                permission = "property.authoritative.read" },
        };
        if (extraGrant is not null) grants.Add(new
        { principalId = "hase-python-automation", permission = extraGrant });
        File.WriteAllText(policy, JsonSerializer.Serialize(new
        { formatVersion = 1, grants }, Options));
        string profile = Path.Combine(directory, "desktop-runtime-host.json");
        var document = new Dictionary<string, object?>
        {
            ["formatVersion"] = 1,
            ["identityFilePath"] = Path.Combine(directory, "identity.json"),
            ["privateNetworkConfigurationFilePath"] = Path.Combine(directory, "network.json"),
            ["endpointCompositionFilePath"] = Path.Combine(directory, "endpoints.json"),
            ["maximumDiagnosticLevel"] = "Bytes",
            ["includeByteBufferSimulation"] = false,
        };
        if (profileAlreadyActive) document["authorizationPolicyFilePath"] = policy;
        File.WriteAllText(profile, JsonSerializer.Serialize(document, Options));
        return new TestFiles(policy, profile,
            Path.Combine(directory, "policy.rollback.json"),
            Path.Combine(directory, "profile.rollback.json"));
    }

    private static PythonPropertyWriteAuthorizationRequest Request(
        TestFiles value, byte[] policy, byte[] profile) => new(
            value.Policy, Hash(policy), value.Profile, Hash(profile),
            value.PolicyBackup, value.ProfileBackup);
    private static string Hash(byte[] value) =>
        Convert.ToHexStringLower(SHA256.HashData(value));
    private static string Security(string path)
    {
        if (!OperatingSystem.IsWindows())
            return Convert.ToInt32(File.GetUnixFileMode(path)).ToString();
        string value = new FileInfo(path).GetAccessControl()
            .GetSecurityDescriptorSddlForm(AccessControlSections.Access);
        int firstAce = value.IndexOf('(');
        if (!value.StartsWith("D:", StringComparison.Ordinal) || firstAce < 0)
            return value;
        string flags = value[2..firstAce].Replace("AI", string.Empty,
            StringComparison.Ordinal).Replace("AR", string.Empty,
            StringComparison.Ordinal);
        return "D:" + flags + value[firstAce..];
    }
    private static readonly JsonSerializerOptions Options = new()
    { WriteIndented = true };
    public void Dispose() => Directory.Delete(directory, true);
    private sealed record TestFiles(string Policy, string Profile,
        string PolicyBackup, string ProfileBackup);
}
