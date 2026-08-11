using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Hase.Runtime.Remote.Grpc.Adapter;

namespace Hase.Python.CredentialProvisioning;

/// <summary>
/// Performs one offline, read-only inspection of the credential selected by a
/// strict Python Runtime Host profile. No file is created or modified.
/// </summary>
public sealed class PythonCredentialLifecycleInspector
{
    public static readonly TimeSpan PlannedRotationWindow =
        TimeSpan.FromDays(30);

    public static readonly TimeSpan UrgentExpiryWindow =
        TimeSpan.FromDays(7);

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

    public async Task<PythonCredentialLifecycleInspectionResult> InspectAsync(
        PythonCredentialLifecycleInspectionRequest request,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            if (utcNow.Offset != TimeSpan.Zero)
            {
                Fail("timestamp-not-utc");
            }

            ValidatedRequest validated = ValidateRequest(request);
            cancellationToken.ThrowIfCancellationRequested();

            byte[] profileBytes = await ReadBoundedFileAsync(
                validated.ProfilePath, cancellationToken).ConfigureAwait(false);
            try
            {
                PythonRuntimeHostProfileDocument profile =
                    PythonRuntimeHostProfileDocument.Load(profileBytes);
                ValidateSelectedPaths(profile);

                byte[] certificateBytes = await ReadBoundedFileAsync(
                    profile.ClientCertificateChainPath, cancellationToken)
                    .ConfigureAwait(false);
                byte[] privateKeyBytes = await ReadBoundedFileAsync(
                    profile.ClientPrivateKeyPath, cancellationToken)
                    .ConfigureAwait(false);
                try
                {
                    using X509Certificate2 certificate =
                        LoadCertificate(certificateBytes);
                    ValidateClientCertificate(certificate, privateKeyBytes);

                    string credentialId = CreateCredentialId(certificate);
                    RuntimeHostClientPrincipal principal = await ResolveEnrollmentAsync(
                        validated, credentialId, utcNow, cancellationToken)
                        .ConfigureAwait(false);
                    IReadOnlyList<string> grants = await ValidateAuthorizationAsync(
                        validated, cancellationToken).ConfigureAwait(false);

                    string trustedServerHash = await HashFileAsync(
                        profile.TrustedServerCertificatePath, cancellationToken)
                        .ConfigureAwait(false);
                    DateTimeOffset notBefore = new(
                        certificate.NotBefore.ToUniversalTime(), TimeSpan.Zero);
                    DateTimeOffset notAfter = new(
                        certificate.NotAfter.ToUniversalTime(), TimeSpan.Zero);
                    TimeSpan remaining = notAfter - utcNow;

                    return new PythonCredentialLifecycleInspectionResult(
                        Classify(notBefore, notAfter, utcNow),
                        credentialId,
                        principal.PrincipalId,
                        principal.TrustPolicyId,
                        notBefore,
                        notAfter,
                        remaining <= TimeSpan.Zero
                            ? 0
                            : (int)Math.Floor(remaining.TotalDays),
                        grants,
                        Sha256(profileBytes),
                        await HashFileAsync(validated.EnrollmentPath,
                            cancellationToken).ConfigureAwait(false),
                        await HashFileAsync(validated.AuthorizationPolicyPath,
                            cancellationToken).ConfigureAwait(false),
                        trustedServerHash);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(certificateBytes);
                    CryptographicOperations.ZeroMemory(privateKeyBytes);
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(profileBytes);
            }
        }
        catch (PythonCredentialLifecycleInspectionException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or CryptographicException
            or ArgumentException
            or InvalidOperationException
            or FormatException)
        {
            throw new PythonCredentialLifecycleInspectionException(
                "lifecycle-input-invalid");
        }
    }

