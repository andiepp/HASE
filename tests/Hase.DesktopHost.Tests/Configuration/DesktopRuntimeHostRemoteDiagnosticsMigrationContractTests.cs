using System.IO;
using System.Runtime.CompilerServices;

namespace Hase.DesktopHost.Tests.Configuration;

public sealed class DesktopRuntimeHostRemoteDiagnosticsMigrationContractTests
{
    [Fact]
    public void Migration_ShouldRequirePolicyAndLevelWithoutGrantParameters()
    {
        string script = ReadMigration();
        string parameterBlock = script[..script.IndexOf(
            ")\n\n$ErrorActionPreference",
            StringComparison.Ordinal)];

        Assert.Contains("[string]$AuthorizationPolicyPath", parameterBlock);
        Assert.Contains("[string]$RemoteDiagnosticsMaximumLevel", parameterBlock);
        Assert.DoesNotContain("PrincipalId", parameterBlock, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Grant", parameterBlock, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Migration_ShouldRequireStoppedCompleteGuidedInstallation()
    {
        string script = ReadMigration();

        Assert.Contains("Get-Process -Name \"Hase.DesktopHost.App\"", script);
        Assert.Contains("$requiredFiles = @(", script);
        Assert.Contains("The guided Runtime Host installation is incomplete.", script);
    }

    [Fact]
    public void Migration_ShouldRefuseExistingOrIncompleteMigrationState()
    {
        string script = ReadMigration();

        Assert.Contains("$authorizationPolicyDestinationPath", script);
        Assert.Contains("$profileBackupPath", script);
        Assert.Contains("$temporaryProfilePath", script);
        Assert.Contains("has already been migrated", script);
        Assert.Contains("already enables remote diagnostics", script);
    }

    [Fact]
    public void Migration_ShouldValidateBoundedPolicyStructure()
    {
        string script = ReadMigration();

        Assert.Contains("$policyFile.Length -gt (64 * 1024)", script);
        Assert.Contains("$policyPropertyNames.Count -ne 2", script);
        Assert.Contains("$policyDocument.grants -isnot [System.Array]", script);
        Assert.Contains("$authorizationPolicyDestinationHash -ne", script);
        Assert.Contains("$authorizationPolicySourceHash", script);
    }

    [Fact]
    public void Migration_ShouldUseSameDirectoryTemporaryProfileAndAtomicReplacement()
    {
        string script = ReadMigration();

        Assert.Contains("$temporaryProfilePath = $applicationProfilePath + \".49m-tmp\"", script);
        Assert.Contains("$profileBackupPath = $applicationProfilePath + \".49m-backup\"", script);
        Assert.Contains("[System.IO.File]::Replace(", script);
    }

    [Fact]
    public void Migration_ShouldRestoreProfileAndRemoveCopiedArtifactsOnFailure()
    {
        string script = ReadMigration();

        Assert.Contains("$profileBackupPath,\n            $applicationProfilePath", script);
        Assert.Contains("Remove-Item -LiteralPath $authorizationPolicyDestinationPath -Force", script);
        Assert.Contains("Remove-Item -LiteralPath $temporaryProfilePath -Force", script);
    }

    [Fact]
    public void Migration_ShouldRetainBackupAndSanitizeSuccessOutput()
    {
        string script = ReadMigration();

        Assert.Contains("Original profile    : backup retained", script);
        Assert.Contains("Sensitive values    : withheld", script);
        Assert.DoesNotContain("Write-Host $authorizationPolicySourcePath", script);
        Assert.DoesNotContain("Write-Host $authorizationPolicyDestinationPath", script);
    }

    [Fact]
    public void Updater_ShouldPreserveMigratedAuthorizationPolicyBytes()
    {
        string script = ReadUpdater();

        Assert.Contains("authorizationPolicyFilePath", script);
        Assert.Contains("$authorizationPolicyHash = Get-OptionalFileHash", script);
        Assert.Contains("$authorizationPolicyChanged", script);
        Assert.Contains("Get-OptionalFileHash -Path $authorizationPolicyPath", script);
    }

    [Fact]
    public void Updater_ShouldPreserveMediaConfigurationBytes()
    {
        string script = ReadUpdater();

        Assert.Contains("mediaConfigurationFilePath", script);
        Assert.Contains("$mediaConfigurationHash = Get-OptionalFileHash", script);
        Assert.Contains("$mediaConfigurationChanged", script);
        Assert.Contains("Get-OptionalFileHash -Path $mediaConfigurationPath", script);
    }

    private static string ReadMigration(
        [CallerFilePath] string testSourceFilePath = "") =>
        ReadDeploymentScript(
            testSourceFilePath,
            "Migrate-HaseDesktopRuntimeHostRemoteDiagnostics.ps1");

    private static string ReadUpdater(
        [CallerFilePath] string testSourceFilePath = "") =>
        ReadDeploymentScript(
            testSourceFilePath,
            "Update-HaseDesktopRuntimeHost.ps1");

    private static string ReadDeploymentScript(
        string testSourceFilePath,
        string scriptName)
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
                Path.Combine(repositoryRoot, "tools", "Deployment", scriptName))
            .Replace("\r\n", "\n", StringComparison.Ordinal);
    }
}
