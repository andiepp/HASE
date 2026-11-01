using System.Diagnostics;
using System.Net;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hase.Runtime.Remote.Grpc.Adapter;
using Microsoft.Win32;

namespace Hase.Runtime.Remote.Grpc.Hosting.Tests;

/// <summary>
/// The ADR-0086 renewal tools, run by Windows PowerShell against a temporary
/// deployment whose certificates live in two temporary, separately named
/// stores of the current user; the stores, their keys and the files are
/// removed afterwards. The results are read back with the host's and the
/// client's own parsers and validators.
/// </summary>
public sealed class PrivateNetworkRenewalToolTests : IDisposable
{
    private const string StartTool = "Start-HasePrivateNetworkValidationRenewal.ps1";
    private const string TestTool = "Test-HasePrivateNetworkValidationRenewal.ps1";
    private const string InstallTool = "Install-HasePrivateNetworkValidationRenewal.ps1";
    private const string RestoreTool = "Restore-HasePrivateNetworkValidationRenewal.ps1";
    private const string CommonFile = "HasePrivateNetworkRenewal.Common.ps1";
    private const string LaptopPrincipal = "laptop-validation-client";
    private const string PythonPrincipal = "hase-python-automation";
    private const string TrustPolicy = "private-network-validation-v1";
    private const string TransferPassword = "renewal-test-transfer";
    private const int Port = 5443;

    private static readonly IPAddress Address = IPAddress.Parse("192.0.2.10");

    private readonly DateTimeOffset now = DateTimeOffset.UtcNow;
    private readonly string directory = Path.Combine(
        Path.GetTempPath(), $"hase-private-network-renewal-{Guid.NewGuid():N}");
    private readonly string storeName = $"HaseRenewalTest{Guid.NewGuid():N}";
    private readonly string trustedStoreName = $"HaseRenewalTrust{Guid.NewGuid():N}";

    public PrivateNetworkRenewalToolTests()
    {
        Directory.CreateDirectory(directory);
        // The arguments go to a tool as a splat, read from JSON; the
        // transfer password is converted to a SecureString here.
        File.WriteAllText(WrapperPath, string.Join("\r\n",
            "$ErrorActionPreference = \"Stop\"",
            "$splat = @{}",
            "$arguments = $env:HASE_TOOL_ARGUMENTS | ConvertFrom-Json",
            "foreach ($property in $arguments.PSObject.Properties) { $splat[$property.Name] = $property.Value }",
            "if ($env:HASE_TOOL_PASSWORD) { $splat[\"TransferPassword\"] = ConvertTo-SecureString $env:HASE_TOOL_PASSWORD -AsPlainText -Force }",
            "& $env:HASE_TOOL_PATH @splat",
            "exit $LASTEXITCODE",
            ""));
    }

    private string WrapperPath => Path.Combine(directory, "invoke-tool.ps1");

