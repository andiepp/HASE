using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Security.Cryptography.X509Certificates;
using Hase.DesktopHost.Configuration;
using Hase.Runtime.Remote.Grpc.Adapter;
using Hase.Runtime.Remote.Grpc.Hosting;
using Hase.Transport.Discovery;

if (args.Length == 3
    && string.Equals(args[0], "validate-provisioning", StringComparison.Ordinal))
{
    return await ValidateProvisioningAsync(args[1], args[2]);
}

if (args.Length == 3
    && string.Equals(args[0], "validate-client-provisioning", StringComparison.Ordinal))
{
    return await ValidateClientProvisioningAsync(args[1], args[2]);
}

if (args.Length != 4)
{
    Console.Error.WriteLine("Usage: Hase.DesktopHost.Preflight <repository-root> <private-network-config> <0xVID> <0xPID>");
    return 2;
}

string repositoryRoot = Path.GetFullPath(args[0]);
string configurationPath = Path.GetFullPath(args[1]);
ushort vendorId;
ushort productId;
try
{
    vendorId = CompactSerialUsbIdentifierParser.ParseExactHex16(args[2], "USB vendor ID");
    productId = CompactSerialUsbIdentifierParser.ParseExactHex16(args[3], "USB product ID");
}
catch
{
    Console.Error.WriteLine("The USB identifiers are invalid.");
    return 2;
}

bool windowsReady = OperatingSystem.IsWindows();
bool sdkReady = await HasDotNet10SdkAsync();
bool repositoryReady = File.Exists(Path.Combine(repositoryRoot, "HASE.slnx"))
    && File.Exists(Path.Combine(repositoryRoot, "src", "Hase.DesktopHost.App", "Hase.DesktopHost.App.csproj"));
string installationDirectory = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "HASE", "RuntimeHost");
bool installationClean = !Directory.Exists(installationDirectory);
string shortcutPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
    "HASE Runtime Host.lnk");
bool shortcutClean = !File.Exists(shortcutPath);

RuntimeHostPrivateNetworkDeploymentOptions? options = null;
bool configurationReady;
try
{
    options = await RuntimeHostPrivateNetworkDeploymentOptionsFile.LoadAsync(configurationPath);
    configurationReady = true;
}
catch { configurationReady = false; }

bool enrollmentReady = false;
bool certificateReady = false;
bool addressOwned = false;
if (options is not null)
{
    try
    {
        _ = await RuntimeHostClientCredentialEnrollmentRegistryFile.LoadAsync(
            options.ClientEnrollmentFilePath);
        enrollmentReady = true;
    }
    catch { }

    try
    {
        using X509Certificate2 certificate = RuntimeHostCertificateStoreLoader.Load(
            options.ServerCertificate, requirePrivateKey: true);
        certificateReady = certificate.HasPrivateKey;
    }
    catch { }

    addressOwned = NetworkInterface.GetAllNetworkInterfaces()
        .SelectMany(network => network.GetIPProperties().UnicastAddresses)
        .Any(address => address.Address.Equals(options.Binding.Address));
}

bool arduinoPresent = false;
if (windowsReady)
{
    try
    {
        await foreach (UsbSerialEndpointCandidate candidate in
            new WindowsUsbSerialEndpointCandidateSource().EnumerateAsync())
        {
            if (candidate.VendorId == vendorId && candidate.ProductId == productId)
            {
                arduinoPresent = true;
                break;
            }
        }
    }
    catch { }
}

var assessment = new SecondPcRuntimeHostPreflightAssessment(
    windowsReady, sdkReady, repositoryReady, installationClean, shortcutClean,
    configurationReady, enrollmentReady, certificateReady, addressOwned,
    arduinoPresent);

Console.WriteLine("HASE second-PC Runtime Host preflight");
foreach ((string name, bool ready) in assessment.Readiness)
    Console.WriteLine($"{name,-34}: {(ready ? "Ready" : "Blocked")}");
Console.WriteLine("Audit mode                        : Read only");
Console.WriteLine($"Overall readiness                 : {(assessment.IsReady ? "Ready" : "Blocked")}");
return assessment.IsReady ? 0 : 1;

static async Task<bool> HasDotNet10SdkAsync()
{
    try
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("dotnet", "--list-sdks")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.Start();
        string output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return process.ExitCode == 0
            && output.Split(Environment.NewLine).Any(line => line.StartsWith("10.", StringComparison.Ordinal));
    }
    catch { return false; }
}

