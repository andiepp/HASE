using System.Security.Cryptography;
using System.Text.Json;
namespace Hase.Python.CredentialProvisioning.Tests;
public sealed class PythonCachedPropertyAuthorizerTests:IDisposable
{
 readonly string directory=Path.Combine(Path.GetTempPath(),"hase-cached-auth-"+Guid.NewGuid());
 public PythonCachedPropertyAuthorizerTests()=>Directory.CreateDirectory(directory);
 [Fact] public async Task AuthorizeAsync_AppendsCachedReadAndRetainsRollback()
 {
  string policy=Create(); string rollback=Path.Combine(directory,"rollback.json"); byte[] original=File.ReadAllBytes(policy);
  await new PythonCachedPropertyAuthorizer().AuthorizeAsync(new(policy,Hash(original),rollback));
  Assert.Equal(original,File.ReadAllBytes(rollback)); using var doc=JsonDocument.Parse(File.ReadAllBytes(policy));
  Assert.Equal("property.cached.read",doc.RootElement.GetProperty("grants").EnumerateArray().Last().GetProperty("permission").GetString());
 }
 [Fact] public async Task AuthorizeAsync_StaleRevision_MutatesNothing()
 { string policy=Create(); byte[] original=File.ReadAllBytes(policy);
  var error=await Assert.ThrowsAsync<PythonCachedPropertyAuthorizationException>(()=>new PythonCachedPropertyAuthorizer().AuthorizeAsync(new(policy,new string('0',64),Path.Combine(directory,"rollback.json"))));
  Assert.Equal("authorization-input-revision-mismatch",error.Code); Assert.Equal(original,File.ReadAllBytes(policy)); }
 string Create(){string path=Path.Combine(directory,"policy.json"); string[] permissions=["runtime-host.snapshot.read","property.authoritative.read","property.write","command.execute","observation.subscribe"];
  var grants=permissions.Select(x=>new{principalId="hase-python-automation",permission=x}); File.WriteAllText(path,JsonSerializer.Serialize(new{formatVersion=1,grants})); return path;}
 static string Hash(byte[] x)=>Convert.ToHexStringLower(SHA256.HashData(x)); public void Dispose()=>Directory.Delete(directory,true);
}
