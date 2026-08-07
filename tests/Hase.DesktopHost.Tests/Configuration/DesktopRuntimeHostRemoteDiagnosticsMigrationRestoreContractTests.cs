using System.IO;
using System.Runtime.CompilerServices;

namespace Hase.DesktopHost.Tests.Configuration;

public sealed class DesktopRuntimeHostRemoteDiagnosticsMigrationRestoreContractTests
{
    [Fact]
    public void Restore_ShouldHaveNoDeploymentSensitiveParameters()
    {
        string script = ReadRestore();
        int parameterBlockEnd = script.IndexOf(
            ")\n\n$ErrorActionPreference",
            StringComparison.Ordinal);
        Assert.True(parameterBlockEnd > 0);
        string parameterBlock = script[..(parameterBlockEnd + 1)];

        Assert.Contains("param()", parameterBlock);
        Assert.DoesNotContain("Principal", parameterBlock, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Grant", parameterBlock, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Path", parameterBlock, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Restore_ShouldRequireStoppedCompleteMigratedInstallation()
    {
        string script = ReadRestore();

        Assert.Contains("Get-Process -Name \"Hase.DesktopHost.App\"", script);
        Assert.Contains("$authorizationPolicyPath", ExtractRequiredFiles(script));
        Assert.Contains("$originalProfileBackupPath", ExtractRequiredFiles(script));
        Assert.Contains("rollback prerequisites are incomplete", script);
    }

    [Fact]
    public void Restore_ShouldRefuseExistingBackupOrTemporaryArtifacts()
    {
        string script = ReadRestore();

        Assert.Contains("$migratedProfileBackupPath", ExtractProhibitedArtifacts(script));
        Assert.Contains("$migrationTemporaryProfilePath", ExtractProhibitedArtifacts(script));
        Assert.Contains("$rollbackTemporaryProfilePath", ExtractProhibitedArtifacts(script));
    }

    [Fact]
    public void Restore_ShouldValidateMigratedAndOriginalProfileStates()
    {
        string script = ReadRestore();

        Assert.Contains("is not a completed remote-diagnostics migration", script);
        Assert.Contains("authorizationPolicyFilePath", script);
        Assert.Contains("is not a disabled pre-migration profile", script);
        Assert.Contains("Assert-SupportedProfile", script);
        Assert.Contains("-Role \"identity path\"", script);
        Assert.Contains("-Role \"private-network path\"", script);
        Assert.Contains("-Role \"endpoint-composition path\"", script);
    }

    [Fact]
    public void Restore_ShouldValidatePolicyStructureAndCustody()
    {
        string script = ReadRestore();

        Assert.Contains("$policyFile.Length -gt (64 * 1024)", script);
        Assert.Contains("$policyPropertyNames.Count -ne 2", script);
        Assert.Contains("$policyDocument.grants -isnot [System.Array]", script);
        Assert.Contains("$authorizationPolicyHash -ne", script);
    }

    [Fact]
    public void Restore_ShouldValidateCrossProfileCustodyAndReplaceAtomically()
    {
        string script = ReadRestore();

        Assert.Contains("identity custody", script);
        Assert.Contains("private-network custody", script);
        Assert.Contains("endpoint-composition custody", script);
        Assert.Contains("[System.IO.File]::Replace(", script);
        Assert.Contains("$migratedProfileBackupPath", script);
    }

    [Fact]
    public void Restore_ShouldRecoverMigratedProfileAfterVerificationFailure()
    {
        string script = ReadRestore();

        Assert.Contains(
            "$migratedProfileBackupPath,\n            $applicationProfilePath,\n            $originalProfileBackupPath",
            script);
        Assert.Contains("$profileReplaced", script);
        Assert.Contains("throw", script);
    }

    [Fact]
    public void Restore_ShouldRetainInactivePolicyAndSanitizeOutput()
    {
        string script = ReadRestore();

        Assert.Contains("Migrated profile    : backup retained", script);
        Assert.Contains("Authorization policy: retained and inactive", script);
        Assert.Contains("Sensitive values    : withheld", script);
        Assert.DoesNotContain("Remove-Item -LiteralPath $authorizationPolicyPath", script);
        Assert.DoesNotContain("Write-Host $authorizationPolicyPath", script);
    }

    private static string ExtractRequiredFiles(string script) =>
        ExtractArray(script, "foreach ($requiredFile in @(");

    private static string ExtractProhibitedArtifacts(string script) =>
        ExtractArray(script, "foreach ($prohibitedArtifact in @(");

    private static string ExtractArray(string script, string startMarker)
    {
        int start = script.IndexOf(startMarker, StringComparison.Ordinal);
        int end = script.IndexOf(")) {", start, StringComparison.Ordinal);
        Assert.True(start >= 0);
        Assert.True(end > start);
        return script[start..end];
    }

    private static string ReadRestore(
        [CallerFilePath] string testSourceFilePath = "")
    {
        string repositoryRoot = Path.GetFullPath(
            Path.Combine(
                Path.GetDirectoryName(testSourceFilePath)!,
                "..",
                "..",
                ".."));
        return File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "tools",
                "Deployment",
                "Restore-HaseDesktopRuntimeHostRemoteDiagnosticsMigration.ps1"));
    }
}
