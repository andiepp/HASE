using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hase.Python.CredentialProvisioning;

namespace Hase.Python.CredentialProvisioning.Tests;

public sealed class PythonLaptopMiniPcObservationAuthorizerTests : IDisposable
{
    private readonly string directory = Path.Combine(
        Path.GetTempPath(), "hase-laptop-minipc-observation-" + Guid.NewGuid().ToString("N"));

    public PythonLaptopMiniPcObservationAuthorizerTests() => Directory.CreateDirectory(directory);

    [Fact]
    public async Task AuthorizeAsync_AddsOnlyObservationToLaptopPrincipal()
    {
        string policy=CreatePolicy();
        string originalHash=HashFile(policy);
        string rollback=Path.Combine(directory,"rollback.json");

        PythonObservationAuthorizationResult result =
            await new PythonLaptopMiniPcObservationAuthorizer().AuthorizeAsync(
                new(policy,originalHash,rollback));

        Assert.Equal(rollback,result.RollbackPath);
        Assert.Equal(originalHash,HashFile(rollback));

        using JsonDocument document=JsonDocument.Parse(File.ReadAllBytes(policy));
        var grants=document.RootElement.GetProperty("grants").EnumerateArray()
            .Select(x=>(x.GetProperty("principalId").GetString()!,
                x.GetProperty("permission").GetString()!)).ToArray();

        Assert.Contains(("hase-laptop-python-minipc","observation.subscribe"),grants);
        Assert.Equal(3,grants.Count(x=>x.Item1=="hase-laptop-python-minipc"));
        Assert.DoesNotContain(("hase-laptop-python-minipc","property.write"),grants);
        Assert.DoesNotContain(("hase-laptop-python-minipc","diagnostics.subscribe"),grants);
    }

    [Fact]
    public async Task AuthorizeAsync_PreservesOtherPrincipalGrants()
    {
        string policy=CreatePolicy();
        string rollback=Path.Combine(directory,"rollback.json");
        await new PythonLaptopMiniPcObservationAuthorizer().AuthorizeAsync(
            new(policy,HashFile(policy),rollback));

        using JsonDocument document=JsonDocument.Parse(File.ReadAllBytes(policy));
        string[] permissions=document.RootElement.GetProperty("grants").EnumerateArray()
            .Where(x=>x.GetProperty("principalId").GetString()=="hase-python-automation")
            .Select(x=>x.GetProperty("permission").GetString()!).ToArray();

        Assert.Equal(new[]{"runtime-host.snapshot.read","property.authoritative.read"},permissions);
    }

    [Fact]
    public async Task AuthorizeAsync_RejectsAlreadyAuthorizedLaptopPrincipal()
    {
        string policy=CreatePolicy(new[]{
            "runtime-host.snapshot.read","property.authoritative.read","observation.subscribe"});
        var failure=await Assert.ThrowsAsync<PythonObservationAuthorizationException>(() =>
            new PythonLaptopMiniPcObservationAuthorizer().AuthorizeAsync(
                new(policy,HashFile(policy),Path.Combine(directory,"rollback.json"))));
        Assert.Equal("observation-already-authorized",failure.Code);
    }

    [Fact]
    public async Task AuthorizeAsync_RejectsUnexpectedLaptopPrincipalState()
    {
        string policy=CreatePolicy(new[]{
            "runtime-host.snapshot.read","property.authoritative.read","property.write"});
        var failure=await Assert.ThrowsAsync<PythonObservationAuthorizationException>(() =>
            new PythonLaptopMiniPcObservationAuthorizer().AuthorizeAsync(
                new(policy,HashFile(policy),Path.Combine(directory,"rollback.json"))));
        Assert.Equal("laptop-minipc-principal-state-invalid",failure.Code);
    }

    [Fact]
    public async Task AuthorizeAsync_RejectsRevisionMismatchWithoutRollback()
    {
        string policy=CreatePolicy();
        string rollback=Path.Combine(directory,"rollback.json");
        var failure=await Assert.ThrowsAsync<PythonObservationAuthorizationException>(() =>
            new PythonLaptopMiniPcObservationAuthorizer().AuthorizeAsync(
                new(policy,new string('0',64),rollback)));
        Assert.Equal("authorization-input-revision-mismatch",failure.Code);
        Assert.False(File.Exists(rollback));
    }

    [Fact]
    public async Task AuthorizeAsync_RejectsExistingRollbackPath()
    {
        string policy=CreatePolicy();
        string rollback=Path.Combine(directory,"rollback.json");
        File.WriteAllText(rollback,"occupied");
        var failure=await Assert.ThrowsAsync<PythonObservationAuthorizationException>(() =>
            new PythonLaptopMiniPcObservationAuthorizer().AuthorizeAsync(
                new(policy,HashFile(policy),rollback)));
        Assert.Equal("authorization-target-invalid",failure.Code);
    }

    private string CreatePolicy(string[]? laptopPermissions=null)
    {
        laptopPermissions??=new[]{"runtime-host.snapshot.read","property.authoritative.read"};
        var grants=new List<object>{
            new{principalId="laptop-validation-client",permission="runtime-host.snapshot.read"},
            new{principalId="laptop-validation-client",permission="observation.subscribe"},
            new{principalId="hase-python-automation",permission="runtime-host.snapshot.read"},
            new{principalId="hase-python-automation",permission="property.authoritative.read"}};
        grants.AddRange(laptopPermissions.Select(permission=>
            new{principalId="hase-laptop-python-minipc",permission}));
        string path=Path.Combine(directory,"policy.json");
        File.WriteAllText(path,JsonSerializer.Serialize(new{formatVersion=1,grants},
            new JsonSerializerOptions{WriteIndented=true}),new UTF8Encoding(false));
        return path;
    }

    private static string HashFile(string path)=>
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    public void Dispose()
    {
        try{Directory.Delete(directory,true);}catch{}
    }
}
