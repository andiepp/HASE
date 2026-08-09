using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text.Json;
using Hase.Runtime.Remote.Grpc.Adapter;
namespace Hase.Python.CredentialProvisioning;

public sealed record PythonCachedPropertyAuthorizationRequest(string PolicyPath,
    string ExpectedSha256, string RollbackPath);
public sealed record PythonCachedPropertyAuthorizationResult(string TransactionId,
    string PolicySha256, string RollbackPath);
public sealed class PythonCachedPropertyAuthorizationException(string code)
    : Exception($"Python cached-read authorization failed: {code}.")
{ public string Code { get; } = code; }

public sealed class PythonCachedPropertyAuthorizer
{
    private static readonly string[] Existing = ["runtime-host.snapshot.read",
        "property.authoritative.read", "property.write", "command.execute",
        "observation.subscribe"];
    public async Task<PythonCachedPropertyAuthorizationResult> AuthorizeAsync(
        PythonCachedPropertyAuthorizationRequest request,
        CancellationToken token = default)
    {
        if (request is null || !Path.IsPathFullyQualified(request.PolicyPath)
            || !Path.IsPathFullyQualified(request.RollbackPath)
            || request.ExpectedSha256?.Length != 64) throw Fail("authorization-request-invalid");
        string policy=Path.GetFullPath(request.PolicyPath), rollback=Path.GetFullPath(request.RollbackPath);
        if (!File.Exists(policy) || File.Exists(rollback) || Directory.Exists(rollback)
            || Path.GetDirectoryName(rollback) is not string parent || !Directory.Exists(parent))
            throw Fail("authorization-target-invalid");
        byte[] original=await File.ReadAllBytesAsync(policy,token); string id=Guid.NewGuid().ToString("N");
        string stage=policy+".stage-"+id; bool published=false;
        try {
            if (Hash(original)!=request.ExpectedSha256) throw Fail("authorization-input-revision-mismatch");
            string security=Security(policy); byte[] candidate=Candidate(original);
            await File.WriteAllBytesAsync(stage,candidate,token);
            if (HashFile(policy)!=request.ExpectedSha256) throw Fail("authorization-input-revision-mismatch");
            File.Replace(stage,policy,rollback,false); published=true; string hash=Hash(candidate);
            if (HashFile(policy)!=hash || HashFile(rollback)!=request.ExpectedSha256
                || Security(policy)!=security || Security(rollback)!=security)
                throw Fail("authorization-publication-invalid");
            return new(id,hash,rollback);
        } catch (PythonCachedPropertyAuthorizationException) {
            if (published) { File.Delete(policy); File.Move(rollback,policy); }
            throw;
        } finally { CryptographicOperations.ZeroMemory(original); try { File.Delete(stage); } catch {} }
    }
    private static byte[] Candidate(byte[] original)
    {
        _=RuntimeHostAuthorizationPolicyFile.Load(original); using JsonDocument doc=JsonDocument.Parse(original);
        JsonElement[] grants=doc.RootElement.GetProperty("grants").EnumerateArray().ToArray();
        string[] permissions=grants.Where(x=>x.GetProperty("principalId").GetString()=="hase-python-automation")
            .Select(x=>x.GetProperty("permission").GetString()!).ToArray();
        if (!permissions.SequenceEqual(Existing)) throw Fail("python-principal-state-invalid");
        using var stream=new MemoryStream(); using(var writer=new Utf8JsonWriter(stream,new(){Indented=true})) {
            writer.WriteStartObject(); writer.WriteNumber("formatVersion",1); writer.WriteStartArray("grants");
            foreach(var grant in grants) grant.WriteTo(writer); writer.WriteStartObject();
            writer.WriteString("principalId","hase-python-automation"); writer.WriteString("permission","property.cached.read");
            writer.WriteEndObject(); writer.WriteEndArray(); writer.WriteEndObject(); }
        byte[] result=stream.ToArray(); _=RuntimeHostAuthorizationPolicyFile.Load(result); return result;
    }
    private static string Security(string path) { if(!OperatingSystem.IsWindows()) return Convert.ToInt32(File.GetUnixFileMode(path)).ToString();
        string v=new FileInfo(path).GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.Access); int i=v.IndexOf('(');
        if(!v.StartsWith("D:")||i<0)return v; return "D:"+v[2..i].Replace("AI","").Replace("AR","")+v[i..]; }
    private static string Hash(byte[] x)=>Convert.ToHexStringLower(SHA256.HashData(x));
    private static string HashFile(string p)=>Hash(File.ReadAllBytes(p));
    private static PythonCachedPropertyAuthorizationException Fail(string c)=>new(c);
}
