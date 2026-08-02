using System.IO;
using System.Text.Json;
using Hase.DesktopHost.Configuration;

namespace Hase.DesktopHost.Tests.Configuration;

public sealed class DesktopRuntimeHostInstallationAuditTests
{
    [Fact]
    public async Task AuditAsync_CompleteInstallation_ShouldReturnAuthoritativeIdentity()
    {
        using TestInstallation installation = new();
        installation.WriteComplete();

        DesktopRuntimeHostInstallationAuditResult result =
            await DesktopRuntimeHostInstallationAudit.AuditAsync(installation.Root);

        Assert.Equal("runtime-host-audit-01", result.RuntimeHostId.Value);
    }

    [Fact]
    public async Task AuditAsync_MissingExecutable_ShouldFailClosed()
    {
        using TestInstallation installation = new();
        installation.WriteComplete();
        File.Delete(installation.ExecutablePath);

        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => DesktopRuntimeHostInstallationAudit.AuditAsync(installation.Root));

        Assert.Contains("application executable", exception.Message);
    }

    [Fact]
    public async Task AuditAsync_ProfileOutsideInstallationCustody_ShouldFailClosed()
    {
        using TestInstallation installation = new();
        installation.WriteComplete(identityPath: Path.Combine(installation.Root, "other-identity.json"));

        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => DesktopRuntimeHostInstallationAudit.AuditAsync(installation.Root));

        Assert.Contains("identity file path", exception.Message);
    }

    [Fact]
    public async Task AuditAsync_InvalidIdentityDocument_ShouldFailClosed()
    {
        using TestInstallation installation = new();
        installation.WriteComplete();
        File.WriteAllText(installation.IdentityPath, "{}");

        await Assert.ThrowsAsync<InvalidDataException>(
            () => DesktopRuntimeHostInstallationAudit.AuditAsync(installation.Root));
    }

    [Fact]
    public async Task AuditAsync_MissingClientEnrollment_ShouldFailClosed()
    {
        using TestInstallation installation = new();
        installation.WriteComplete();
        File.Delete(installation.EnrollmentPath);

        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => DesktopRuntimeHostInstallationAudit.AuditAsync(installation.Root));

        Assert.Contains("client-enrollment configuration", exception.Message);
    }

    private sealed class TestInstallation : IDisposable
    {
        public TestInstallation()
        {
            Root = Path.Combine(Path.GetTempPath(), "hase-43g1", Guid.NewGuid().ToString("N"));
            Application = Path.Combine(Root, "Application");
            Configuration = Path.Combine(Root, "Configuration");
            Identity = Path.Combine(Root, "Identity");
            Directory.CreateDirectory(Application);
            Directory.CreateDirectory(Configuration);
            Directory.CreateDirectory(Identity);
        }

        public string Root { get; }
        public string Application { get; }
        public string Configuration { get; }
        public string Identity { get; }
        public string ExecutablePath => Path.Combine(Application, "Hase.DesktopHost.App.exe");
        public string ProfilePath => Path.Combine(Configuration, "desktop-runtime-host.json");
        public string EndpointsPath => Path.Combine(Configuration, "desktop-runtime-endpoints.json");
        public string PrivateNetworkPath => Path.Combine(Configuration, "desktop-private-network.json");
        public string EnrollmentPath => Path.Combine(Configuration, "client-enrollments.json");
        public string IdentityPath => Path.Combine(Identity, "runtime-host-identity.json");

        public void WriteComplete(string? identityPath = null)
        {
            File.WriteAllText(ExecutablePath, "test executable");
            WriteJson(EnrollmentPath, new
            {
                formatVersion = 1,
                enrollments = new object[]
                {
                    new
                    {
                        credentialId = "x509-sha256:"
                            + "0123456789abcdef0123456789abcdef"
                            + "0123456789abcdef0123456789abcdef",
                        principalId = "audit-client",
                        trustPolicyId = "audit-policy"
                    }
                }
            });
            WriteJson(IdentityPath, new
            {
                formatVersion = 1,
                runtimeHostId = "runtime-host-audit-01"
            });
            WriteJson(EndpointsPath, new
            {
                formatVersion = 1,
                endpoints = new object[]
                {
                    new
                    {
                        kind = "NativeNetwork",
                        expectedEndpointId = "endpoint-audit-01",
                        host = "endpoint.invalid",
                        port = 5000
                    }
                }
            });
            WriteJson(PrivateNetworkPath, new
            {
                formatVersion = 1,
                binding = new { address = "192.0.2.1", port = 5001 },
                serverCertificate = new
                {
                    storeName = "My",
                    storeLocation = "CurrentUser",
                    thumbprint = "00112233445566778899AABBCCDDEEFF00112233"
                },
                clientEnrollmentFilePath = EnrollmentPath
            });
            WriteJson(ProfilePath, new
            {
                formatVersion = 1,
                identityFilePath = identityPath ?? IdentityPath,
                privateNetworkConfigurationFilePath = PrivateNetworkPath,
                endpointCompositionFilePath = EndpointsPath,
                maximumDiagnosticLevel = "Bytes",
                includeByteBufferSimulation = false
            });
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);

        private static void WriteJson(string path, object document) =>
            File.WriteAllText(
                path,
                JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true }));
    }
}
