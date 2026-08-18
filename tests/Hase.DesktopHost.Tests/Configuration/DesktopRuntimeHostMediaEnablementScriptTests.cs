using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace Hase.DesktopHost.Tests.Configuration;

public sealed class DesktopRuntimeHostMediaEnablementScriptTests
{
    private static readonly string[] ScriptNames =
    [
        "HaseMediaEnablement.Common.ps1",
        "New-HaseClientRuntimeHostMediaAuthorizationRequest.ps1",
        "New-HaseDesktopRuntimeHostMediaBindingCandidate.ps1",
        "Test-HaseDesktopRuntimeHostMediaEnablement.ps1",
        "Enable-HaseDesktopRuntimeHostMedia.ps1",
        "Restore-HaseDesktopRuntimeHostMediaEnablement.ps1",
        "HaseMediaReplacement.Common.ps1",
        "Test-HaseDesktopRuntimeHostMediaReplacement.ps1",
        "Replace-HaseDesktopRuntimeHostMedia.ps1",
        "Install-HaseDesktopRuntimeHost.ps1",
        "Publish-HaseDesktopRuntimeHost.ps1",
        "Update-HaseDesktopRuntimeHost.ps1"
    ];

    [Fact]
    public void BindingTool_ShouldRequireExplicitInstalledApplicationMode()
    {
        string script = ReadScript(
            "New-HaseDesktopRuntimeHostMediaBindingCandidate.ps1");

        Assert.Contains("AEPRAKETE", script);
        Assert.Contains("--prepare-media-binding", script);
        Assert.Contains("binding.html", script);
        Assert.Contains("Start-Process", script);
        Assert.Contains("Device identifiers withheld", script);
        Assert.DoesNotContain("Hase.Client.Wpf.App.exe", script);
    }

    [Fact]
    public void BindingAndEnablement_ShouldAcceptBoundedMultipleCameraCandidate()
    {
        string binding = ReadScript(
            "New-HaseDesktopRuntimeHostMediaBindingCandidate.ps1");
        string common = ReadScript("HaseMediaEnablement.Common.ps1");
        string enablement = ReadScript("Enable-HaseDesktopRuntimeHostMedia.ps1");

        Assert.Contains("$sources.Count -gt 16", binding);
        Assert.Contains("Selected camera count", binding);
        Assert.Contains("$sources.Count -gt 16", common);
        Assert.Contains("$plan.SourceCount", enablement);
        Assert.Contains("video-device identities must be unique", common);
    }

    [Fact]
    public void ClientRequest_ShouldHashCertificatesWithoutPrintingIdentifiers()
    {
        string script = ReadScript(
            "New-HaseClientRuntimeHostMediaAuthorizationRequest.ps1");

        Assert.Contains("LTAEP", script);
        Assert.Contains("SHA256", script);
        Assert.Contains("x509-sha256:", script);
        Assert.Contains("Certificate values withheld", script);
        Assert.DoesNotContain("Write-Host $credentialId", script);
        Assert.DoesNotContain("Write-Host $certificateHash", script);
    }

    [Fact]
    public void Preflight_ShouldRemainReadOnlyAndProduceTransactionIdentity()
    {
        string script = ReadScript(
            "Test-HaseDesktopRuntimeHostMediaEnablement.ps1");

        Assert.Contains("Get-HaseMediaEnablementPlan", script);
        Assert.Contains("Transaction ID", script);
        Assert.Contains("made no file", script);
        Assert.DoesNotContain("Move-Item", script);
        Assert.DoesNotContain("File]::Replace", script);
        Assert.DoesNotContain("Remove-Item", script);
    }