    public void Dispose()
    {
        if (OperatingSystem.IsWindows())
        {
            RemoveStore(storeName);
            RemoveStore(trustedStoreName);
        }
        foreach (string file in Directory.EnumerateFiles(
                     directory, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }
        Directory.Delete(directory, recursive: true);
    }

    public enum Spoil
    {
        None,
        ForeignPin,
        RootExpiresSoon,
        PrincipalMissing,
        OutputExists,
        LaptopAddressDiffers,
    }

    [Theory]
    [InlineData(StartTool)]
    [InlineData(TestTool)]
    [InlineData(InstallTool)]
    [InlineData(RestoreTool)]
    [InlineData(CommonFile)]
    [InlineData("New-HasePrivateNetworkValidationBundle.ps1")]
    [InlineData("Install-HasePrivateNetworkValidationClient.ps1")]
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
    public void MutatingTools_GuardTheComputerAndTheProcesses()
    {
        string common = File.ReadAllText(ToolPath(CommonFile));
        Assert.Contains("$env:COMPUTERNAME -ceq $ExpectedComputer", common);
        Assert.Contains("@(\"Hase.DesktopHost.App*\", \"Hase.Client.Wpf.App*\")", common);

        foreach (string tool in new[] { StartTool, InstallTool, RestoreTool })
        {
            string script = File.ReadAllText(ToolPath(tool));
            Assert.Contains("Assert-HaseRenewalComputer $ExpectedComputer", script);
            Assert.Contains("Get-HaseRenewalRunningProcessNames", script);
        }
    }

    [Fact]
    public async Task Preflight_OfAnExpiredDeployment_IsReady_AndChangesNothing()
    {
        if (!OperatingSystem.IsWindows()) return;
        using Deployment deployment = CreateDeployment();
        string[] files = Snapshot();
        string[] certificates = StoreSnapshot(storeName);

        (int code, string output, string error) = await RunToolAsync(
            StartTool, DesktopArguments(deployment, preflightOnly: true));

        Assert.True(code == 0, output + error);
        Assert.Contains(Line("Server certificate", "Expired, "), output);
        Assert.Contains(Line("Root trusted", "True"), output);
        Assert.Contains(Line("Principal enrolled once", "True"), output);
        Assert.Contains(Line("Pins match the server", "True"), output);
        Assert.Contains(Line("Ready for renewal", "True"), output);
        AssertWithholds(output, deployment.Root, deployment.OldServer, deployment.OldClient);
        Assert.Equal(files, Snapshot());
        Assert.Equal(certificates, StoreSnapshot(storeName));
    }

    [Theory]
    [InlineData(Spoil.ForeignPin, "pin-foreign")]
    [InlineData(Spoil.RootExpiresSoon, "root-does-not-cover-validity")]
    [InlineData(Spoil.PrincipalMissing, "principal-not-enrolled-once")]
    [InlineData(Spoil.OutputExists, "output-directory-exists")]
    [InlineData(Spoil.LaptopAddressDiffers, "laptop-address-mismatch")]
    public async Task Preflight_RefusesWhatARenewalCannotCarry(Spoil spoil, string refusal)
    {
        if (!OperatingSystem.IsWindows()) return;
        using Deployment deployment = CreateDeployment(spoil);
        string[] files = Snapshot();

        (int code, string output, _) = await RunToolAsync(
            StartTool, DesktopArguments(deployment, preflightOnly: true));

        Assert.Equal(1, code);
        Assert.Contains(Line("Refused", refusal), output);
        Assert.Contains(Line("Ready for renewal", "False"), output);
        Assert.Equal(files, Snapshot());
    }

    [Fact]
    public async Task Renewal_OnAnotherComputer_IsRefused()
    {
        if (!OperatingSystem.IsWindows()) return;
        using Deployment deployment = CreateDeployment();
        Dictionary<string, object> arguments = DesktopArguments(deployment, preflightOnly: true);
        arguments["ExpectedComputer"] = "NOT-" + Environment.MachineName;

        (int code, _, string error) = await RunToolAsync(StartTool, arguments);

        Assert.NotEqual(0, code);
        Assert.Contains("computer-mismatch", error);
    }

    [Fact]
    public async Task Renewal_PublishesADeploymentTheHostAndTheClientAccept()
    {
        if (!OperatingSystem.IsWindows()) return;
        using Deployment deployment = CreateDeployment();
        Dictionary<string, byte[]> originals = deployment.Targets.ToDictionary(
            path => path, File.ReadAllBytes);

        (int code, string output, string error) = await RunToolAsync(
            StartTool, DesktopArguments(deployment), withPassword: true);

        Assert.True(code == 0, output + error);
        Assert.Contains(Line("Renewal published", "True"), output);
        Assert.Contains(Line("Renewal verified", "True"), output);

        string laptopSet = Path.Combine(deployment.OutputDirectory, "Laptop");
        using X509Certificate2 server = X509CertificateLoader.LoadCertificateFromFile(
            Path.Combine(laptopSet, "runtime-host-server.cer"));
        using X509Certificate2 client = X509CertificateLoader.LoadPkcs12FromFile(
            Path.Combine(laptopSet, "laptop-client.pfx"), TransferPassword);
        AssertWithholds(output, deployment.Root, deployment.OldServer, server, client);
        Assert.DoesNotContain(CredentialId(client)["x509-sha256:".Length..], output);

        // The host side, read by the host's own parsers and validator.
        foreach (string path in deployment.DesktopConfigurations)
        {
            RuntimeHostPrivateNetworkDeploymentOptions options =
                await RuntimeHostPrivateNetworkDeploymentOptionsFile.LoadAsync(path);
            Assert.Equal(server.Thumbprint, options.ServerCertificate.Thumbprint);
            Assert.Equal(Address, options.Binding.Address);
        }
        using X509Certificate2 installed = Assert.Single(FindInStore(storeName, server.Thumbprint));
        Assert.True(installed.HasPrivateKey);
        RuntimeHostPrivateNetworkServerCertificateValidator.Validate(
            installed, new PrivateNetworkGrpcBinding(Address, Port), DateTimeOffset.UtcNow);
        Assert.InRange(installed.NotAfter.ToUniversalTime(),
            now.AddDays(90).AddMinutes(-5).UtcDateTime, now.AddDays(90).AddMinutes(5).UtcDateTime);
        Assert.Equal(deployment.Root.Subject, server.Issuer);

        RuntimeHostClientCredentialEnrollmentRegistry registry =
            await RuntimeHostClientCredentialEnrollmentRegistryFile.LoadAsync(deployment.EnrollmentPath);
        Assert.Equal(LaptopPrincipal, Resolve(registry, client));
        Assert.Equal(PythonPrincipal, Resolve(registry, deployment.PythonClient));
        Assert.Null(Resolve(registry, deployment.OldClient));

        Assert.Equal(server.RawData, File.ReadAllBytes(deployment.PinPath));

        // The Laptop set, read by the client's own parser.
        RuntimeHostPrivateNetworkClientOptions laptop =
            await RuntimeHostPrivateNetworkClientOptionsFile.LoadAsync(
                Path.Combine(laptopSet, "laptop-private-network.json"));
        Assert.Equal(client.Thumbprint, laptop.ClientCertificate.Thumbprint);
        Assert.Equal(server.Thumbprint, laptop.TrustedServerCertificate.Thumbprint);
        Assert.Equal(new Uri($"https://{Address}:{Port}"), laptop.Address);

        // The originals are retained beside every target.
        string journalPath = Path.Combine(deployment.OutputDirectory, "private-network-renewal.transaction.json");
        JsonNode journal = JsonNode.Parse(File.ReadAllText(journalPath))!;
        Assert.Equal("published", (string?)journal["phase"]);
        foreach (JsonNode? target in journal["targets"]!.AsArray())
        {
            string path = (string)target!["path"]!;
            Assert.Equal(originals[path], File.ReadAllBytes((string)target["backupPath"]!));
        }
    }

    [Fact]
    public async Task Renewal_ThatFailsWhilePublishing_RollsEverythingBack()
    {
        if (!OperatingSystem.IsWindows()) return;
        using Deployment deployment = CreateDeployment();
        Dictionary<string, byte[]> originals = deployment.Targets.ToDictionary(
            path => path, File.ReadAllBytes);
        string[] certificates = StoreSnapshot(storeName);
        // The pin is the last target: the configurations and the registry
        // are already replaced when its replacement fails.
        File.SetAttributes(deployment.PinPath, FileAttributes.ReadOnly);

        (int code, _, _) = await RunToolAsync(
            StartTool, DesktopArguments(deployment), withPassword: true);

        Assert.NotEqual(0, code);
        foreach ((string path, byte[] bytes) in originals)
        {
            Assert.Equal(bytes, File.ReadAllBytes(path));
        }
        Assert.Equal(certificates, StoreSnapshot(storeName));
        JsonNode journal = JsonNode.Parse(File.ReadAllText(Path.Combine(
            deployment.OutputDirectory, "private-network-renewal.transaction.json")))!;
        Assert.Equal("rolled-back", (string?)journal["phase"]);
    }

    [Fact]
    public async Task Restore_RollsAPublishedRenewalBack()
    {
        if (!OperatingSystem.IsWindows()) return;
        using Deployment deployment = CreateDeployment();
        Dictionary<string, byte[]> originals = deployment.Targets.ToDictionary(
            path => path, File.ReadAllBytes);
        string[] certificates = StoreSnapshot(storeName);
        (int renewed, string renewal, string renewalError) = await RunToolAsync(
            StartTool, DesktopArguments(deployment), withPassword: true);
        Assert.True(renewed == 0, renewal + renewalError);

        (int code, string output, string error) = await RunToolAsync(RestoreTool, new()
        {
            ["ExpectedComputer"] = Environment.MachineName,
            ["JournalPath"] = Path.Combine(deployment.OutputDirectory, "private-network-renewal.transaction.json"),
            ["Mode"] = "Rollback",
        });

        Assert.True(code == 0, output + error);
        Assert.Contains(Line("Rolled back", "True"), output);
        foreach ((string path, byte[] bytes) in originals)
        {
            Assert.Equal(bytes, File.ReadAllBytes(path));
        }
        Assert.Equal(certificates, StoreSnapshot(storeName));
    }

    [Fact]
    public async Task Restore_CleansUpAPublishedRenewal_AndRefusesAChangedTarget()
    {
        if (!OperatingSystem.IsWindows()) return;
        using Deployment deployment = CreateDeployment();
        (int renewed, string renewal, string renewalError) = await RunToolAsync(
            StartTool, DesktopArguments(deployment), withPassword: true);
        Assert.True(renewed == 0, renewal + renewalError);
        string journalPath = Path.Combine(deployment.OutputDirectory, "private-network-renewal.transaction.json");
        Dictionary<string, object> cleanup = new()
        {
            ["ExpectedComputer"] = Environment.MachineName,
            ["JournalPath"] = journalPath,
            ["Mode"] = "Cleanup",
        };

        byte[] published = File.ReadAllBytes(deployment.PinPath);
        File.WriteAllBytes(deployment.PinPath, [1, 2, 3]);
        (int refused, _, string refusal) = await RunToolAsync(RestoreTool, cleanup);
        Assert.NotEqual(0, refused);
        Assert.Contains("target-changed-outside-renewal", refusal);
        File.WriteAllBytes(deployment.PinPath, published);

        (int code, string output, string error) = await RunToolAsync(RestoreTool, cleanup);

        Assert.True(code == 0, output + error);
        Assert.Contains(Line("Cleaned", "True"), output);
        Assert.Empty(Directory.EnumerateFiles(directory, "*.backup", SearchOption.AllDirectories));
        Assert.False(File.Exists(Path.Combine(deployment.OutputDirectory, "Laptop", "laptop-client.pfx")));

        (int verified, string verifier, _) = await RunToolAsync(TestTool, new()
        {
            ["DesktopConfigurationPath"] = deployment.DesktopConfigurations,
            ["PinnedServerCertificatePath"] = new[] { deployment.PinPath },
            ["JournalPath"] = journalPath,
            ["CertificateStoreName"] = storeName,
        });
        Assert.True(verified == 0, verifier);
        Assert.Contains(Line("Journal phase", "cleaned"), verifier);
    }

    [Fact]
    public async Task Verifier_RejectsTheExpiredDeployment()
    {
        if (!OperatingSystem.IsWindows()) return;
        using Deployment deployment = CreateDeployment();

        (int code, string output, _) = await RunToolAsync(TestTool, new()
        {
            ["DesktopConfigurationPath"] = deployment.DesktopConfigurations,
            ["PinnedServerCertificatePath"] = new[] { deployment.PinPath },
            ["CertificateStoreName"] = storeName,
        });

        Assert.Equal(1, code);
        Assert.Contains(Line("Rejected", "server-certificate-not-current"), output);
        Assert.Contains(Line("Renewal verified", "False"), output);
    }

    [Fact]
    public async Task LaptopInstallation_PointsTheClientAtTheRenewedCertificates()
    {
        if (!OperatingSystem.IsWindows()) return;
        using Deployment deployment = CreateDeployment();
        (int renewed, string renewal, string renewalError) = await RunToolAsync(
            StartTool, DesktopArguments(deployment), withPassword: true);
        Assert.True(renewed == 0, renewal + renewalError);
        string laptopSet = Path.Combine(deployment.OutputDirectory, "Laptop");

        // The Laptop as it is before the renewal: its configuration names the
        // expired client and server certificates, its Python profile pins the
        // expired server certificate.
        string laptopDirectory = Path.Combine(directory, "Laptop");
        Directory.CreateDirectory(laptopDirectory);
        string configurationPath = Path.Combine(laptopDirectory, "laptop-private-network.json");
        File.Copy(deployment.LaptopConfigurationPath, configurationPath);
        string pinPath = Path.Combine(laptopDirectory, "runtime-host-server.cer");
        File.WriteAllBytes(pinPath, deployment.OldServer.RawData);
        AddWithKey(deployment.OldClient, storeName);
        Dictionary<string, object> arguments = new()
        {
            ["ExpectedComputer"] = Environment.MachineName,
            ["BundleDirectory"] = laptopSet,
            ["ClientConfigurationPath"] = configurationPath,
            ["PinnedServerCertificatePath"] = new[] { pinPath },
            ["CustodyDirectory"] = Path.Combine(laptopDirectory, "Custody"),
            ["CertificateStoreName"] = storeName,
            ["PreflightOnly"] = true,
        };

        (int notImported, string beforeImport, _) = await RunToolAsync(InstallTool, arguments);
        Assert.Equal(1, notImported);
        Assert.Contains(Line("Refused", "client-certificate-not-imported"), beforeImport);

        // What Install-HasePrivateNetworkValidationClient.ps1 imports.
        using X509Certificate2 client = X509CertificateLoader.LoadPkcs12FromFile(
            Path.Combine(laptopSet, "laptop-client.pfx"), TransferPassword,
            X509KeyStorageFlags.Exportable);
        AddWithKey(client, storeName);

        (int ready, string preflight, _) = await RunToolAsync(InstallTool, arguments);
        Assert.True(ready == 0, preflight);
        arguments.Remove("PreflightOnly");
        (int code, string output, string error) = await RunToolAsync(InstallTool, arguments);

        Assert.True(code == 0, output + error);
        Assert.Contains(Line("Installation verified", "True"), output);
        RuntimeHostPrivateNetworkClientOptions options =
            await RuntimeHostPrivateNetworkClientOptionsFile.LoadAsync(configurationPath);
        using X509Certificate2 server = X509CertificateLoader.LoadCertificateFromFile(
            Path.Combine(laptopSet, "runtime-host-server.cer"));
        Assert.Equal(client.Thumbprint, options.ClientCertificate.Thumbprint);
        Assert.Equal(server.Thumbprint, options.TrustedServerCertificate.Thumbprint);
        Assert.Equal(server.RawData, File.ReadAllBytes(pinPath));
        AssertWithholds(output, deployment.OldServer, deployment.OldClient, server, client);
    }

    private Deployment CreateDeployment(Spoil spoil = Spoil.None)
    {
        X509Certificate2 root = CreateRoot(
            spoil == Spoil.RootExpiresSoon ? now.AddDays(30) : now.AddYears(2));
        X509Certificate2 oldServer = Issue(root, "CN=HASE Private Network Validation Server",
            "1.3.6.1.5.5.7.3.1", Address, now.AddDays(-100), now.AddDays(-8));
        X509Certificate2 oldClient = Issue(root, "CN=HASE Private Network Validation Client",
            "1.3.6.1.5.5.7.3.2", null, now.AddDays(-100), now.AddDays(-8));
        X509Certificate2 pythonClient = Issue(root, "CN=HASE Python Automation Client",
            "1.3.6.1.5.5.7.3.2", null, now.AddDays(-16), now.AddDays(20));
        if (OperatingSystem.IsWindows())
        {
            AddWithKey(root, storeName);
            AddWithKey(oldServer, storeName);
            AddPublic(root, trustedStoreName);
        }

        string desktopDirectory = Path.Combine(directory, "Desktop");
        Directory.CreateDirectory(desktopDirectory);
        string enrollmentPath = Path.Combine(desktopDirectory, "client-enrollments.json");
        var enrollments = new List<object>();
        if (spoil != Spoil.PrincipalMissing)
        {
            enrollments.Add(new { credentialId = CredentialId(oldClient), principalId = LaptopPrincipal, trustPolicyId = TrustPolicy });
        }
        enrollments.Add(new { credentialId = CredentialId(pythonClient), principalId = PythonPrincipal, trustPolicyId = TrustPolicy });
        WriteJson(enrollmentPath, new { formatVersion = 1, enrollments });

        object desktop = new
        {
            formatVersion = 1,
            binding = new { address = Address.ToString(), port = Port },
            serverCertificate = new { storeName = "My", storeLocation = "CurrentUser", thumbprint = oldServer.Thumbprint },
            clientEnrollmentFilePath = enrollmentPath,
        };
        string[] desktopConfigurations =
        [
            Path.Combine(desktopDirectory, "Installed", "desktop-private-network.json"),
            Path.Combine(desktopDirectory, "desktop-private-network.json"),
        ];
        foreach (string path in desktopConfigurations)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            WriteJson(path, desktop);
        }

        string laptopAddress = spoil == Spoil.LaptopAddressDiffers
            ? $"https://192.0.2.11:{Port}"
            : $"https://{Address}:{Port}";
        string laptopConfigurationPath = Path.Combine(desktopDirectory, "laptop-private-network.json");
        WriteJson(laptopConfigurationPath, new
        {
            formatVersion = 1,
            address = laptopAddress,
            clientCertificate = new { storeName = "My", storeLocation = "CurrentUser", thumbprint = oldClient.Thumbprint },
            trustedServerCertificate = new { storeName = "TrustedPeople", storeLocation = "CurrentUser", thumbprint = oldServer.Thumbprint },
        });

        string pinPath = Path.Combine(desktopDirectory, "runtime-host-server.cer");
        File.WriteAllBytes(pinPath, spoil == Spoil.ForeignPin ? pythonClient.RawData : oldServer.RawData);

        string outputDirectory = Path.Combine(directory, "Renewal");
        if (spoil == Spoil.OutputExists)
        {
            Directory.CreateDirectory(outputDirectory);
        }

        return new Deployment(root, oldServer, oldClient, pythonClient,
            desktopConfigurations, enrollmentPath, laptopConfigurationPath, pinPath, outputDirectory);
    }

