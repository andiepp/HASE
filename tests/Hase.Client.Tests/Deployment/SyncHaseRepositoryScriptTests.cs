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
    public void Script_ShouldCheckOutSubmodulesAfterTheFastForwardAndBeforeTheCleanCheck()
    {
        string script = ReadScript();

        // The fast-forward moves a submodule's recorded commit, the update
        // moves its working tree there, and only then is cleanliness
        // judged. Out of that order the moved submodule reads as a change.
        int merge = script.IndexOf("& git merge --ff-only origin/main", StringComparison.Ordinal);
        int update = script.IndexOf("& git submodule update --init", StringComparison.Ordinal);
        int afterStatus = script.IndexOf("$afterEntries = @(& git status", StringComparison.Ordinal);

        Assert.True(merge >= 0);
        Assert.True(update > merge);
        Assert.True(afterStatus > update);

        // A repository without submodules does not run the update at all.
        Assert.Contains(
            "if (Test-Path -LiteralPath (Join-Path $repositoryRoot \".gitmodules\")) {",
            script,
            StringComparison.Ordinal);

        // The recorded commit, not the submodule's own remote branch.
        Assert.DoesNotContain("--remote", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Script_ShouldCheckOutAMovedSubmoduleAtTheRecordedCommit()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = new GitFixture();
        fixture.CreateRepositoryWithSubmodule();
        string superCommit = fixture.AdvanceSubmoduleInOrigin();

        ScriptResult result = await fixture.RunScriptAsync("-ExpectedCommit", superCommit);

        Assert.True(result.ExitCode == 0, result.Describe());
        Assert.Contains("clean        : True", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("level        : True", result.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(superCommit, fixture.Git(fixture.ComputerPath, "rev-parse", "HEAD"));
        Assert.Equal(
            fixture.Git(fixture.SubmoduleOriginPath, "rev-parse", "HEAD"),
            fixture.Git(fixture.ComputerSubmodulePath, "rev-parse", "HEAD"));
        Assert.Equal(string.Empty, fixture.Git(fixture.ComputerPath, "status", "--porcelain"));
    }

    [Fact]
    public async Task Script_ShouldRefuseAndLeaveADirtySubmoduleUntouched()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = new GitFixture();
        fixture.CreateRepositoryWithSubmodule();
        string headBefore = fixture.Git(fixture.ComputerPath, "rev-parse", "HEAD");
        string submoduleBefore = fixture.Git(fixture.ComputerSubmodulePath, "rev-parse", "HEAD");
        string localFile = Path.Combine(fixture.ComputerSubmodulePath, GitFixture.FileName);
        File.WriteAllText(localFile, "local change\n");
        fixture.AdvanceSubmoduleInOrigin();

        ScriptResult result = await fixture.RunScriptAsync();

        Assert.True(result.ExitCode != 0, result.Describe());
        Assert.Contains(
            "Refusing to fast-forward a working tree that carries changes.",
            result.StandardError,
            StringComparison.Ordinal);
        Assert.Equal(headBefore, fixture.Git(fixture.ComputerPath, "rev-parse", "HEAD"));
        Assert.Equal(submoduleBefore, fixture.Git(fixture.ComputerSubmodulePath, "rev-parse", "HEAD"));
        Assert.Equal("local change\n", File.ReadAllText(localFile));
    }

    [Fact]
    public async Task Script_ShouldSynchronizeARepositoryWithoutSubmodulesAsBefore()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = new GitFixture();
        fixture.CreateRepositoryWithoutSubmodules();
        string originCommit = fixture.AdvanceOrigin();

        ScriptResult result = await fixture.RunScriptAsync("-ExpectedCommit", originCommit);

        Assert.True(result.ExitCode == 0, result.Describe());
        Assert.Contains("clean        : True", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("level        : True", result.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Submodule", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(originCommit, fixture.Git(fixture.ComputerPath, "rev-parse", "HEAD"));
    }

    [Fact]
    public void Script_ShouldCheckEveryGitExitCode()
    {
        string script = ReadScript();

        // Every native invocation must be followed by an exit-code check,
        // or a failed command would be reported as a successful sync. The
        // check is what matters, not the shape of the condition holding
        // it, so count the check itself.
        int invocations = CountOccurrences(script, "& git ");
        int checks = CountOccurrences(script, "$LASTEXITCODE -ne 0");

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
            "$isExpected = $headAfter -eq $resolvedExpected",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "but $resolvedExpected was expected",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Script_ShouldResolveTheExpectedCommitRatherThanCompareItAsText()
    {
        string script = ReadScript();

        // Comparing the forty-character commit against what an operator
        // types rejects an abbreviation, which is correct input.
        Assert.Contains(
            "git rev-parse --verify --quiet \"$ExpectedCommit^{commit}\"",
            script,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "$headAfter -eq $ExpectedCommit",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Script_ShouldRejectAnExpectedCommitThatNamesNothing()
    {
        string script = ReadScript();

        // A value that resolves to nothing is a different fault from a
        // genuine mismatch and has to say so.
        Assert.Contains(
            "does not name a commit in this repository",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "[string]::IsNullOrWhiteSpace($resolvedExpected)",
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
    public void Script_ShouldSayThatItCannotCarryAComputerAcrossItsOwnCommit()
    {
        string script = ReadScript();

        // A computer older than the commit that added this file does not
        // have this file, so the first synchronization onto it cannot use
        // the script. Saying so here is cheaper than rediscovering it.
        Assert.Contains(
            "cannot carry a computer across the commit that introduced",
            script,
            StringComparison.Ordinal);

        // And it must say what to run instead, not merely that a problem
        // exists.
        Assert.Contains(
            "has to be done with\n    Git directly",
            script.Replace("\r\n", "\n", StringComparison.Ordinal),
            StringComparison.Ordinal);
        Assert.Contains(
            "git status --porcelain      # must print nothing",
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

        string powerShellPath = PowerShellPath;
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

    private static string PowerShellPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        "System32",
        "WindowsPowerShell",
        "v1.0",
        "powershell.exe");

    private sealed record ScriptResult(int ExitCode, string StandardOutput, string StandardError)
    {
        public string Describe() =>
            $"exit {ExitCode}{Environment.NewLine}{StandardOutput}{Environment.NewLine}{StandardError}";
    }

    /// <summary>
    /// Throwaway repositories standing in for origin and for one computer's
    /// working tree. Git runs with an empty global and no system
    /// configuration, so nothing on the test computer changes the outcome.
    /// </summary>
    private sealed class GitFixture : IDisposable
    {
        public const string FileName = "file.txt";

        private static readonly (string Key, string Value)[] Configuration =
        [
            ("protocol.file.allow", "always"),
            ("user.name", "HASE Test"),
            ("user.email", "test@hase.invalid"),
            ("init.defaultBranch", "main"),
            ("commit.gpgsign", "false"),
            ("core.autocrlf", "false")
        ];

        private readonly string _root;
        private readonly string _globalConfiguration;

        public GitFixture()
        {
            _root = Path.Combine(Path.GetTempPath(), $"hase-sync-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_root);
            _globalConfiguration = Path.Combine(_root, "gitconfig");
            File.WriteAllText(_globalConfiguration, string.Empty);
        }

        public string OriginPath => Path.Combine(_root, "origin");

        public string SubmoduleOriginPath => Path.Combine(_root, "submodule-origin");

        public string ComputerPath => Path.Combine(_root, "computer");

        public string ComputerSubmodulePath => Path.Combine(ComputerPath, "base");

        public void CreateRepositoryWithoutSubmodules()
        {
            CreateRepository(OriginPath);
            Git(_root, "clone", OriginPath, ComputerPath);
        }

        public void CreateRepositoryWithSubmodule()
        {
            CreateRepository(SubmoduleOriginPath);
            CreateRepository(OriginPath);
            Git(OriginPath, "submodule", "add", ToGitPath(SubmoduleOriginPath), "base");
            Git(OriginPath, "commit", "-m", "add base");
            Git(_root, "clone", "--recurse-submodules", OriginPath, ComputerPath);
        }

        public string AdvanceOrigin()
        {
            Commit(OriginPath, "origin advanced\n");
            return Git(OriginPath, "rev-parse", "HEAD");
        }

        public string AdvanceSubmoduleInOrigin()
        {
            Commit(SubmoduleOriginPath, "base advanced\n");
            string originSubmodule = Path.Combine(OriginPath, "base");
            Git(originSubmodule, "pull", "--ff-only", "origin", "main");
            Git(OriginPath, "add", "base");
            Git(OriginPath, "commit", "-m", "move base");
            return Git(OriginPath, "rev-parse", "HEAD");
        }

        public string Git(string workingDirectory, params string[] arguments)
        {
            var startInfo = CreateStartInfo("git", workingDirectory);
            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Git did not start.");
            Task<string> standardError = process.StandardError.ReadToEndAsync();
            string standardOutput = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"git {string.Join(' ', arguments)} failed: {standardError.Result}");
            }

            return standardOutput.Trim();
        }

        public async Task<ScriptResult> RunScriptAsync(params string[] arguments)
        {
            Assert.True(File.Exists(PowerShellPath));

            var startInfo = CreateStartInfo(PowerShellPath, ComputerPath);
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-NonInteractive");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(ScriptPath);
            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Windows PowerShell did not start.");
            Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
            Task<string> standardError = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            return new ScriptResult(
                process.ExitCode,
                await standardOutput,
                await standardError);
        }

        public void Dispose()
        {
            try
            {
                // Git writes its objects read-only, which Directory.Delete
                // refuses to remove.
                foreach (string file in Directory.EnumerateFiles(
                    _root, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(_root, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private void CreateRepository(string path)
        {
            Directory.CreateDirectory(path);
            Git(path, "init");
            Commit(path, "first\n");
        }

        private void Commit(string repository, string content)
        {
            File.WriteAllText(Path.Combine(repository, FileName), content);
            Git(repository, "add", FileName);
            Git(repository, "commit", "-m", content.Trim());
        }

        private ProcessStartInfo CreateStartInfo(string fileName, string workingDirectory)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            startInfo.Environment["GIT_CONFIG_GLOBAL"] = _globalConfiguration;
            startInfo.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
            startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
            startInfo.Environment["GIT_CONFIG_COUNT"] =
                Configuration.Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
            for (int index = 0; index < Configuration.Length; index++)
            {
                startInfo.Environment[$"GIT_CONFIG_KEY_{index}"] = Configuration[index].Key;
                startInfo.Environment[$"GIT_CONFIG_VALUE_{index}"] = Configuration[index].Value;
            }

            return startInfo;
        }

        private static string ToGitPath(string path) =>
            path.Replace('\\', '/');
    }
}
