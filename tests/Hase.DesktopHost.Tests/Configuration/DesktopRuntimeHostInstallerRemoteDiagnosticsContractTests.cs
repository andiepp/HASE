using System.IO;
using System.Runtime.CompilerServices;

namespace Hase.DesktopHost.Tests.Configuration;

public sealed class DesktopRuntimeHostInstallerRemoteDiagnosticsContractTests
{
    [Fact]
    public void Installer_Defaults_ShouldKeepRemoteDiagnosticsDisabled()
    {
        string script = ReadInstaller();

        Assert.Contains(
            "[switch]$EnableRemoteDiagnostics",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "$applicationProfile.remoteDiagnosticsEnabled = [bool]$EnableRemoteDiagnostics",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_Enablement_ShouldRequireExplicitPolicySource()
    {
        string script = ReadInstaller();

        Assert.Contains(
            "Remote diagnostics require an explicit authorization-policy source.",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "The authorization-policy source path must not be empty.",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_LevelWithoutEnablement_ShouldBeRejected()
    {
        string script = ReadInstaller();

        Assert.Contains(
            "$PSBoundParameters.ContainsKey(\"RemoteDiagnosticsMaximumLevel\")",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "A remote diagnostics maximum level requires explicit remote diagnostics enablement.",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_PolicyCustody_ShouldBeProtectedCopiedAndRolledBack()
    {
        string script = ReadInstaller();

        Assert.Contains(
            "$authorizationPolicyDestinationPath",
            ExtractProtectedTargets(script),
            StringComparison.Ordinal);
        Assert.Contains(
            "-Destination $authorizationPolicyDestinationPath",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "$installedFiles.Add($authorizationPolicyDestinationPath)",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "foreach ($installedFile in $installedFiles)",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_ShouldNotAcceptPrincipalOrGrantParameters()
    {
        string script = ReadInstaller();
        int parameterBlockEnd = script.IndexOf(
            ")\n\n$ErrorActionPreference",
            StringComparison.Ordinal);
        Assert.True(parameterBlockEnd > 0);
        string parameterBlock = script[..parameterBlockEnd];

        Assert.DoesNotContain(
            "PrincipalId",
            parameterBlock,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "Grant",
            parameterBlock,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Installer_Output_ShouldReportOnlyPolicyCustodyState()
    {
        string script = ReadInstaller();

        Assert.Contains(
            "Authorization policy : $(if ($null -eq $authorizationPolicySourcePath) { 'not installed' } else { 'installed' })",
            script,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Write-Host $authorizationPolicySourcePath",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_MediaConfiguration_ShouldRequirePolicyAndRetainCustody()
    {
        string script = ReadInstaller();

        Assert.Contains("[string]$MediaConfigurationPath", script,
            StringComparison.Ordinal);
        Assert.Contains(
            "Runtime Host media requires an explicit authorization-policy source.",
            script,
            StringComparison.Ordinal);
        Assert.Contains("$mediaConfigurationDestinationPath",
            ExtractProtectedTargets(script), StringComparison.Ordinal);
        Assert.Contains("-Destination $mediaConfigurationDestinationPath",
            script, StringComparison.Ordinal);
        Assert.Contains("$installedFiles.Add($mediaConfigurationDestinationPath)",
            script, StringComparison.Ordinal);
        Assert.DoesNotContain("PrincipalId", script[..script.IndexOf(
            ")\n\n$ErrorActionPreference", StringComparison.Ordinal)],
            StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractProtectedTargets(string script)
    {
        int start = script.IndexOf("$protectedTargets = @(", StringComparison.Ordinal);
        int end = script.IndexOf(")", start, StringComparison.Ordinal);
        Assert.True(start >= 0);
        Assert.True(end > start);
        return script[start..end];
    }

    private static string ReadInstaller(
        [CallerFilePath] string testSourceFilePath = "")
    {
        string repositoryRoot = Path.GetFullPath(
            Path.Combine(
                Path.GetDirectoryName(testSourceFilePath)!,
                "..",
                "..",
                ".."));
        // A fresh clone may check the script out with CRLF endings; the
        // content contract must not depend on the checkout line layout.
        return File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    "tools",
                    "Deployment",
                    "Install-HaseDesktopRuntimeHost.ps1"))
            .Replace("\r\n", "\n", StringComparison.Ordinal);
    }
}