    private Dictionary<string, object> DesktopArguments(Deployment deployment, bool preflightOnly = false)
    {
        Dictionary<string, object> arguments = new()
        {
            ["ExpectedComputer"] = Environment.MachineName,
            ["DesktopConfigurationPath"] = deployment.DesktopConfigurations,
            ["LaptopConfigurationPath"] = deployment.LaptopConfigurationPath,
            ["PinnedServerCertificatePath"] = new[] { deployment.PinPath },
            ["OutputDirectory"] = deployment.OutputDirectory,
            ["CertificateStoreName"] = storeName,
            ["TrustedRootStoreName"] = trustedStoreName,
        };
        if (preflightOnly)
        {
            arguments["PreflightOnly"] = true;
        }
        return arguments;
    }

    private X509Certificate2 CreateRoot(DateTimeOffset notAfter)
    {
        using RSA key = RSA.Create(3072);
        var request = new CertificateRequest(
            "CN=HASE Private Network Validation Root " + Guid.NewGuid().ToString("N"),
            key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        return request.CreateSelfSigned(now.AddDays(-200), notAfter);
    }

    private static X509Certificate2 Issue(
        X509Certificate2 root, string subject, string usage, IPAddress? address,
        DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        using RSA key = RSA.Create(2048);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid(usage)], true));
        if (address is not null)
        {
            var names = new SubjectAlternativeNameBuilder();
            names.AddIpAddress(address);
            request.CertificateExtensions.Add(names.Build());
        }
        using X509Certificate2 issued = request.Create(
            root, notBefore, notAfter, RandomNumberGenerator.GetBytes(16).Select((b, i) => i == 0 ? (byte)(b & 0x7F) : b).ToArray());
        return issued.CopyWithPrivateKey(key);
    }

    private static string CredentialId(X509Certificate2 certificate) =>
        "x509-sha256:" + Convert.ToHexStringLower(SHA256.HashData(certificate.RawData));

    private static string? Resolve(
        RuntimeHostClientCredentialEnrollmentRegistry registry, X509Certificate2 certificate)
    {
        RuntimeHostClientCredentialIdentity identity =
            new RuntimeHostX509ClientCredentialIdentityExtractor().Extract(certificate);
        return registry.TryResolve(identity, DateTimeOffset.UtcNow, out RuntimeHostClientPrincipal? principal)
            ? principal!.PrincipalId
            : null;
    }

    private static void AssertWithholds(string output, params X509Certificate2[] certificates)
    {
        foreach (X509Certificate2 certificate in certificates)
        {
            Assert.DoesNotContain(certificate.Thumbprint, output, StringComparison.OrdinalIgnoreCase);
        }
        Assert.DoesNotContain(Address.ToString(), output);
        Assert.DoesNotContain("PRIVATE KEY", output);
    }

    private static string Line(string label, string value) =>
        string.Format("{0,-32}: {1}", label, value);

    private static void WriteJson(string path, object document) =>
        File.WriteAllText(path, JsonSerializer.Serialize(document));

    private string[] Snapshot() =>
        Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(path => path + ":" + Convert.ToHexStringLower(
                SHA256.HashData(File.ReadAllBytes(path))))
            .ToArray();

    private static X509Certificate2[] FindInStore(string name, string thumbprint)
    {
        using X509Store store = new(name, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);
        return [.. store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false)];
    }

    private static string[] StoreSnapshot(string name)
    {
        using X509Store store = new(name, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);
        return store.Certificates.Select(c => c.Thumbprint).Order(StringComparer.Ordinal).ToArray();
    }

    private static void AddWithKey(X509Certificate2 certificate, string name)
    {
        byte[] pkcs12 = certificate.Export(X509ContentType.Pkcs12, "fixture");
        using X509Certificate2 persisted = X509CertificateLoader.LoadPkcs12(
            pkcs12, "fixture", X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.UserKeySet);
        using X509Store store = new(name, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadWrite);
        store.Add(persisted);
    }

    private static void AddPublic(X509Certificate2 certificate, string name)
    {
        using X509Certificate2 publicOnly = X509CertificateLoader.LoadCertificate(certificate.RawData);
        using X509Store store = new(name, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadWrite);
        store.Add(publicOnly);
    }

    [SupportedOSPlatform("windows")]
    private static void RemoveStore(string name)
    {
        using (X509Store store = new(name, StoreLocation.CurrentUser))
        {
            store.Open(OpenFlags.ReadWrite);
            foreach (X509Certificate2 certificate in store.Certificates)
            {
                if (certificate.HasPrivateKey)
                {
                    using RSA? key = certificate.GetRSAPrivateKey();
                    if (key is RSACng cng)
                    {
                        cng.Key.Delete();
                    }
                    else if (key is RSACryptoServiceProvider legacy)
                    {
                        legacy.PersistKeyInCsp = false;
                        legacy.Clear();
                    }
                }
                store.Remove(certificate);
            }
        }
        Registry.CurrentUser.DeleteSubKeyTree(
            @"Software\Microsoft\SystemCertificates\" + name, throwOnMissingSubKey: false);
    }

    private async Task<(int Code, string Output, string Error)> RunToolAsync(
        string tool, Dictionary<string, object> arguments, bool withPassword = false)
    {
        return await RunPowerShellAsync(
            ["-ExecutionPolicy", "Bypass", "-File", WrapperPath],
            ("HASE_TOOL_PATH", ToolPath(tool)),
            ("HASE_TOOL_ARGUMENTS", JsonSerializer.Serialize(arguments)),
            ("HASE_TOOL_PASSWORD", withPassword ? TransferPassword : string.Empty));
    }

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
            string path = Path.Combine(current.FullName, "tools", "PrivateNetwork", fileName);
            if (File.Exists(path)) return path;
            current = current.Parent;
        }
        throw new InvalidOperationException("Repository root was not found.");
    }

    private sealed record Deployment(
        X509Certificate2 Root,
        X509Certificate2 OldServer,
        X509Certificate2 OldClient,
        X509Certificate2 PythonClient,
        string[] DesktopConfigurations,
        string EnrollmentPath,
        string LaptopConfigurationPath,
        string PinPath,
        string OutputDirectory) : IDisposable
    {
        public IEnumerable<string> Targets =>
            [.. DesktopConfigurations, EnrollmentPath, PinPath];

        public void Dispose()
        {
            Root.Dispose();
            OldServer.Dispose();
            OldClient.Dispose();
            PythonClient.Dispose();
        }
    }
}
