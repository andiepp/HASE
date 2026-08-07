using System.IO;
using System.Runtime.CompilerServices;

namespace Hase.DesktopHost.Tests.Configuration;

public sealed class DesktopRuntimeHostRemoteDiagnosticsAuthorizationPolicySubstitutionContractTests
{
    [Fact]
    public void Substitute_ShouldRequireExternalPolicyAndStoppedHost()
    {
        string script = ReadScript(SubstituteScript);

        Assert.Contains("[string]$AuthorizationPolicyPath", script);
        Assert.Contains("Get-Process -Name \"Hase.DesktopHost.App\"", script);
        Assert.Contains("must be fully qualified", script);
        Assert.DoesNotContain("Read-Host", script);
    }

    [Fact]
    public void Substitute_ShouldPermitOnlyOneRemovedDiagnosticsGrant()
    {
        string script = ReadScript(SubstituteScript);

        Assert.Contains("$removed.Count -ne 1", script);
        Assert.Contains("$added.Count -ne 0", script);
        Assert.Contains("EndsWith(\"diagnostics.subscribe\"", script);
        Assert.Contains("preserve every other grant", script);
    }

    [Fact]
    public void Substitute_ShouldReplaceAtomicallyAndRollbackVerificationFailure()
    {
        string script = ReadScript(SubstituteScript);

        Assert.Contains("[System.IO.File]::Replace(", script);
        Assert.Contains("$authorizedBackupPath", script);
        Assert.Contains("$replaced", script);
        Assert.Contains("custody verification failed", script);
        Assert.DoesNotContain("Write-Host $candidatePath", script);
    }

    [Fact]
    public void Restore_ShouldHaveNoParametersAndRestoreExactAuthorizedBytes()
    {
        string script = ReadScript(RestoreScript);

        Assert.Contains("param()", script);
        Assert.Contains("$authorized.Hash", script);
        Assert.Contains("$deniedBackupPath", script);
        Assert.Contains("Authorized policy : restored exactly", script);
        Assert.DoesNotContain("Write-Host $installedPolicyPath", script);
    }

    [Fact]
    public void BothCommands_ShouldRequireMigratedProfileAndWithholdSensitiveValues()
    {
        foreach (string name in new[] { SubstituteScript, RestoreScript })
        {
            string script = ReadScript(name);
            Assert.Contains(
                "active Runtime Host profile is not a completed remote-diagnostics migration",
                script);
            Assert.Contains("Sensitive values   : withheld", script);
            Assert.Contains("authorizationPolicyFilePath", script);
        }
    }

    private const string SubstituteScript =
        "Substitute-HaseDesktopRuntimeHostRemoteDiagnosticsAuthorizationPolicy.ps1";
    private const string RestoreScript =
        "Restore-HaseDesktopRuntimeHostRemoteDiagnosticsAuthorizationPolicy.ps1";

    private static string ReadScript(
        string name,
        [CallerFilePath] string sourcePath = "")
    {
        string repositoryRoot = Path.GetFullPath(
            Path.Combine(
                Path.GetDirectoryName(sourcePath)!,
                "..",
                "..",
                ".."));
        return File.ReadAllText(
            Path.Combine(repositoryRoot, "tools", "Deployment", name));
    }
}
