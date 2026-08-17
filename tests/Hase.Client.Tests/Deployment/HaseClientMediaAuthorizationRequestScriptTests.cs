using System.IO;
using System.Runtime.CompilerServices;

namespace Hase.Client.Tests.Deployment;

public sealed class HaseClientMediaAuthorizationRequestScriptTests
{
    [Fact]
    public void Request_ShouldContainOnlyHostAndCredentialIdentity()
    {
        string script = ReadScript();

        Assert.Contains("expectedRuntimeHostId =", script);
        Assert.Contains("credentialId =", script);
        Assert.DoesNotContain("address =", script,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("thumbprint =", script,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("principalId =", script,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Request_ShouldRequireEnabledProfilesAndProtectedCustody()
    {
        string script = ReadScript();

        Assert.Contains("[bool]$profile.enabled", script);
        Assert.Contains("SetAccessRuleProtection($true, $false)", script);
        Assert.Contains("CurrentUser", script);
        Assert.Contains("SYSTEM", script);
        Assert.Contains("Output SHA-256", script);
    }

    [Fact]
    public void Request_ShouldMaterializeGenericProfileListExplicitly()
    {
        string script = ReadScript();

        Assert.Contains("profiles = $requestProfiles.ToArray()", script);
        Assert.DoesNotContain("profiles = @($requestProfiles)", script);
    }

    private static string ReadScript(
        [CallerFilePath] string testSourceFilePath = "")
    {
        string repositoryRoot = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(testSourceFilePath)!,
            "..",
            "..",
            ".."));
        return File.ReadAllText(Path.Combine(
            repositoryRoot,
            "tools",
            "Deployment",
            "New-HaseClientRuntimeHostMediaAuthorizationRequest.ps1"));
    }
}