    public static PythonCredentialLifecycleState Classify(
        DateTimeOffset notBeforeUtc,
        DateTimeOffset notAfterUtc,
        DateTimeOffset utcNow)
    {
        if (notBeforeUtc.Offset != TimeSpan.Zero
            || notAfterUtc.Offset != TimeSpan.Zero
            || utcNow.Offset != TimeSpan.Zero
            || notAfterUtc <= notBeforeUtc)
        {
            throw new ArgumentException(
                "Credential lifecycle timestamps must be valid UTC values.");
        }

        if (utcNow < notBeforeUtc)
        {
            return PythonCredentialLifecycleState.NotYetValid;
        }
        if (utcNow >= notAfterUtc)
        {
            return PythonCredentialLifecycleState.Expired;
        }

        TimeSpan remaining = notAfterUtc - utcNow;
        if (remaining <= UrgentExpiryWindow)
        {
            return PythonCredentialLifecycleState.Expiring;
        }
        if (remaining <= PlannedRotationWindow)
        {
            return PythonCredentialLifecycleState.RotationDue;
        }
        return PythonCredentialLifecycleState.Active;
    }

    private static ValidatedRequest ValidateRequest(
        PythonCredentialLifecycleInspectionRequest request)
    {
        string profile = RequireFile(request.ProfilePath, "profile-invalid");
        string enrollment = RequireFile(request.EnrollmentPath, "enrollment-invalid");
        string policy = RequireFile(
            request.AuthorizationPolicyPath, "authorization-policy-invalid");
        if (new[] { profile, enrollment, policy }.Distinct(PathComparer).Count() != 3)
        {
            Fail("paths-not-distinct");
        }

        string principal = RequireValue(request.ExpectedPrincipalId,
            "principal-id-invalid");
        string trust = RequireValue(request.ExpectedTrustPolicyId,
            "trust-policy-id-invalid");
        string[] grants = request.ExpectedAuthorizationGrants?.ToArray()
            ?? throw new PythonCredentialLifecycleInspectionException(
                "authorization-grants-invalid");
        string[] known = KnownPermissions.Select(value => value.Value).ToArray();
        if (grants.Length == 0
            || grants.Any(string.IsNullOrWhiteSpace)
            || grants.Any(value => value != value.Trim())
            || grants.Distinct(StringComparer.Ordinal).Count() != grants.Length
            || grants.Except(known, StringComparer.Ordinal).Any())
        {
            Fail("authorization-grants-invalid");
        }

        return new(profile, enrollment, policy, principal, trust,
            Array.AsReadOnly(grants.Order(StringComparer.Ordinal).ToArray()));
    }

    private static void ValidateSelectedPaths(PythonRuntimeHostProfileDocument profile)
    {
        string[] paths =
        [
            profile.ClientCertificateChainPath,
            profile.ClientPrivateKeyPath,
            profile.TrustedServerCertificatePath,
        ];
        if (paths.Distinct(PathComparer).Count() != paths.Length
            || paths.Any(path => !File.Exists(path) || IsReparsePoint(path)))
        {
            Fail("profile-custody-invalid");
        }
    }

    private static void ValidateClientCertificate(
        X509Certificate2 certificate,
        byte[] privateKeyPem)
    {
        using RSA privateKey = RSA.Create();
        byte[] privateKeyDer = DecodePem(privateKeyPem, "PRIVATE KEY");
        try
        {
            privateKey.ImportPkcs8PrivateKey(privateKeyDer, out int bytesRead);
            if (bytesRead != privateKeyDer.Length)
            {
                Fail("credential-material-invalid");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateKeyDer);
        }
        using RSA? publicKey = certificate.GetRSAPublicKey();
        X509EnhancedKeyUsageExtension? eku = certificate.Extensions
            .OfType<X509EnhancedKeyUsageExtension>().SingleOrDefault();
        if (publicKey is null
            || !ParametersEqual(publicKey.ExportParameters(false),
                privateKey.ExportParameters(false))
            || eku is null
            || eku.EnhancedKeyUsages.Cast<Oid>()
                .Count(oid => oid.Value == "1.3.6.1.5.5.7.3.2") != 1)
        {
            Fail("credential-material-invalid");
        }
    }

    private static async Task<RuntimeHostClientPrincipal> ResolveEnrollmentAsync(
        ValidatedRequest request,
        string credentialId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        RuntimeHostClientCredentialEnrollmentRegistry registry =
            await RuntimeHostClientCredentialEnrollmentRegistryFile.LoadAsync(
                request.EnrollmentPath, cancellationToken).ConfigureAwait(false);
        var identity = new RuntimeHostClientCredentialIdentity(
            RuntimeHostAuthenticationMechanism.MutualTls,
            new RuntimeHostClientCredentialId(credentialId));
        if (!registry.TryResolve(identity, utcNow, out RuntimeHostClientPrincipal? principal)
            || principal is null
            || principal.PrincipalId != request.PrincipalId
            || principal.TrustPolicyId != request.TrustPolicyId)
        {
            Fail("selected-credential-not-enrolled");
        }
        return principal;
    }

