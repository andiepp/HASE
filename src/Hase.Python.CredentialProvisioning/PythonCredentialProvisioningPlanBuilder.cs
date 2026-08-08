using System.Net;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Hase.Runtime.Remote.Grpc.Adapter;

namespace Hase.Python.CredentialProvisioning;

public sealed partial class PythonCredentialProvisioningPlanBuilder
{
    private static readonly TimeSpan Backdating = TimeSpan.FromMinutes(5);

    private static readonly IReadOnlyList<RuntimeHostPermission> KnownPermissions =
    [
        RuntimeHostPermission.ReadSnapshot,
        RuntimeHostPermission.ReadCachedProperty,
        RuntimeHostPermission.ReadAuthoritativeProperty,
        RuntimeHostPermission.WriteProperty,
        RuntimeHostPermission.ExecuteCommand,
        RuntimeHostPermission.SubscribeObservation,
        RuntimeHostPermission.SubscribeDiagnostics,
    ];

    private static readonly IReadOnlyList<string> PlannedGrants =
        Array.AsReadOnly(
            new[]
            {
                RuntimeHostPermission.ReadSnapshot.Value,
                RuntimeHostPermission.ReadAuthoritativeProperty.Value,
            });

    private static readonly IReadOnlyList<string> LeafEnhancedKeyUsages =
        Array.AsReadOnly(
            new[]
            {
                "1.3.6.1.5.5.7.3.2",
            });

