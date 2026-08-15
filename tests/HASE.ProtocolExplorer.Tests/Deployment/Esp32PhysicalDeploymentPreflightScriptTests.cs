using System.Diagnostics;
using System.Text;
using Xunit;

namespace HASE.ProtocolExplorer.Tests.Deployment;

public sealed class Esp32PhysicalDeploymentPreflightScriptTests
{
    private static string ScriptPath => Path.Combine(
        AppContext.BaseDirectory,
        "Test-HaseEsp32PhysicalDeploymentPreflight.ps1");

    [Fact]
    public void Script_ShouldPreserveNonMutatingPreflightContract()
    {
        string script = File.ReadAllText(ScriptPath);

        Assert.Contains("[string]$ExpectedCommit", script);
        Assert.Contains("$ExpectedCommit = $ExpectedCommit.ToLowerInvariant()", script);
        Assert.Contains("$head[0].Trim() -cne $ExpectedCommit", script);
        Assert.Contains("$origin[0].Trim() -cne $ExpectedCommit", script);
        Assert.Contains("96db1799d410eedc82aea82cc3f5b3efa003242c", script);
        Assert.Contains("selectedPort = \"Withheld\"", script);
        Assert.Contains("localSecretsRead = $false", script);
        Assert.Contains("firmwareCompiled = $false", script);
        Assert.Contains("firmwareUploaded = $false", script);
        Assert.Contains("serialPortOpened = $false", script);
        Assert.Contains("physicalStateChanged = $false", script);
        Assert.DoesNotContain("\"compile\",", script, StringComparison.Ordinal);
        Assert.DoesNotContain("\"upload\",", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-Item", script, StringComparison.OrdinalIgnoreCase);
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
            + "$tokens = $null; "
            + "$errors = $null; "
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