    private static async Task<IReadOnlyList<string>> ValidateAuthorizationAsync(
        ValidatedRequest request,
        CancellationToken cancellationToken)
    {
        RuntimeHostAuthorizationPolicy policy =
            await RuntimeHostAuthorizationPolicyFile.LoadAsync(
                request.AuthorizationPolicyPath, cancellationToken)
                .ConfigureAwait(false);
        string[] actual = KnownPermissions
            .Where(permission => policy.IsGranted(request.PrincipalId, permission))
            .Select(permission => permission.Value)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (!actual.SequenceEqual(request.Grants, StringComparer.Ordinal))
        {
            Fail("authorization-grants-mismatch");
        }
        return Array.AsReadOnly(actual);
    }

    private static async Task<byte[]> ReadBoundedFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var information = new FileInfo(path);
        if (!information.Exists || information.Length is <= 0 or > 64 * 1024
            || IsReparsePoint(path))
        {
            Fail("credential-file-invalid");
        }
        return await File.ReadAllBytesAsync(path, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<string> HashFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read,
            FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
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

    private static string Sha256(byte[] bytes)
    {
        byte[] hash = SHA256.HashData(bytes);
        try
        {
            return Convert.ToHexStringLower(hash);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(hash);
        }
    }

    private static string CreateCredentialId(X509Certificate2 certificate) =>
        "x509-sha256:" + Sha256(certificate.RawData);

    private static X509Certificate2 LoadCertificate(byte[] certificatePem)
    {
        byte[] certificateDer = DecodePem(certificatePem, "CERTIFICATE");
        try
        {
            return X509CertificateLoader.LoadCertificate(certificateDer);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(certificateDer);
        }
    }

    private static byte[] DecodePem(byte[] pem, string expectedLabel)
    {
        char[] characters = Encoding.ASCII.GetChars(pem);
        try
        {
            if (!PemEncoding.TryFind(characters, out PemFields fields)
                || !characters.AsSpan(fields.Label).SequenceEqual(expectedLabel)
                || fields.Location.Start.Value != 0
                || fields.Location.End.Value != characters.Length)
            {
                Fail("credential-material-invalid");
            }

            byte[] decoded = new byte[fields.DecodedDataLength];
            if (!Convert.TryFromBase64Chars(
                    characters.AsSpan(fields.Base64Data), decoded,
                    out int bytesWritten)
                || bytesWritten != decoded.Length)
            {
                CryptographicOperations.ZeroMemory(decoded);
                Fail("credential-material-invalid");
            }
            return decoded;
        }
        finally
        {
            Array.Clear(characters);
        }
    }

    private static bool ParametersEqual(RSAParameters left, RSAParameters right) =>
        left.Modulus is not null
        && right.Modulus is not null
        && left.Exponent is not null
        && right.Exponent is not null
        && CryptographicOperations.FixedTimeEquals(left.Modulus, right.Modulus)
        && CryptographicOperations.FixedTimeEquals(left.Exponent, right.Exponent);

    private static string RequireFile(string value, string code)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value != value.Trim()
            || !Path.IsPathFullyQualified(value))
        {
            Fail(code);
        }
        string path = Path.GetFullPath(value);
        if (!File.Exists(path) || IsReparsePoint(path))
        {
            Fail(code);
        }
        return path;
    }

    private static string RequireValue(string value, string code)
    {
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
        {
            Fail(code);
        }
        return value;
    }

    private static bool IsReparsePoint(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    [DoesNotReturn]
    private static void Fail(string code) =>
        throw new PythonCredentialLifecycleInspectionException(code);

    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private sealed record ValidatedRequest(
        string ProfilePath,
        string EnrollmentPath,
        string AuthorizationPolicyPath,
        string PrincipalId,
        string TrustPolicyId,
        IReadOnlyList<string> Grants);
}
