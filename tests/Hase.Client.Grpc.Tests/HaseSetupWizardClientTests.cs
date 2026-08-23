using System.Diagnostics;
using System.Runtime.CompilerServices;
using Hase.Client.Grpc.Configuration;

namespace Hase.Client.Grpc.Tests;

public sealed class HaseSetupWizardClientTests
{
    [Fact]
    public async Task ClientRole_Success_ShouldAuthorLoadableRegistryFromHandoff()
    {
        using var directory = new WizardTestDirectory();
        string stubPath = WriteInstallStub(directory.Path);
        string bundlePath = Path.Combine(directory.Path, "transfer");
        Directory.CreateDirectory(bundlePath);
        File.WriteAllText(
            Path.Combine(bundlePath, "laptop-private-network.json"),
            "{ \"formatVersion\": 1 }");
        File.WriteAllText(
            Path.Combine(bundlePath, "client-handoff.json"),
            """
            {
              "formatVersion": 1,
              "profileId": "example-host",
              "displayName": "Example Host (secured)",
              "expectedRuntimeHostId": "hase-example-host-01"
            }
            """);

        (int exitCode, string standardError) = RunWizard(
            "-BundleDirectory", bundlePath,
            "-InstallScriptPath", stubPath);

        Assert.True(exitCode == 0, standardError);
        Assert.True(
            File.Exists(
                Path.Combine(directory.Path, "install-invoked.txt")));

        PrivateNetworkRuntimeHostProfileRegistry registry =
            await PrivateNetworkRuntimeHostProfileRegistryFile.LoadAsync(
                Path.Combine(bundlePath, "client-runtime-hosts.json"));

        Hase.Client.Configuration.RuntimeHostProfile coreProfile =
            Assert.Single(registry.CoreProfiles.Profiles);
        Assert.True(
            registry.TryGet(
                coreProfile.ProfileId,
                out PrivateNetworkRuntimeHostProfile? resolved));
        PrivateNetworkRuntimeHostProfile profile = resolved!;
        Assert.Equal("example-host", profile.Profile.ProfileId.Value);
        Assert.Equal(
            "Example Host (secured)",
            profile.Profile.DisplayName);
        Assert.Equal(
            "hase-example-host-01",
            profile.Profile.ExpectedRuntimeHostId.Value);
        Assert.True(profile.Profile.IsEnabled);
        Assert.Equal(
            Path.Combine(bundlePath, "laptop-private-network.json"),
            profile.PrivateNetworkConfigurationFilePath);
    }

    [Fact]
    public void ClientRole_MissingHandoff_ShouldRefuseBeforeInstallation()
    {
        using var directory = new WizardTestDirectory();
        string stubPath = WriteInstallStub(directory.Path);
        string bundlePath = Path.Combine(directory.Path, "transfer");
        Directory.CreateDirectory(bundlePath);
        File.WriteAllText(
            Path.Combine(bundlePath, "laptop-private-network.json"),
            "{ \"formatVersion\": 1 }");

        (int exitCode, string standardError) = RunWizard(
            "-BundleDirectory", bundlePath,
            "-InstallScriptPath", stubPath);

        Assert.NotEqual(0, exitCode);
        Assert.Contains("client-handoff.json is missing", standardError);
        Assert.False(
            File.Exists(
                Path.Combine(directory.Path, "install-invoked.txt")));
        Assert.False(
            File.Exists(
                Path.Combine(bundlePath, "client-runtime-hosts.json")));
    }

    [Fact]
    public void ClientRole_ExistingRegistry_ShouldRefuse()
    {
        using var directory = new WizardTestDirectory();
        string stubPath = WriteInstallStub(directory.Path);
        string bundlePath = Path.Combine(directory.Path, "transfer");
        Directory.CreateDirectory(bundlePath);
        File.WriteAllText(
            Path.Combine(bundlePath, "client-handoff.json"),
            "{ \"formatVersion\": 1 }");
        File.WriteAllText(
            Path.Combine(bundlePath, "client-runtime-hosts.json"),
            "{}");

        (int exitCode, string standardError) = RunWizard(
            "-BundleDirectory", bundlePath,
            "-InstallScriptPath", stubPath);

        Assert.NotEqual(0, exitCode);
        Assert.Contains("already exists", standardError);
        Assert.False(
            File.Exists(
                Path.Combine(directory.Path, "install-invoked.txt")));
    }

    private static string WriteInstallStub(string directoryPath)
    {
        string stubPath = Path.Combine(directoryPath, "stub-install.ps1");
        string markerPath = Path.Combine(directoryPath, "install-invoked.txt");
        string stub =
            "param($BundleDirectory)\n"
            + "$ErrorActionPreference = \"Stop\"\n"
            + "[System.IO.File]::WriteAllText(\n"
            + $"    \"{markerPath.Replace("\\", "\\\\")}\",\n"
            + "    $BundleDirectory)\n";
        File.WriteAllText(stubPath, stub);
        return stubPath;
    }

    private static (int ExitCode, string StandardError) RunWizard(
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
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(WizardPath());
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)!;
        string standardError = process.StandardError.ReadToEnd();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, standardError);
    }

    private static string WizardPath(
        [CallerFilePath] string testSourceFilePath = "")
    {
        string repositoryRoot = Path.GetFullPath(
            Path.Combine(
                Path.GetDirectoryName(testSourceFilePath)!,
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