    [Fact]
    public void Enablement_ShouldMutateOnlyThreeConfigurationDocuments()
    {
        string script = ReadScript("Enable-HaseDesktopRuntimeHostMedia.ps1");

        Assert.Contains("desktop-runtime-host.before.json", script);
        Assert.Contains("runtime-host-authorization.before.json", script);
        Assert.Contains("desktop-runtime-media", ReadScript(
            "HaseMediaEnablement.Common.ps1"));
        Assert.Contains("[System.IO.File]::Replace", script);
        Assert.Contains("Recovery\\ADR-0055-55F", script);
        Assert.Contains("media.audio.receive", ReadScript(
            "HaseMediaEnablement.Common.ps1"));
        Assert.DoesNotContain("Start-Process", script);
        Assert.DoesNotContain("GetUserMedia", script,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Enablement_ShouldUseProtectedReplacementBackups()
    {
        string script = ReadScript("Enable-HaseDesktopRuntimeHostMedia.ps1");

        Assert.Contains("desktop-runtime-host.replace-backup.json", script);
        Assert.Contains(
            "runtime-host-authorization.replace-backup.json",
            script);
        Assert.Contains("authorization replacement backup", script);
        Assert.Contains("profile replacement backup", script);
        Assert.DoesNotContain(
            "$plan.PolicyPath," + Environment.NewLine + "        $null",
            script);
        Assert.DoesNotContain(
            "$plan.ProfilePath," + Environment.NewLine + "        $null",
            script);
    }

    [Fact]
    public void Enablement_ShouldReuseOnlyExactPreparedRecoveryTransaction()
    {
        string script = ReadScript("Enable-HaseDesktopRuntimeHostMedia.ps1");

        Assert.Contains("$transactionDirectoryReused", script);
        Assert.Contains("New-Item -ItemType Directory", script);
        Assert.Contains("Copy-Item -LiteralPath $plan.ProfilePath", script);
        Assert.Contains("Write-HaseUtf8Json $manifestPath", script);
        Assert.Contains("existing media enablement recovery manifest", script);
        Assert.Contains("[string]$existingManifest.state -ceq \"prepared\"",
            script);
        Assert.Contains("$existingManifestMatches", script);
        Assert.Contains(
            "does not match the current plan",
            script);
        Assert.Contains("$temporaryPaths", script);
        Assert.Contains("Remove-Item -LiteralPath $temporaryPath", script);
    }

    [Fact]
    public void Enablement_ShouldRollbackEveryLiveDocumentAfterMutationFailure()
    {
        string script = ReadScript("Enable-HaseDesktopRuntimeHostMedia.ps1");

        Assert.Contains("catch {", script);
        Assert.Contains(
            "Copy-Item -LiteralPath $profileBackup",
            script);
        Assert.Contains(
            "Copy-Item -LiteralPath $policyBackup",
            script);
        Assert.Contains("Set-HaseFileAccessSddl $plan.ProfilePath", script);
        Assert.Contains("Set-HaseFileAccessSddl $plan.PolicyPath", script);
        Assert.Contains("Remove-Item -LiteralPath $plan.MediaPath", script);
        Assert.Contains("finally {", script);
        Assert.Contains("Remove-Item -LiteralPath $temporaryPath", script);
    }

    [Fact]
    public void Restore_ShouldRetainEvidenceAndRequireExactEnabledHashes()
    {
        string script = ReadScript(
            "Restore-HaseDesktopRuntimeHostMediaEnablement.ps1");

        Assert.Contains("ExpectedManifestSha256", script);
        Assert.Contains("enabledProfileSha256", script);
        Assert.Contains("enabledPolicySha256", script);
        Assert.Contains("enabledMediaSha256", script);
        Assert.Contains("Recovery evidence retained", script);
        Assert.DoesNotContain("Remove-Item -LiteralPath $transactionDirectory",
            script);
    }

    [Fact]
    public void ReplacementPreflight_ShouldRemainReadOnlyAndBindExactPlan()
    {
        string script = ReadScript(
            "Test-HaseDesktopRuntimeHostMediaReplacement.ps1");

        Assert.Contains("Get-HaseMediaReplacementPlan", script);
        Assert.Contains("Current source count", script);
        Assert.Contains("Replacement source count", script);
        Assert.Contains("Transaction ID", script);
        Assert.Contains("made no file", script);
        Assert.DoesNotContain("File]::Replace", script);
        Assert.DoesNotContain("Copy-Item", script);
        Assert.DoesNotContain("Remove-Item", script);
    }

    [Fact]
    public void ReplacementPreflight_ShouldExposeBoundedCountsWithLegacyDefaults()
    {
        string script = ReadScript(
            "Test-HaseDesktopRuntimeHostMediaReplacement.ps1");

        Assert.Contains("[ValidateRange(1, 16)]", script);
        Assert.Contains("[int]$ExpectedCurrentSourceCount = 1", script);
        Assert.Contains("[int]$ExpectedReplacementSourceCount = 2", script);
        Assert.Contains(
            "-ExpectedCurrentSourceCount $ExpectedCurrentSourceCount",
            script);
        Assert.Contains(
            "-ExpectedReplacementSourceCount $ExpectedReplacementSourceCount",
            script);
        Assert.Contains("ExpectedCurrentAudioConfigured", script);
        Assert.Contains("ExpectedReplacementAudioConfigured", script);
        Assert.Contains("Policy change required", script);
        Assert.DoesNotContain("-ExpectedCurrentSourceCount 1", script);
        Assert.DoesNotContain("-ExpectedReplacementSourceCount 2", script);
    }

    [Fact]
    public void ReplacementPlan_ShouldRequireExactCurrentAuthorizationAndAudioTransition()
    {
        string common = ReadScript("HaseMediaReplacement.Common.ps1");

        Assert.Contains("ExpectedCurrentSourceCount", common);
        Assert.Contains("ExpectedReplacementSourceCount", common);
        Assert.Contains("ExpectedCurrentAudioConfigured", common);
        Assert.Contains("ExpectedReplacementAudioConfigured", common);
        Assert.Contains("Get-HaseMediaAuthorizationState", common);
        Assert.Contains("media.audio.receive", common);
        Assert.Contains("PolicyChangeRequired", common);
        Assert.Contains("$script:HaseMediaPermissions", common);
        Assert.Contains("mediaConfigurationFilePath", common);
        Assert.Contains("outside guided preparation custody", common);
        Assert.DoesNotContain("Set-Acl", common);
    }

    [Fact]
    public void Replacement_ShouldBackUpAndReplaceMediaAndChangedPolicy()
    {
        string script = ReadScript(
            "Replace-HaseDesktopRuntimeHostMedia.ps1");

        Assert.Contains("Recovery\\ADR-0055-55F4-Rebind", script);
        Assert.Contains("desktop-runtime-host.before.json", script);
        Assert.Contains("runtime-host-authorization.before.json", script);
        Assert.Contains("desktop-runtime-media.before.json", script);
        Assert.Contains("[System.IO.File]::Replace", script);
        Assert.Contains("media replacement backup", script);
        Assert.Contains("authorization replacement backup", script);
        Assert.Contains("runtime-host-authorization.replace-backup.json", script);
        Assert.Contains("originalMediaGrantCount", script);
        Assert.Contains("replacementMediaGrantCount", script);
        Assert.Contains("currentAudioConfigured", script);
        Assert.Contains("replacementAudioConfigured", script);
        Assert.Contains("Profile hash preserved", script);
        Assert.Contains("Policy changed", script);
        Assert.DoesNotContain("$plan.ProfilePath,", script);
        Assert.DoesNotContain("Start-Process", script);
        Assert.DoesNotContain("GetUserMedia", script,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Replacement_ShouldExposeBoundedCountsWithLegacyDefaults()
    {
        string script = ReadScript(
            "Replace-HaseDesktopRuntimeHostMedia.ps1");

        Assert.Contains("[ValidateRange(1, 16)]", script);
        Assert.Contains("[int]$ExpectedCurrentSourceCount = 1", script);
        Assert.Contains("[int]$ExpectedReplacementSourceCount = 2", script);
        Assert.Contains(
            "-ExpectedCurrentSourceCount $ExpectedCurrentSourceCount",
            script);
        Assert.Contains(
            "-ExpectedReplacementSourceCount $ExpectedReplacementSourceCount",
            script);
        Assert.Contains(
            "-ExpectedCurrentAudioConfigured $ExpectedCurrentAudioConfigured",
            script);
        Assert.Contains(
            "-ExpectedReplacementAudioConfigured $ExpectedReplacementAudioConfigured",
            script);
        Assert.DoesNotContain("-ExpectedCurrentSourceCount 1", script);
        Assert.DoesNotContain("-ExpectedReplacementSourceCount 2", script);
    }

    [Fact]
    public void Replacement_ShouldRollbackMediaAndPolicyAfterMutationFailure()
    {
        string script = ReadScript(
            "Replace-HaseDesktopRuntimeHostMedia.ps1");

        Assert.Contains("catch {", script);
        Assert.Contains("Copy-Item -LiteralPath $mediaBackup", script);
        Assert.Contains("Copy-Item -LiteralPath $policyBackup", script);
        Assert.Contains("Set-HaseFileAccessSddl $plan.MediaPath", script);
        Assert.Contains("Set-HaseFileAccessSddl $plan.PolicyPath", script);
        Assert.Contains("rolled-back media configuration", script);
        Assert.Contains("rolled-back authorization policy", script);
        Assert.Contains("Write-HaseUtf8Json $manifestPath $preparedManifest", script);
        Assert.Contains("replacement and rollback both failed", script);
        Assert.Contains("finally {", script);
        Assert.Contains("Remove-Item -LiteralPath $temporaryPath", script);
    }

    [Fact]
    public void MediaTools_ShouldPersistOnlyAccessRulesAndAvoidSetAcl()
    {
        string common = ReadScript("HaseMediaEnablement.Common.ps1");
        string binding = ReadScript(
            "New-HaseDesktopRuntimeHostMediaBindingCandidate.ps1");
        string enable = ReadScript("Enable-HaseDesktopRuntimeHostMedia.ps1");
        string restore = ReadScript(
            "Restore-HaseDesktopRuntimeHostMediaEnablement.ps1");

        Assert.Contains("AccessControlSections]::Access", common);
        Assert.Contains("GetSecurityDescriptorSddlForm", common);
        Assert.Contains("SetSecurityDescriptorSddlForm", common);
        Assert.Contains("$directoryInfo.SetAccessControl($directorySecurity)",
            common);
        Assert.Contains("$directoryAlreadyExisted", binding);
        Assert.Contains("Test-HaseProtectedDirectoryAccessControl", binding);
        Assert.Contains("grants = $grantDocuments.ToArray()", enable);
        Assert.Contains("Set-HaseFileAccessSddl", enable);
        Assert.Contains("Set-HaseFileAccessSddl", restore);

        foreach (string scriptName in ScriptNames)
        {
            Assert.DoesNotContain("Set-Acl", ReadScript(scriptName));
        }
    }

    [Theory]
    [MemberData(nameof(Scripts))]
    public async Task Script_ShouldParseWithWindowsPowerShell(string scriptName)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string powerShellPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32",
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe");
        Assert.True(File.Exists(powerShellPath));
        const string command = ""
            + "$tokens = $null; $errors = $null; "
            + "[System.Management.Automation.Language.Parser]::ParseFile("
            + "$env:HASE_SCRIPT_TO_PARSE, [ref]$tokens, [ref]$errors) | Out-Null; "
            + "if (@($errors).Count -ne 0) { "
            + "$errors | ForEach-Object { [Console]::Error.WriteLine($_.Message) }; "
            + "exit 1 }; exit 0";
        var startInfo = new ProcessStartInfo
        {
            FileName = powerShellPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-EncodedCommand");
        startInfo.ArgumentList.Add(Convert.ToBase64String(
            Encoding.Unicode.GetBytes(command)));
        startInfo.Environment["HASE_SCRIPT_TO_PARSE"] = ScriptPath(scriptName);

        using Process process = Process.Start(startInfo) ??
            throw new InvalidOperationException("Windows PowerShell did not start.");
        string standardOutput = await process.StandardOutput.ReadToEndAsync();
        string standardError = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.True(
            process.ExitCode == 0,
            $"PowerShell parser failed.{Environment.NewLine}{standardOutput}{standardError}");
    }

    public static IEnumerable<object[]> Scripts() =>
        ScriptNames.Select(name => new object[] { name });

    private static string ReadScript(string fileName) =>
        File.ReadAllText(ScriptPath(fileName));

    private static string ScriptPath(
        string fileName,
        [CallerFilePath] string testSourceFilePath = "")
    {
        string repositoryRoot = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(testSourceFilePath)!,
            "..",
            "..",
            ".."));
        return Path.Combine(
            repositoryRoot,
            "tools",
            "Deployment",
            fileName);
    }
}