    [GeneratedRegex("\\A[0-9A-Fa-f]{40}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex ThumbprintPattern();

    [GeneratedRegex("\\Ax509-sha256:[0-9a-f]{64}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex CredentialIdPattern();

    [GeneratedRegex("\\A[0-9a-f]{64}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Pattern();

    public async Task<PythonCredentialProvisioningPlan> CreateAsync(
        PythonCredentialProvisioningPlanRequest request,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var personalStore = new X509Store(
            StoreName.My,
            StoreLocation.CurrentUser);
        using var rootStore = new X509Store(
            StoreName.Root,
            StoreLocation.CurrentUser);
        personalStore.Open(OpenFlags.ReadOnly);
        rootStore.Open(OpenFlags.ReadOnly);

        return await CreateAsync(
                request,
                utcNow,
                personalStore.Certificates.Cast<X509Certificate2>(),
                rootStore.Certificates.Cast<X509Certificate2>(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    internal async Task<PythonCredentialProvisioningPlan> CreateAsync(
        PythonCredentialProvisioningPlanRequest request,
        DateTimeOffset utcNow,
        IEnumerable<X509Certificate2> personalCertificates,
        IEnumerable<X509Certificate2> trustedRoots,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(personalCertificates);
        ArgumentNullException.ThrowIfNull(trustedRoots);

        if (utcNow.Offset != TimeSpan.Zero)
        {
            Fail("timestamp-not-utc");
        }

        ValidatedRequest validated = ValidateRequest(request);
        DateTimeOffset notBefore = utcNow - Backdating;
        DateTimeOffset notAfter = notBefore + request.Validity;
        X509Certificate2 signingRoot = SelectSigningRoot(
            validated.SigningRootThumbprint,
            personalCertificates,
            trustedRoots,
            notBefore,
            notAfter);

        cancellationToken.ThrowIfCancellationRequested();
        string sourceProfileHash = await HashFileAsync(
                validated.SourceProfilePath,
                cancellationToken)
            .ConfigureAwait(false);
        string enrollmentHash = await HashFileAsync(
                validated.EnrollmentPath,
                cancellationToken)
            .ConfigureAwait(false);
        string actualPolicyHash = await HashFileAsync(
                validated.AuthorizationPolicyPath,
                cancellationToken)
            .ConfigureAwait(false);
        ValidateSourceProfile(validated.SourceProfilePath);
        if (!string.Equals(
            actualPolicyHash,
            validated.ExpectedAuthorizationPolicySha256,
            StringComparison.Ordinal))
        {
            Fail("authorization-policy-revision-mismatch");
        }

        RuntimeHostAuthorizationPolicy policy;
        RuntimeHostClientCredentialEnrollmentRegistry registry;
        try
        {
            policy = await RuntimeHostAuthorizationPolicyFile.LoadAsync(
                    validated.AuthorizationPolicyPath,
                    cancellationToken)
                .ConfigureAwait(false);
            registry = await RuntimeHostClientCredentialEnrollmentRegistryFile.LoadAsync(
                    validated.EnrollmentPath,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or ArgumentException)
        {
            Fail("security-configuration-invalid");
            throw;
        }

        foreach (RuntimeHostPermission permission in KnownPermissions)
        {
            if (policy.IsGranted(validated.PrincipalId, permission))
            {
                Fail("principal-already-authorized");
            }
        }

        var identity = new RuntimeHostClientCredentialIdentity(
            RuntimeHostAuthenticationMechanism.MutualTls,
            new RuntimeHostClientCredentialId(validated.CredentialId));
        if (registry.TryResolve(identity, utcNow, out _))
        {
            Fail("credential-already-enrolled");
        }

        await EnsureFileHashAsync(
                validated.SourceProfilePath,
                sourceProfileHash,
                cancellationToken)
            .ConfigureAwait(false);
        await EnsureFileHashAsync(
                validated.EnrollmentPath,
                enrollmentHash,
                cancellationToken)
            .ConfigureAwait(false);
        await EnsureFileHashAsync(
                validated.AuthorizationPolicyPath,
                actualPolicyHash,
                cancellationToken)
            .ConfigureAwait(false);

        string planId = CreatePlanId(
            validated,
            sourceProfileHash,
            enrollmentHash,
            actualPolicyHash,
            notBefore,
            notAfter,
            signingRoot.RawData);

        return new PythonCredentialProvisioningPlan(
            planId,
            validated.SigningRootThumbprint,
            validated.CredentialId,
            validated.PrincipalId,
            validated.TrustPolicyId,
            validated.SourceProfilePath,
            validated.ProvisioningDirectory,
            validated.CertificatePath,
            validated.PrivateKeyPath,
            validated.ProfilePath,
            validated.EnrollmentPath,
            validated.AuthorizationPolicyPath,
            sourceProfileHash,
            enrollmentHash,
            actualPolicyHash,
            notBefore,
            notAfter,
            2048,
            "sha256WithRSAEncryption",
            LeafEnhancedKeyUsages,
            PlannedGrants,
            request.AllowReplacement);
    }

    private static ValidatedRequest ValidateRequest(
        PythonCredentialProvisioningPlanRequest request)
    {
        string thumbprint = request.SigningRootThumbprint ?? string.Empty;
        if (thumbprint != thumbprint.Trim()
            || !ThumbprintPattern().IsMatch(thumbprint))
        {
            Fail("signing-root-thumbprint-invalid");
        }
        thumbprint = thumbprint.ToUpperInvariant();

        string credentialId = request.CredentialId ?? string.Empty;
        if (!CredentialIdPattern().IsMatch(credentialId))
        {
            Fail("credential-id-invalid");
        }
        string principalId = request.PrincipalId ?? string.Empty;
        if (string.IsNullOrWhiteSpace(principalId)
            || principalId != principalId.Trim())
        {
            Fail("principal-id-invalid");
        }
        string trustPolicyId = request.TrustPolicyId ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trustPolicyId)
            || trustPolicyId != trustPolicyId.Trim())
        {
            Fail("trust-policy-id-invalid");
        }
        string expectedPolicyHash =
            request.ExpectedAuthorizationPolicySha256 ?? string.Empty;
        if (!Sha256Pattern().IsMatch(expectedPolicyHash))
        {
            Fail("authorization-policy-hash-invalid");
        }
        if (request.Validity <= TimeSpan.Zero
            || request.Validity > PythonClientCredentialFactory.MaximumValidity)
        {
            Fail("validity-invalid");
        }

        string root = RequireDirectory(request.ProvisioningDirectory);
        string sourceProfile = RequireFile(request.SourceProfilePath, "source-profile-invalid");
        ValidateSourceProfile(sourceProfile);
        string enrollment = RequireFile(request.EnrollmentPath, "enrollment-invalid");
        string policy = RequireFile(request.AuthorizationPolicyPath, "authorization-policy-invalid");
        string certificate = RequireTarget(root, request.CertificatePath, request.AllowReplacement);
        string privateKey = RequireTarget(root, request.PrivateKeyPath, request.AllowReplacement);
        string profile = RequireTarget(root, request.ProfilePath, request.AllowReplacement);

        string[] allPaths = [sourceProfile, enrollment, policy, certificate, privateKey, profile];
        if (allPaths.Distinct(PathComparer).Count() != allPaths.Length)
        {
            Fail("paths-not-distinct");
        }

        return new ValidatedRequest(
            thumbprint,
            credentialId,
            principalId,
            trustPolicyId,
            sourceProfile,
            root,
            certificate,
            privateKey,
            profile,
            enrollment,
            policy,
            expectedPolicyHash);
    }

    private static X509Certificate2 SelectSigningRoot(
        string thumbprint,
        IEnumerable<X509Certificate2> personalCertificates,
        IEnumerable<X509Certificate2> trustedRoots,
        DateTimeOffset notBefore,
        DateTimeOffset notAfter)
    {
        X509Certificate2[] matches = personalCertificates
            .Where(certificate => string.Equals(
                certificate.Thumbprint,
                thumbprint,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (matches.Length != 1)
        {
            Fail("signing-root-not-unique");
        }

        X509Certificate2 root = matches[0];
        if (!root.HasPrivateKey
            || !root.SubjectName.RawData.AsSpan().SequenceEqual(root.IssuerName.RawData)
            || root.NotBefore.ToUniversalTime() > notBefore
            || root.NotAfter.ToUniversalTime() < notAfter)
        {
            Fail("signing-root-unusable");
        }

        using RSA? rsa = root.GetRSAPrivateKey();
        if (rsa is null)
        {
            Fail("signing-root-unusable");
        }

        X509Extension? constraintsSource = FindExtension(root, "2.5.29.19");
        X509Extension? keyUsageSource = FindExtension(root, "2.5.29.15");
        if (constraintsSource is null
            || keyUsageSource is null
            || !new X509BasicConstraintsExtension(
                constraintsSource,
                constraintsSource.Critical).CertificateAuthority
            || !new X509KeyUsageExtension(
                keyUsageSource,
                keyUsageSource.Critical).KeyUsages.HasFlag(X509KeyUsageFlags.KeyCertSign))
        {
            Fail("signing-root-unusable");
        }

        if (trustedRoots.Count(candidate => candidate.RawData.AsSpan().SequenceEqual(root.RawData)) != 1)
        {
            Fail("signing-root-not-trusted");
        }
        return root;
    }

    private static X509Extension? FindExtension(X509Certificate2 certificate, string oid) =>
        certificate.Extensions.Cast<X509Extension>()
            .SingleOrDefault(extension => extension.Oid?.Value == oid);

    private static string RequireDirectory(string value)
    {
        string path = RequireAbsolute(value);
        if (!Directory.Exists(path) || IsReparsePoint(path))
        {
            Fail("provisioning-directory-invalid");
        }
        return path;
    }

    private static string RequireFile(string value, string code)
    {
        string path = RequireAbsolute(value);
        if (!File.Exists(path) || IsReparsePoint(path))
        {
            Fail(code);
        }
        return path;
    }

    private static void ValidateSourceProfile(string path)
    {
        try
        {
            var file = new FileInfo(path);
            if (file.Length > 64 * 1024)
            {
                Fail("source-profile-invalid");
            }

            byte[] bytes = File.ReadAllBytes(path);
            try
            {
                using JsonDocument document = JsonDocument.Parse(bytes);
                JsonElement root = document.RootElement;
                RequireJsonProperties(
                    root,
                    "formatVersion",
                    "address",
                    "clientCertificate",
                    "trustedServerCertificate");
                if (root.GetProperty("formatVersion").ValueKind != JsonValueKind.Number
                    || root.GetProperty("formatVersion").GetInt32() != 1)
                {
                    Fail("source-profile-invalid");
                }

                string address = RequireJsonString(root.GetProperty("address"));
                if (!IsStrictAddress(address))
                {
                    Fail("source-profile-invalid");
                }

                JsonElement client = root.GetProperty("clientCertificate");
                JsonElement trusted = root.GetProperty("trustedServerCertificate");
                RequireJsonProperties(client, "certificateChainPath", "privateKeyPath");
                RequireJsonProperties(trusted, "certificatePath");
                string[] credentialPaths =
                [
                    RequireJsonString(client.GetProperty("certificateChainPath")),
                    RequireJsonString(client.GetProperty("privateKeyPath")),
                    RequireJsonString(trusted.GetProperty("certificatePath")),
                ];
                if (credentialPaths.Any(value =>
                        !Path.IsPathFullyQualified(value)
                        || !File.Exists(value)
                        || IsReparsePoint(value))
                    || credentialPaths.Select(Path.GetFullPath)
                        .Distinct(PathComparer)
                        .Count() != 3)
                {
                    Fail("source-profile-invalid");
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
            }
        }
        catch (PythonCredentialProvisioningPlanException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or InvalidOperationException
            or FormatException)
        {
            Fail("source-profile-invalid");
        }
    }

    private static void RequireJsonProperties(
        JsonElement element,
        params string[] expected)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.EnumerateObject()
                .Select(property => property.Name)
                .Order(StringComparer.Ordinal)
                .SequenceEqual(expected.Order(StringComparer.Ordinal)))
        {
            Fail("source-profile-invalid");
        }
    }

    private static string RequireJsonString(JsonElement element)
    {
        string? value = element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
        {
            Fail("source-profile-invalid");
        }
        return value;
    }

    private static bool IsStrictAddress(string address)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out Uri? uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || uri.IsDefaultPort
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment)
            || address != $"https://{uri.Authority}")
        {
            return false;
        }
        return IPAddress.TryParse(uri.Host.Trim('[', ']'), out _);
    }

    private static string RequireTarget(string root, string value, bool allowReplacement)
    {
        string path = RequireAbsolute(value);
        string relative = Path.GetRelativePath(root, path);
        if (Path.IsPathRooted(relative)
            || relative == ".."
            || relative.StartsWith(".." + Path.DirectorySeparatorChar))
        {
            Fail("target-outside-provisioning-directory");
        }
        string? parent = Path.GetDirectoryName(path);
        if (parent is null || !Directory.Exists(parent))
        {
            Fail("target-parent-unavailable");
        }
        if (Directory.Exists(path)
            || (File.Exists(path) && IsReparsePoint(path))
            || ContainsReparsePoint(root, parent))
        {
            Fail("target-path-invalid");
        }
        if (File.Exists(path) && !allowReplacement)
        {
            Fail("replacement-not-authorized");
        }
        return path;
    }

    private static string RequireAbsolute(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value != value.Trim()
            || !Path.IsPathFullyQualified(value))
        {
            Fail("path-invalid");
        }
        return Path.GetFullPath(value);
    }

    private static bool ContainsReparsePoint(string root, string path)
    {
        string? current = path;
        while (current is not null)
        {
            if (IsReparsePoint(current))
            {
                return true;
            }
            if (PathComparer.Equals(current, root))
            {
                return false;
            }
            current = Path.GetDirectoryName(current);
        }
        return true;
    }

    private static bool IsReparsePoint(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private static async Task<string> HashFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            await using FileStream stream = File.OpenRead(path);
            byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken)
                .ConfigureAwait(false);
            try
            {
                return Convert.ToHexStringLower(hash);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(hash);
            }
        }
        catch (Exception exception) when (
            exception is IOException
            or UnauthorizedAccessException)
        {
            throw new PythonCredentialProvisioningPlanException(
                "input-unavailable");
        }
    }

    private static async Task EnsureFileHashAsync(
        string path,
        string expectedHash,
        CancellationToken cancellationToken)
    {
        string actualHash = await HashFileAsync(path, cancellationToken)
            .ConfigureAwait(false);
        if (!string.Equals(actualHash, expectedHash, StringComparison.Ordinal))
        {
            Fail("input-revision-changed");
        }
    }

    private static string CreatePlanId(
        ValidatedRequest request,
        string sourceProfileHash,
        string enrollmentHash,
        string policyHash,
        DateTimeOffset notBefore,
        DateTimeOffset notAfter,
        byte[] rootDer)
    {
        string rootHash = Convert.ToHexStringLower(SHA256.HashData(rootDer));
        string canonical = string.Join(
            "\n",
            request.CredentialId,
            request.PrincipalId,
            request.TrustPolicyId,
            request.SourceProfilePath,
            request.ProvisioningDirectory,
            request.CertificatePath,
            request.PrivateKeyPath,
            request.ProfilePath,
            request.EnrollmentPath,
            request.AuthorizationPolicyPath,
            sourceProfileHash,
            enrollmentHash,
            policyHash,
            notBefore.ToString("O"),
            notAfter.ToString("O"),
            rootHash);
        byte[] bytes = Encoding.UTF8.GetBytes(canonical);
        try
        {
            return "python-provisioning-plan-sha256:"
                + Convert.ToHexStringLower(SHA256.HashData(bytes));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    [DoesNotReturn]
    private static void Fail(string code) =>
        throw new PythonCredentialProvisioningPlanException(code);

    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private sealed record ValidatedRequest(
        string SigningRootThumbprint,
        string CredentialId,
        string PrincipalId,
        string TrustPolicyId,
        string SourceProfilePath,
        string ProvisioningDirectory,
        string CertificatePath,
        string PrivateKeyPath,
        string ProfilePath,
        string EnrollmentPath,
        string AuthorizationPolicyPath,
        string ExpectedAuthorizationPolicySha256);
}
