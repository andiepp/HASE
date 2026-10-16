using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace Hase.Python.CredentialProvisioning.Tests;

/// <summary>
/// The ADR-0083 operator scripts: parsed by Windows PowerShell, and the
/// read-only preflight and verifier run against a temporary installation.
/// </summary>
public sealed class PythonCredentialReissueToolTests : IDisposable
{
    private const string StartTool = "Start-HasePythonCredentialReissue.ps1";
    private const string TestTool = "Test-HasePythonCredentialReissue.ps1";
    private const string RecoverTool = "Recover-HasePythonCredentialReissue.ps1";
    private const string Principal = "hase-python-automation";
    private const string TrustPolicy = "private-network-validation-v1";

    private static readonly string[] Grants =
    [
        "runtime-host.snapshot.read",
        "property.authoritative.read",
        "property.write",
    ];

    private readonly DateTimeOffset now = WholeSeconds(DateTimeOffset.UtcNow);
    private readonly string directory = Path.Combine(
        Path.GetTempPath(), $"hase-python-reissue-tool-{Guid.NewGuid():N}");

    public PythonCredentialReissueToolTests() =>
        Directory.CreateDirectory(directory);

    public void Dispose() => Directory.Delete(directory, recursive: true);

    [Theory]
    [InlineData(StartTool)]
    [InlineData(TestTool)]
    [InlineData(RecoverTool)]
    public async Task Tool_ParsesWithWindowsPowerShell(string tool)
    {
        if (!OperatingSystem.IsWindows()) return;

        const string command = ""
            + "$tokens = $null; $errors = $null; "
            + "[System.Management.Automation.Language.Parser]::ParseFile("
            + "$env:HASE_SCRIPT_TO_PARSE, [ref]$tokens, [ref]$errors) | Out-Null; "
            + "if (@($errors).Count -ne 0) { "
            + "$errors | ForEach-Object { [Console]::Error.WriteLine($_.Message) }; "
            + "exit 1 }; exit 0";
        (int code, _, string error) = await RunPowerShellAsync(
            ["-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(command))],
            ("HASE_SCRIPT_TO_PARSE", ToolPath(tool)));

        Assert.Equal(0, code);
        Assert.Equal(string.Empty, error.Trim());
    }

    [Fact]
    public void StartTool_GuardsComputerRepositoryProcessesAndJournals()
    {
        string script = File.ReadAllText(ToolPath(StartTool));

        Assert.Contains("$env:COMPUTERNAME -ceq $ExpectedComputer", script);
        Assert.Contains("status --porcelain", script);
        Assert.Contains("rev-parse origin/main", script);
        Assert.Contains("\"Hase.DesktopHost.App*\", \"Hase.Client.Wpf.App*\"", script);
        Assert.Contains("python-credential-reissue.transaction.json", script);
        Assert.Contains("python-credential-rotation.transaction.json", script);
        Assert.Contains("\"reissue-expired\"", script);
        Assert.Contains("$state.SourceState -eq \"Expired\"", script);
        Assert.Contains("if (-not $state.Ready) { throw \"not-ready\" }", script);
        Assert.Contains(TestTool, script);
    }

    [Fact]
    public void RecoverTool_TakesTheOriginalHashesFromTheJournal()
    {
        string script = File.ReadAllText(ToolPath(RecoverTool));

        Assert.Contains("$env:COMPUTERNAME -cne $ExpectedComputer", script);
        Assert.Contains("\"reissue-recover\"", script);
        Assert.Contains("OriginalSha256", script);
        Assert.Contains("AuthorizationPolicySha256", script);
        Assert.Contains("\"Hase.DesktopHost.App*\", \"Hase.Client.Wpf.App*\"", script);
    }

    [Fact]
    public async Task Preflight_OfAnExpiredCredential_ReportsItAndChangesNothing()
    {
        if (!OperatingSystem.IsWindows()) return;
        using Installation installation = CreateInstallation(
            now - TimeSpan.FromDays(40), TimeSpan.FromDays(30));
        string[] before = Snapshot();

        (int code, string output, _) = await RunToolAsync(StartTool,
            "-ExpectedComputer", Environment.MachineName,
            "-RuntimeHostConfigurationPath", installation.HostConfigurationPath,
            "-PythonProfilePath", installation.ProfilePath,
            // no root of this thumbprint exists, so the preflight is never ready
            "-SigningRootThumbprint", new string('0', 40),
            "-PreflightOnly");

        Assert.Equal(1, code);
        Assert.Contains("Source state                : Expired", output);
        Assert.Contains("Enrolled for the principal  : True", output);
        Assert.Contains("Grants                      : 3", output);
        Assert.Contains("Signing root                : not found", output);
        Assert.Contains("Ready for re-issue          : False", output);
        Assert.Contains(installation.Material.CredentialId[..24] + "...", output);
        Assert.DoesNotContain(installation.Material.CredentialId, output);
        Assert.DoesNotContain("PRIVATE KEY", output);
        Assert.Equal(before, Snapshot());
    }

    [Fact]
    public async Task Preflight_OfAValidCredential_IsNotReady()
    {
        if (!OperatingSystem.IsWindows()) return;
        using Installation installation = CreateInstallation(
            now - TimeSpan.FromDays(1), TimeSpan.FromDays(30));

        (int code, string output, _) = await RunToolAsync(StartTool,
            "-ExpectedComputer", Environment.MachineName,
            "-RuntimeHostConfigurationPath", installation.HostConfigurationPath,
            "-PythonProfilePath", installation.ProfilePath,
            "-SigningRootThumbprint", new string('0', 40),
            "-PreflightOnly");

        Assert.Equal(1, code);
        Assert.Contains("Source state                : Valid", output);
        Assert.Contains("Ready for re-issue          : False", output);
    }

    [Fact]
    public async Task Verifier_AcceptsACommittedReissue_AndRefusesAChangedPolicy()
    {
        if (!OperatingSystem.IsWindows()) return;
        using Installation installation = CreateInstallation(
            now - TimeSpan.FromDays(40), TimeSpan.FromDays(30));
        string replacementId = await ReissueAsync(installation);

        (int code, string output, _) = await RunToolAsync(TestTool,
            "-RuntimeHostConfigurationPath", installation.HostConfigurationPath,
            "-PythonProfilePath", installation.ProfilePath);

        Assert.Equal(0, code);
        Assert.Contains("Installed credential        : "
            + replacementId[..24] + "...", output);
        Assert.Contains("Re-issue journal            : committed, old files retained",
            output);
        Assert.Contains("Re-issue verified           : True", output);
        Assert.DoesNotContain("PRIVATE KEY", output);

        File.AppendAllText(installation.PolicyPath, " ");
        (int changed, _, _) = await RunToolAsync(TestTool,
            "-RuntimeHostConfigurationPath", installation.HostConfigurationPath,
            "-PythonProfilePath", installation.ProfilePath);

        Assert.Equal(1, changed);
    }

    [Fact]
    public async Task Verifier_RefusesAnExpiredInstallation()
    {
        if (!OperatingSystem.IsWindows()) return;
        using Installation installation = CreateInstallation(
            now - TimeSpan.FromDays(40), TimeSpan.FromDays(30));

        (int code, _, _) = await RunToolAsync(TestTool,
            "-RuntimeHostConfigurationPath", installation.HostConfigurationPath,
            "-PythonProfilePath", installation.ProfilePath);

        Assert.Equal(1, code);
    }

    private async Task<string> ReissueAsync(Installation installation)
    {
        var inspection = new PythonCredentialLifecycleInspectionRequest(
            installation.ProfilePath, installation.EnrollmentPath,
            installation.PolicyPath, Principal, TrustPolicy, Grants);
        PythonCredentialLifecycleInspectionResult inspected =
            await new PythonCredentialLifecycleInspector().InspectAsync(
                inspection, now);
        using PythonClientCredentialMaterial replacement =
            PythonClientCredentialFactory.Create(
                installation.Root, now, TimeSpan.FromDays(90));
        _ = await new PythonCredentialRotationOrchestrator().ReissueAsync(
            new(inspection, installation.Material.CredentialId,
                inspected.ProfileSha256, inspected.EnrollmentSha256,
                inspected.AuthorizationPolicySha256,
                inspected.TrustedServerCertificateSha256),
            new(installation.SecurityDirectory, installation.CertificatePath,
                installation.PrivateKeyPath, installation.ProfilePath,
                installation.EnrollmentPath, installation.PolicyPath,
                inspected.ClientCertificateSha256,
                inspected.ClientPrivateKeySha256, inspected.ProfileSha256,
                inspected.EnrollmentSha256,
                inspected.AuthorizationPolicySha256),
            replacement, now);
        return replacement.CredentialId;
    }

    private Installation CreateInstallation(DateTimeOffset issued, TimeSpan validity)
    {
        X509Certificate2 root = CreateRoot();
        PythonClientCredentialMaterial material =
            PythonClientCredentialFactory.Create(root, issued, validity);
        string security = Path.Combine(directory, "PythonAutomation", "Security");
        string configuration = Path.Combine(directory, "RuntimeHost", "Configuration");
        string validation = Path.Combine(directory, "PrivateNetworkValidation");
        Directory.CreateDirectory(security);
        Directory.CreateDirectory(configuration);
        Directory.CreateDirectory(validation);

        string certificate = Path.Combine(security, "python-client-chain.pem");
        string privateKey = Path.Combine(security, "python-client-key.pem");
        string profile = Path.Combine(security, "runtime-host-profile.json");
        string trustedServer = Path.Combine(validation, "runtime-host-server.cer");
        string enrollment = Path.Combine(validation, "client-enrollments.json");
        string policy = Path.Combine(configuration, "runtime-host-authorization.json");
        string network = Path.Combine(configuration, "desktop-private-network.json");
        string host = Path.Combine(configuration, "desktop-runtime-host.json");

        File.WriteAllBytes(certificate, material.CertificatePem.ToArray());
        File.WriteAllBytes(privateKey, material.PrivateKeyPem.ToArray());
        File.WriteAllBytes(trustedServer, root.RawData);
        File.WriteAllText(profile, JsonSerializer.Serialize(new
        {
            formatVersion = 1,
            address = "https://192.0.2.20:50443",
            clientCertificate = new
            {
                certificateChainPath = certificate,
                privateKeyPath = privateKey,
            },
            trustedServerCertificate = new { certificatePath = trustedServer },
        }));
        File.WriteAllText(enrollment, JsonSerializer.Serialize(new
        {
            formatVersion = 1,
            enrollments = new[]
            {
                new
                {
                    credentialId = "x509-sha256:" + new string('9', 64),
                    principalId = "laptop-validation-client",
                    trustPolicyId = TrustPolicy,
                },
                new
                {
                    credentialId = material.CredentialId,
                    principalId = Principal,
                    trustPolicyId = TrustPolicy,
                },
            },
        }));
        File.WriteAllText(policy, JsonSerializer.Serialize(new
        {
            formatVersion = 1,
            grants = Grants.Select(permission => new
            {
                principalId = Principal,
                permission,
            }),
        }));
        File.WriteAllText(network, JsonSerializer.Serialize(new
        {
            formatVersion = 1,
            clientEnrollmentFilePath = enrollment,
        }));
        File.WriteAllText(host, JsonSerializer.Serialize(new
        {
            authorizationPolicyFilePath = policy,
            privateNetworkConfigurationFilePath = network,
        }));
        return new Installation(root, material, host, profile, security,
            certificate, privateKey, enrollment, policy);
    }

    private X509Certificate2 CreateRoot()
    {
        using RSA key = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=HASE Python Reissue Tool Test Root " + Guid.NewGuid().ToString("N"),
            key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        return request.CreateSelfSigned(now.AddYears(-1), now.AddYears(1));
    }

    private string[] Snapshot() =>
        Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(path => path + ":" + Convert.ToHexStringLower(
                SHA256.HashData(File.ReadAllBytes(path))))
            .ToArray();

    private static Task<(int Code, string Output, string Error)> RunToolAsync(
        string tool, params string[] arguments) =>
        RunPowerShellAsync(
            ["-ExecutionPolicy", "Bypass", "-File", ToolPath(tool), .. arguments]);

    private static async Task<(int Code, string Output, string Error)> RunPowerShellAsync(
        string[] arguments,
        params (string Name, string Value)[] environment)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32", "WindowsPowerShell", "v1.0", "powershell.exe"),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        foreach (string argument in arguments) startInfo.ArgumentList.Add(argument);
        foreach ((string name, string value) in environment)
            startInfo.Environment[name] = value;

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Windows PowerShell did not start.");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await output, await error);
    }

    private static string ToolPath(string fileName)
    {
        DirectoryInfo? current = new(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            string path = Path.Combine(current.FullName, "python",
                "hase-client", "tools", fileName);
            if (File.Exists(path)) return path;
            current = current.Parent;
        }
        throw new InvalidOperationException("Repository root was not found.");
    }

    private static DateTimeOffset WholeSeconds(DateTimeOffset value) =>
        new(value.Ticks - value.Ticks % TimeSpan.TicksPerSecond, TimeSpan.Zero);

    private sealed record Installation(
        X509Certificate2 Root,
        PythonClientCredentialMaterial Material,
        string HostConfigurationPath,
        string ProfilePath,
        string SecurityDirectory,
        string CertificatePath,
        string PrivateKeyPath,
        string EnrollmentPath,
        string PolicyPath) : IDisposable
    {
        public void Dispose()
        {
            Material.Dispose();
            Root.Dispose();
        }
    }
}
