using System.Diagnostics;
using System.Text;
using Xunit;

namespace HASE.ProtocolExplorer.Tests.Deployment;

public sealed class Esp32ControlledUploadScriptTests
{
    private static string ReadinessScriptPath => Path.Combine(
        AppContext.BaseDirectory,
        "New-HaseEsp32ControlledUploadReadinessPlan.ps1");

    private static string UploadScriptPath => Path.Combine(
        AppContext.BaseDirectory,
        "Invoke-HaseEsp32ControlledUpload.ps1");

    [Fact]
    public void ReadinessScript_ShouldRemainReadOnlyAndBindExactCustody()
    {
        string script = File.ReadAllText(ReadinessScriptPath);

        Assert.Contains("[string]$ExpectedBundleRepositoryCommit", script);
        Assert.Contains("[string]$ExpectedBundleManifestSha256", script);
        Assert.Contains("[string]$ExpectedPreparationEvidenceSha256", script);
        Assert.Contains("Get-PnpDevice -Class Ports -PresentOnly", script);
        Assert.Contains("currentArtifacts = @($manifest.currentArtifacts)", script);
        Assert.Contains("rollbackArtifacts = @($manifest.rollbackArtifacts)", script);
        Assert.Contains("firmwareUploaded = $false", script);
        Assert.Contains("serialPortOpened = $false", script);
        Assert.Contains("physicalStateChanged = $false", script);
        Assert.Equal(2, CountOccurrences(script, "[AllowEmptyCollection()]"));
        Assert.DoesNotContain("\"upload\",", script, StringComparison.Ordinal);
        Assert.DoesNotContain("[System.IO.Ports.SerialPort]", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-Item", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UploadScript_ShouldPermitOneCurrentUploadWithoutRetryOrRollback()
    {
        string script = File.ReadAllText(UploadScriptPath);

        Assert.Equal(
            1,
            CountOccurrences(script, "\"upload\","));
        Assert.Contains("$script:uploadInvocationCount++", script);
        Assert.Contains("More than one firmware-upload invocation was attempted", script);
        Assert.Contains("\"--input-dir\", $currentRoot", script);
        Assert.DoesNotContain("\"--input-dir\", $rollbackRoot", script, StringComparison.Ordinal);
        Assert.Contains("automaticRetryAttempted = $false", script);
        Assert.Contains("automaticRollbackAttempted = $false", script);
        Assert.Contains("outcome is uncertain", script);
        Assert.Equal(2, CountOccurrences(script, "[AllowEmptyCollection()]"));
    }

    [Fact]
    public void UploadScript_ShouldFailClosedBeforePhysicalMutation()
    {
        string script = File.ReadAllText(UploadScriptPath);

        Assert.Contains("The upload-evidence root already exists", script);
        Assert.Contains("$actualReadinessPlanHash -cne $ExpectedReadinessPlanSha256", script);
        Assert.Contains("$plan.firmwareUploaded -ne $false", script);
        Assert.Contains("$plan.physicalStateChanged -ne $false", script);
        Assert.Contains("Get-PnpDevice -Class Ports -PresentOnly", script);
        Assert.Contains("upload-begin.json", script);
        Assert.Contains("upload-result.json", script);
        Assert.DoesNotContain("Remove-Item", script, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("New-HaseEsp32ControlledUploadReadinessPlan.ps1")]
    [InlineData("Invoke-HaseEsp32ControlledUpload.ps1")]
    public async Task Script_ShouldParseWithWindowsPowerShell(string scriptFileName)
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
        string encodedCommand = Convert.ToBase64String(
            Encoding.Unicode.GetBytes(command));
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
        startInfo.ArgumentList.Add(encodedCommand);
        startInfo.Environment["HASE_SCRIPT_TO_PARSE"] = Path.Combine(
            AppContext.BaseDirectory,
            scriptFileName);

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Windows PowerShell did not start.");
        string standardOutput = await process.StandardOutput.ReadToEndAsync();
        string standardError = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.True(
            process.ExitCode == 0,
            $"PowerShell parser failed.{Environment.NewLine}{standardOutput}{standardError}");
    }

    private static int CountOccurrences(string value, string search)
    {
        int count = 0;
        int offset = 0;

        while ((offset = value.IndexOf(search, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += search.Length;
        }

        return count;
    }
}
