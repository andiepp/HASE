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
        "Restore-HaseDesktopRuntimeHostMediaEnablement.ps1"
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
