using System.Diagnostics;
using System.Text;
using Xunit;

namespace HASE.ProtocolExplorer.Tests.Deployment;

public sealed class Esp32DeploymentBundleScriptTests
{
    private static string ScriptPath => Path.Combine(
        AppContext.BaseDirectory,
        "New-HaseEsp32DeploymentBundle.ps1");

    [Fact]
    public void Script_ShouldPreserveCompilationOnlySecurityContract()
    {
        string script = File.ReadAllText(ScriptPath);

        Assert.Contains("[string]$ExpectedCommit", script);
        Assert.Contains("96db1799d410eedc82aea82cc3f5b3efa003242c", script);
        Assert.Contains("Esp32DeploymentBundles", script);
        Assert.Contains("firmwareCompiled = $true", script);
        Assert.Contains("firmwareUploaded = $false", script);
        Assert.Contains("serialPortOpened = $false", script);
        Assert.Contains("physicalStateChanged = $false", script);
        Assert.Contains("secretsCopiedToEvidence = $secretsCopiedToEvidence", script);
        Assert.DoesNotContain("\"upload\",", script, StringComparison.Ordinal);
        Assert.DoesNotContain("board\", \"list", script, StringComparison.Ordinal);
        Assert.DoesNotContain("[System.IO.Ports.SerialPort]", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Script_ShouldRequireNewOutputDirectoriesAndProtectedBundleRoot()
    {
        string script = File.ReadAllText(ScriptPath);

        Assert.Contains("The sensitive bundle root must be under the current user's local HASE custody", script);
        Assert.Contains("The sensitive bundle root already exists", script);
        Assert.Contains("The evidence root already exists", script);
        Assert.Contains("HaseSecrets.h", script);
        Assert.Contains("git archive", script);
        Assert.Contains("finally", script);
        Assert.Contains("Remove-Item -LiteralPath $workingRoot", script);
    }

    [Fact]
    public async Task Script_ShouldParseWithWindowsPowerShell()
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
        startInfo.Environment["HASE_SCRIPT_TO_PARSE"] = ScriptPath;

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Windows PowerShell did not start.");
        string standardOutput = await process.StandardOutput.ReadToEndAsync();
        string standardError = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.True(
            process.ExitCode == 0,
            $"PowerShell parser failed.{Environment.NewLine}{standardOutput}{standardError}");
    }
}
