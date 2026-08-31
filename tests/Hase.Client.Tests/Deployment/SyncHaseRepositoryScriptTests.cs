using System.Diagnostics;
using System.IO;
using System.Text;

namespace Hase.Client.Tests.Deployment;

/// <summary>
/// The repository synchronization script refuses rather than repairs, and
/// reports computed values rather than assurances. These tests hold it to
/// both, and prove that Windows PowerShell can parse it.
/// </summary>
public sealed class SyncHaseRepositoryScriptTests
{
    private const string ScriptFileName = "Sync-HaseRepository.ps1";

    [Fact]
    public void Script_ShouldRefuseEveryStateItCannotSafelyAdvance()
    {
        string script = ReadScript();

        // A dirty tree, another branch, and a diverged branch each mean
        // something happened on that computer that an operator must see.
        Assert.Contains(
            "Refusing to fast-forward a working tree that carries changes.",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "this script only synchronizes 'main'",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "the branch has diverged",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Script_ShouldOnlyFastForward()
    {
        string script = ReadScript();

        // Anything that could rewrite or discard local history is absent.
        Assert.Contains("git merge --ff-only origin/main", script, StringComparison.Ordinal);
        Assert.DoesNotContain("git reset", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("git checkout", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("git clean", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("git rebase", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("git push", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--force", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Script_ShouldCheckEveryGitExitCode()
    {
        string script = ReadScript();

        // Every native invocation must be followed by an exit-code check,
        // or a failed command would be reported as a successful sync.
        int invocations = CountOccurrences(script, "& git ");
        int checks = CountOccurrences(script, "if ($LASTEXITCODE -ne 0)");

        Assert.Equal(invocations, checks);
    }

    [Fact]
    public void Script_ShouldReportComputedValuesRatherThanAssurances()
    {
        string script = ReadScript();

        // The reported outcomes are variables the script computed, not
        // literal text that would read as success whatever happened.
        Assert.Contains("$isClean = $afterEntries.Count -eq 0", script, StringComparison.Ordinal);
        Assert.Contains("$isLevel = $headAfter -eq $originCommit", script, StringComparison.Ordinal);
        Assert.Contains("$didAdvance = $headBefore -ne $headAfter", script, StringComparison.Ordinal);
        Assert.Contains("clean        : $isClean", script, StringComparison.Ordinal);
        Assert.Contains("level        : $isLevel", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Script_ShouldFailWhenTheResultIsNotTheExpectedCommit()
    {
        string script = ReadScript();

        // Reporting a mismatch is not enough; it has to stop.
        Assert.Contains(
            "$isExpected = $headAfter -eq $ExpectedCommit",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "but $ExpectedCommit was expected",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Script_ShouldObserveTheWindowsPowerShellRules()
    {
        string script = ReadScript();

        Assert.Contains("$ErrorActionPreference = \"Stop\"", script, StringComparison.Ordinal);
        Assert.Contains("Set-StrictMode -Version Latest", script, StringComparison.Ordinal);

        // Collections are materialized before Count is relied upon.
        Assert.Contains(
            "$beforeEntries = @(& git status --porcelain | Where-Object { $_ })",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "$afterEntries = @(& git status --porcelain | Where-Object { $_ })",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Script_ShouldSayThatAnInstalledApplicationIsNotRefreshed()
    {
        string script = ReadScript();

        // Synchronizing Git and refreshing an installation are separate,
        // and mistaking one for the other has cost time before.
        Assert.Contains(
            "Installed applications are unchanged.",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "does not refresh an installed",
            script,
            StringComparison.Ordinal);
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
        string standardError = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.Equal(0, process.ExitCode);
        Assert.Equal(string.Empty, standardError.Trim());
    }

    private static string ScriptPath =>
        Path.Combine(AppContext.BaseDirectory, ScriptFileName);

    private static string ReadScript() =>
        File.ReadAllText(ScriptPath);

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int index = text.IndexOf(value, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = text.IndexOf(
                value,
                index + value.Length,
                StringComparison.Ordinal);
        }

        return count;
    }
}
