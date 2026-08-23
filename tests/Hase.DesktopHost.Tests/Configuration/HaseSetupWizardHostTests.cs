using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Hase.DesktopHost.Configuration;
using Hase.Runtime.Remote.Grpc.Adapter;

namespace Hase.DesktopHost.Tests.Configuration;

public sealed class HaseSetupWizardHostTests
{
    [Fact]
    public void Wizard_ShouldParseWithWindowsPowerShell()
    {
        (int exitCode, string standardError) = RunWindowsPowerShell(
            environment => environment["HASE_SCRIPT_TO_PARSE"] = WizardPath(),
            "-EncodedCommand",
            EncodeCommand(
                "$tokens = $null; $errors = $null; "
                + "[System.Management.Automation.Language.Parser]::ParseFile("
                + "$env:HASE_SCRIPT_TO_PARSE, [ref]$tokens, [ref]$errors) | Out-Null; "
                + "if (@($errors).Count -ne 0) { "
                + "$errors | ForEach-Object { [Console]::Error.WriteLine($_.Message) }; "
                + "exit 1 }; exit 0"));

        Assert.True(exitCode == 0, standardError);
    }

    [Fact]
    public async Task HostRole_Success_ShouldAuthorLoadableDocuments()
    {
        using var directory = new WizardTestDirectory();
        string stubPath = WriteSucceedingBundleStub(directory.Path);
        string outputPath = Path.Combine(directory.Path, "secured");

        (int exitCode, string standardError) = RunWizard(
            "-ListenerAddress", "192.0.2.10",
            "-Port", "52210",
            "-OutputDirectory", outputPath,
            "-BundleScriptPath", stubPath);

        Assert.True(exitCode == 0, standardError);

        DesktopRuntimeHostInstallationProfile installation =
            await DesktopRuntimeHostInstallationProfileFile.LoadAsync(
                Path.Combine(outputPath, "desktop-runtime-host.json"));
        Assert.True(installation.IncludeByteBufferSimulation);
        Assert.Equal(
            Path.Combine(outputPath, "runtime-host-identity.json"),
            installation.IdentityFilePath);
        Assert.Equal(
            Path.Combine(outputPath, "authorization-policy.json"),
            installation.AuthorizationPolicyFilePath);

        DesktopRuntimeHostEndpointCompositionProfile composition =
            await DesktopRuntimeHostEndpointCompositionProfileFile.LoadAsync(
                installation.EndpointCompositionFilePath);
        Assert.Single(composition.CompactSerialEndpoints);

        RuntimeHostAuthorizationPolicy policy =
            await RuntimeHostAuthorizationPolicyFile.LoadAsync(
                installation.AuthorizationPolicyFilePath!);
        Assert.NotNull(policy);

        using JsonDocument policyDocument = JsonDocument.Parse(
            File.ReadAllText(
                installation.AuthorizationPolicyFilePath!));
        JsonElement grants = policyDocument.RootElement.GetProperty("grants");
        Assert.Equal(6, grants.GetArrayLength());
        foreach (JsonElement grant in grants.EnumerateArray())
        {
            Assert.Equal(
                "laptop-validation-client",
                grant.GetProperty("principalId").GetString());
        }

        using JsonDocument identityDocument = JsonDocument.Parse(
            File.ReadAllText(
                Path.Combine(outputPath, "runtime-host-identity.json")));
        Assert.Equal(
            "hase-example-host-01",
            identityDocument.RootElement
                .GetProperty("runtimeHostId")
                .GetString());

        using JsonDocument handoffDocument = JsonDocument.Parse(
            File.ReadAllText(
                Path.Combine(outputPath, "client-handoff.json")));
        Assert.Equal(
            "example-host",
            handoffDocument.RootElement.GetProperty("profileId").GetString());
        Assert.Equal(
            "hase-example-host-01",
            handoffDocument.RootElement
                .GetProperty("expectedRuntimeHostId")
                .GetString());
    }

    [Fact]
    public void HostRole_ExistingAuthoredTarget_ShouldRefuseBeforeBundleCreation()
    {
        using var directory = new WizardTestDirectory();
        string stubPath = WriteSucceedingBundleStub(directory.Path);
        string outputPath = Path.Combine(directory.Path, "secured");
        Directory.CreateDirectory(outputPath);
        File.WriteAllText(
            Path.Combine(outputPath, "desktop-runtime-host.json"),
            "{}");

        (int exitCode, string standardError) = RunWizard(
            "-ListenerAddress", "192.0.2.10",
            "-Port", "52210",
            "-OutputDirectory", outputPath,
            "-BundleScriptPath", stubPath);

        Assert.NotEqual(0, exitCode);
        Assert.Contains("already exists", standardError);
        Assert.False(
            File.Exists(
                Path.Combine(outputPath, "desktop-private-network.json")));
        Assert.False(
            File.Exists(
                Path.Combine(outputPath, "client-handoff.json")));
    }

