using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Hase.Runtime.Remote.Grpc.Adapter;

namespace Hase.Python.CredentialProvisioning;

/// <summary>
/// Prepares planned-rotation candidates in memory after re-inspecting the
/// complete selected deployment state. It publishes no file.
/// </summary>
public sealed class PythonCredentialRotationPreparer
{
    public async Task<PythonCredentialRotationCandidates> PrepareAsync(
        PythonCredentialRotationPreparationRequest request,
        PythonClientCredentialMaterial replacement,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(replacement);

        byte[]? certificate = null;
        byte[]? privateKey = null;
        byte[]? profile = null;
        byte[]? enrollment = null;
        byte[]? policy = null;
        byte[]? overlap = null;
        byte[]? final = null;
        try
        {
            ValidateHash(request.ExpectedProfileSha256);
            ValidateHash(request.ExpectedEnrollmentSha256);
            ValidateHash(request.ExpectedAuthorizationPolicySha256);
            ValidateHash(request.ExpectedTrustedServerCertificateSha256);
            PythonCredentialLifecycleInspectionResult inspected =
                await new PythonCredentialLifecycleInspector().InspectAsync(
                    request.Inspection, utcNow, cancellationToken)
                    .ConfigureAwait(false);
            if (inspected.State is PythonCredentialLifecycleState.Expired
                    or PythonCredentialLifecycleState.NotYetValid
                || inspected.CredentialId != request.ExpectedCurrentCredentialId
                || inspected.ProfileSha256 != request.ExpectedProfileSha256
                || inspected.EnrollmentSha256 != request.ExpectedEnrollmentSha256
                || inspected.AuthorizationPolicySha256
                    != request.ExpectedAuthorizationPolicySha256
                || inspected.TrustedServerCertificateSha256
                    != request.ExpectedTrustedServerCertificateSha256)
            {
                Fail("rotation-source-revision-mismatch");
            }
            if (replacement.CredentialId == inspected.CredentialId)
            {
                Fail("replacement-credential-not-distinct");
            }

            certificate = replacement.CertificatePem.ToArray();
            privateKey = replacement.PrivateKeyPem.ToArray();
            ValidateReplacement(certificate, privateKey,
                replacement.CredentialId, utcNow);
            profile = await File.ReadAllBytesAsync(
                request.Inspection.ProfilePath, cancellationToken)
                .ConfigureAwait(false);
            enrollment = await File.ReadAllBytesAsync(
                request.Inspection.EnrollmentPath, cancellationToken)
                .ConfigureAwait(false);
            policy = await File.ReadAllBytesAsync(
                request.Inspection.AuthorizationPolicyPath, cancellationToken)
                .ConfigureAwait(false);

            if (!Sha256(profile).Equals(inspected.ProfileSha256,
                    StringComparison.Ordinal)
                || !Sha256(enrollment).Equals(inspected.EnrollmentSha256,
                    StringComparison.Ordinal)
                || !Sha256(policy).Equals(inspected.AuthorizationPolicySha256,
                    StringComparison.Ordinal))
            {
                Fail("rotation-source-revision-mismatch");
            }

            (overlap, final) = CreateEnrollmentCandidates(
                enrollment,
                inspected.CredentialId,
                replacement.CredentialId,
                inspected.PrincipalId,
                inspected.TrustPolicyId,
                utcNow);
            _ = RuntimeHostClientCredentialEnrollmentRegistryFile.Load(overlap);
            _ = RuntimeHostClientCredentialEnrollmentRegistryFile.Load(final);

            var result = new PythonCredentialRotationCandidates(
                certificate, privateKey, profile, overlap, final, policy,
                inspected.CredentialId, replacement.CredentialId,
                inspected.PrincipalId, inspected.TrustPolicyId,
                inspected.AuthorizationGrants);
            certificate = privateKey = profile = overlap = final = policy = null;
            return result;
        }
        catch (PythonCredentialRotationPreparationException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (PythonCredentialLifecycleInspectionException exception)
        {
            throw new PythonCredentialRotationPreparationException(
                "rotation-" + exception.Code);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or CryptographicException
            or InvalidDataException
            or JsonException
            or ArgumentException
            or InvalidOperationException
            or FormatException)
        {
            throw new PythonCredentialRotationPreparationException(
                "rotation-input-invalid");
        }
        finally
        {
            Zero(certificate);
            Zero(privateKey);
            Zero(profile);
            Zero(enrollment);
            Zero(policy);
            Zero(overlap);
            Zero(final);
        }
    }

    private static (byte[] Overlap, byte[] Final) CreateEnrollmentCandidates(
        byte[] source,
        string currentCredentialId,
        string replacementCredentialId,
        string principalId,
        string trustPolicyId,
        DateTimeOffset utcNow)
    {
        using JsonDocument document = JsonDocument.Parse(source);
        RejectDuplicates(document.RootElement);
        JsonElement root = document.RootElement;
        RequireProperties(root, "formatVersion", "enrollments");
        if (root.GetProperty("formatVersion").GetInt32() != 1
            || root.GetProperty("enrollments").ValueKind != JsonValueKind.Array)
        {
            Fail("enrollment-invalid");
        }

        List<Enrollment> entries = [];
        foreach (JsonElement entry in root.GetProperty("enrollments").EnumerateArray())
        {
            RequireProperties(entry, "credentialId", "principalId", "trustPolicyId");
            entries.Add(new(
                RequireString(entry.GetProperty("credentialId")),
                RequireString(entry.GetProperty("principalId")),
                RequireString(entry.GetProperty("trustPolicyId"))));
        }
        Enrollment[] current = entries
            .Where(entry => entry.CredentialId == currentCredentialId).ToArray();
        if (current.Length != 1
            || current[0].PrincipalId != principalId
            || current[0].TrustPolicyId != trustPolicyId
            || entries.Any(entry => entry.CredentialId == replacementCredentialId))
        {
            Fail("enrollment-transition-invalid");
        }

        var replacement = new Enrollment(
            replacementCredentialId, principalId, trustPolicyId);
        byte[] overlap = Serialize(entries.Append(replacement));
        byte[] final = Serialize(entries
            .Where(entry => entry.CredentialId != currentCredentialId)
            .Append(replacement));
        ValidateResolution(overlap, currentCredentialId, principalId,
            trustPolicyId, utcNow, expected: true);
        ValidateResolution(overlap, replacementCredentialId, principalId,
            trustPolicyId, utcNow, expected: true);
        ValidateResolution(final, currentCredentialId, principalId,
            trustPolicyId, utcNow, expected: false);
        ValidateResolution(final, replacementCredentialId, principalId,
            trustPolicyId, utcNow, expected: true);
        return (overlap, final);
    }

    private static void ValidateResolution(
        byte[] document,
        string credentialId,
        string principalId,
        string trustPolicyId,
        DateTimeOffset utcNow,
        bool expected)
    {
        RuntimeHostClientCredentialEnrollmentRegistry registry =
            RuntimeHostClientCredentialEnrollmentRegistryFile.Load(document);
        var identity = new RuntimeHostClientCredentialIdentity(
            RuntimeHostAuthenticationMechanism.MutualTls,
            new RuntimeHostClientCredentialId(credentialId));
        bool resolved = registry.TryResolve(identity, utcNow,
            out RuntimeHostClientPrincipal? principal);
        if (resolved != expected
            || resolved && (principal!.PrincipalId != principalId
                || principal.TrustPolicyId != trustPolicyId))
        {
            Fail("enrollment-transition-invalid");
        }
    }

    private static byte[] Serialize(IEnumerable<Enrollment> entries) =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            formatVersion = 1,
            enrollments = entries.Select(entry => new
            {
                credentialId = entry.CredentialId,
                principalId = entry.PrincipalId,
                trustPolicyId = entry.TrustPolicyId,
            }),
        }, new JsonSerializerOptions { WriteIndented = true });

    private static void ValidateReplacement(
        byte[] certificatePem,
        byte[] privateKeyPem,
        string expectedCredentialId,
        DateTimeOffset utcNow)
    {
        byte[] certificateDer = DecodePem(certificatePem, "CERTIFICATE");
        byte[] privateKeyDer = DecodePem(privateKeyPem, "PRIVATE KEY");
        try
        {
            using X509Certificate2 certificate =
                X509CertificateLoader.LoadCertificate(certificateDer);
            using RSA privateKey = RSA.Create();
            privateKey.ImportPkcs8PrivateKey(privateKeyDer, out int read);
            using RSA? publicKey = certificate.GetRSAPublicKey();
            if (read != privateKeyDer.Length
                || publicKey is null
                || !ParametersEqual(publicKey.ExportParameters(false),
                    privateKey.ExportParameters(false))
                || "x509-sha256:" + Sha256(certificate.RawData)
                    != expectedCredentialId
                || certificate.NotBefore.ToUniversalTime() > utcNow.UtcDateTime
                || certificate.NotAfter.ToUniversalTime() <= utcNow.UtcDateTime)
            {
                Fail("replacement-credential-invalid");
            }
        }
        finally
        {
            Zero(certificateDer);
            Zero(privateKeyDer);
        }
    }

    private static byte[] DecodePem(byte[] pem, string label)
    {
        char[] characters = Encoding.ASCII.GetChars(pem);
        try
        {
            if (!PemEncoding.TryFind(characters, out PemFields fields)
                || !characters.AsSpan(fields.Label).SequenceEqual(label)
                || fields.Location.Start.Value != 0
                || fields.Location.End.Value != characters.Length)
            {
                Fail("replacement-credential-invalid");
            }
            byte[] decoded = new byte[fields.DecodedDataLength];
            if (!Convert.TryFromBase64Chars(characters.AsSpan(fields.Base64Data),
                    decoded, out int written)
                || written != decoded.Length)
            {
                Zero(decoded);
                Fail("replacement-credential-invalid");
            }
            return decoded;
        }
        finally
        {
            Array.Clear(characters);
        }
    }

    private static void RejectDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) Fail("enrollment-invalid");
                RejectDuplicates(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
                RejectDuplicates(item);
        }
    }

    private static void RequireProperties(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.EnumerateObject().Select(value => value.Name)
                .Order(StringComparer.Ordinal)
                .SequenceEqual(names.Order(StringComparer.Ordinal)))
        {
            Fail("enrollment-invalid");
        }
    }

    private static string RequireString(JsonElement element)
    {
        string? value = element.ValueKind == JsonValueKind.String
            ? element.GetString() : null;
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
            Fail("enrollment-invalid");
        return value;
    }

    private static void ValidateHash(string value)
    {
        if (value is null || value.Length != 64
            || value.Any(character => character is not (>= '0' and <= '9'
                or >= 'a' and <= 'f')))
        {
            Fail("revision-hash-invalid");
        }
    }

    private static bool ParametersEqual(RSAParameters left, RSAParameters right) =>
        left.Modulus is not null && right.Modulus is not null
        && left.Exponent is not null && right.Exponent is not null
        && CryptographicOperations.FixedTimeEquals(left.Modulus, right.Modulus)
        && CryptographicOperations.FixedTimeEquals(left.Exponent, right.Exponent);

    private static string Sha256(byte[] value)
    {
        byte[] hash = SHA256.HashData(value);
        try { return Convert.ToHexStringLower(hash); }
        finally { Zero(hash); }
    }

    private static void Zero(byte[]? value)
    {
        if (value is not null) CryptographicOperations.ZeroMemory(value);
    }

    [DoesNotReturn]
    private static void Fail(string code) =>
        throw new PythonCredentialRotationPreparationException(code);

    private sealed record Enrollment(
        string CredentialId,
        string PrincipalId,
        string TrustPolicyId);
}