static async Task<int> ValidateProvisioningAsync(
    string configurationFilePath,
    string publicCertificateFilePath)
{
    bool configurationValid = false;
    bool enrollmentValid = false;
    bool privateKeyReady = false;
    bool publicCertificateMatches = false;
    bool addressOwned = false;

    RuntimeHostPrivateNetworkDeploymentOptions? options = null;
    X509Certificate2? serverCertificate = null;
    try
    {
        options = await RuntimeHostPrivateNetworkDeploymentOptionsFile.LoadAsync(
            Path.GetFullPath(configurationFilePath));
        configurationValid = true;
    }
    catch { }

    if (options is not null)
    {
        try
        {
            _ = await RuntimeHostClientCredentialEnrollmentRegistryFile.LoadAsync(
                options.ClientEnrollmentFilePath);
            enrollmentValid = true;
        }
        catch { }

        try
        {
            serverCertificate = RuntimeHostCertificateStoreLoader.Load(
                options.ServerCertificate,
                requirePrivateKey: true);
            privateKeyReady = serverCertificate.HasPrivateKey;
        }
        catch { }

        addressOwned = NetworkInterface.GetAllNetworkInterfaces()
            .SelectMany(network => network.GetIPProperties().UnicastAddresses)
            .Any(address => address.Address.Equals(options.Binding.Address));
    }

    if (serverCertificate is not null)
    {
        try
        {
            using X509Certificate2 publicCertificate =
                X509CertificateLoader.LoadCertificateFromFile(
                    Path.GetFullPath(publicCertificateFilePath));
            publicCertificateMatches = publicCertificate.RawData.AsSpan()
                    .SequenceEqual(serverCertificate.RawData)
                && string.Equals(
                    publicCertificate.Thumbprint,
                    serverCertificate.Thumbprint,
                    StringComparison.OrdinalIgnoreCase);
        }
        catch { }
        finally
        {
            serverCertificate.Dispose();
        }
    }

    var assessment = new SecondPcRuntimeHostSecurityProvisioningAssessment(
        configurationValid,
        enrollmentValid,
        privateKeyReady,
        publicCertificateMatches,
        addressOwned);

    Console.WriteLine("HASE second Runtime Host security provisioning validation");
    foreach ((string name, bool ready) in assessment.Readiness)
        Console.WriteLine($"{name,-34}: {(ready ? "Ready" : "Blocked")}");
    Console.WriteLine("Sensitive deployment values          : Withheld");
    Console.WriteLine($"Overall readiness                 : {(assessment.IsReady ? "Ready" : "Blocked")}");
    return assessment.IsReady ? 0 : 1;
}

static async Task<int> ValidateClientProvisioningAsync(
    string configurationFilePath,
    string publicCertificateFilePath)
{
    bool configurationValid = false;
    bool clientPrivateKeyReady = false;
    bool trustedCertificateReady = false;
    bool publicCertificateMatches = false;

    RuntimeHostPrivateNetworkClientOptions? options = null;
    X509Certificate2? trustedCertificate = null;
    try
    {
        options = await RuntimeHostPrivateNetworkClientOptionsFile.LoadAsync(
            Path.GetFullPath(configurationFilePath));
        configurationValid = true;
    }
    catch { }

    if (options is not null)
    {
        try
        {
            using X509Certificate2 clientCertificate =
                RuntimeHostCertificateStoreLoader.Load(
                    options.ClientCertificate,
                    requirePrivateKey: true);
            clientPrivateKeyReady = clientCertificate.HasPrivateKey;
        }
        catch { }

        try
        {
            trustedCertificate = RuntimeHostCertificateStoreLoader.Load(
                options.TrustedServerCertificate,
                requirePrivateKey: false);
            trustedCertificateReady = true;
        }
        catch { }
    }

    if (trustedCertificate is not null)
    {
        try
        {
            using X509Certificate2 publicCertificate =
                X509CertificateLoader.LoadCertificateFromFile(
                    Path.GetFullPath(publicCertificateFilePath));
            publicCertificateMatches = publicCertificate.RawData.AsSpan()
                    .SequenceEqual(trustedCertificate.RawData)
                && string.Equals(
                    publicCertificate.Thumbprint,
                    trustedCertificate.Thumbprint,
                    StringComparison.OrdinalIgnoreCase);
        }
        catch { }
        finally
        {
            trustedCertificate.Dispose();
        }
    }

    var assessment = new MiniPcClientSecurityProvisioningAssessment(
        configurationValid,
        clientPrivateKeyReady,
        trustedCertificateReady,
        publicCertificateMatches,
        ExistingClientStatePreserved: true);

    Console.WriteLine("HASE MiniPC laptop security provisioning validation");
    foreach ((string name, bool ready) in assessment.Readiness)
        Console.WriteLine($"{name,-34}: {(ready ? "Ready" : "Blocked")}");
    Console.WriteLine("Sensitive deployment values       : Withheld");
    Console.WriteLine($"Overall readiness                 : {(assessment.IsReady ? "Ready" : "Blocked")}");
    return assessment.IsReady ? 0 : 1;
}