    [Fact]
    public void HostRole_BundleFailure_ShouldAuthorNothing()
    {
        using var directory = new WizardTestDirectory();
        string stubPath = Path.Combine(directory.Path, "failing-bundle.ps1");
        File.WriteAllText(
            stubPath,
            "param($ListenerAddress, $Port, $OutputDirectory, "
            + "$ClientPrincipalId)\n"
            + "throw \"Simulated provisioning failure.\"\n");
        string outputPath = Path.Combine(directory.Path, "secured");

        (int exitCode, string standardError) = RunWizard(
            "-ListenerAddress", "192.0.2.10",
            "-Port", "52210",
            "-OutputDirectory", outputPath,
            "-BundleScriptPath", stubPath);

        Assert.NotEqual(0, exitCode);
        Assert.Contains("Simulated provisioning failure", standardError);
        Assert.False(
            File.Exists(
                Path.Combine(outputPath, "desktop-runtime-host.json")));
        Assert.False(
            File.Exists(
                Path.Combine(outputPath, "client-handoff.json")));
    }

    [Fact]
    public void HostRole_InvalidPort_ShouldReject()
    {
        using var directory = new WizardTestDirectory();
        string stubPath = WriteSucceedingBundleStub(directory.Path);

        (int exitCode, _) = RunWizard(
            "-ListenerAddress", "192.0.2.10",
            "-Port", "0",
            "-OutputDirectory", Path.Combine(directory.Path, "secured"),
            "-BundleScriptPath", stubPath);

        Assert.NotEqual(0, exitCode);
    }

    private static string WriteSucceedingBundleStub(string directoryPath)
    {
        string stubPath = Path.Combine(directoryPath, "stub-bundle.ps1");
        const string stub =
            "param($ListenerAddress, $Port, $OutputDirectory, "
            + "$ClientPrincipalId)\n"
            + "$ErrorActionPreference = \"Stop\"\n"
            + "[System.IO.Directory]::CreateDirectory($OutputDirectory) "
            + "| Out-Null\n"
            + "$address = $ListenerAddress.IPAddressToString\n"
            + "$desktop = \"{ `\"formatVersion`\": 1 }\"\n"
            + "$names = @(\n"
            + "    \"desktop-private-network.json\",\n"
            + "    \"laptop-private-network.json\",\n"
            + "    \"client-enrollments.json\",\n"
            + "    \"laptop-client.pfx\",\n"
            + "    \"runtime-host-server.cer\"\n"
            + ")\n"
            + "foreach ($name in $names) {\n"
            + "    [System.IO.File]::WriteAllText(\n"
            + "        [System.IO.Path]::Combine($OutputDirectory, $name),\n"
            + "        $desktop)\n"
            + "}\n";
        File.WriteAllText(stubPath, stub);
        return stubPath;
    }

    private static (int ExitCode, string StandardError) RunWizard(
        params string[] arguments)
    {
        string[] fileArguments =
            ["-File", WizardPath(), .. arguments];

        return RunWindowsPowerShell(
            _ => { },
            fileArguments);
    }

    private static (int ExitCode, string StandardError) RunWindowsPowerShell(
        Action<System.Collections.Specialized.StringDictionary> configureEnvironment,
        params string[] arguments)
    {
        string powerShellPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32",
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe");
        Assert.True(File.Exists(powerShellPath));

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
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        configureEnvironment(startInfo.EnvironmentVariables);

        using Process process = Process.Start(startInfo)!;
        string standardError = process.StandardError.ReadToEnd();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, standardError);
    }

    private static string EncodeCommand(string command) =>
        Convert.ToBase64String(Encoding.Unicode.GetBytes(command));

    private static string WizardPath(
        [CallerFilePath] string testSourceFilePath = "")
    {
        string repositoryRoot = Path.GetFullPath(
            Path.Combine(
                Path.GetDirectoryName(testSourceFilePath)!,
                "..",
                "..",
                ".."));
        return Path.Combine(
            repositoryRoot,
            "tools",
            "Setup",
            "Start-HaseSetup.ps1");
    }

    private sealed class WizardTestDirectory : IDisposable
    {
        public WizardTestDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"hase-60h-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
