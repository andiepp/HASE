using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Hase.Runtime.Remote.Grpc.Adapter;

namespace Hase.Python.CredentialProvisioning;

public sealed class PythonCredentialProvisioningPreparer
{
    private const string LocalPrincipalId = "hase-python-automation";
    private const string LaptopMiniPcPrincipalId = "hase-laptop-python-minipc";
    private const string ClientAuthenticationOid = "1.3.6.1.5.5.7.3.2";
    private const string Sha256RsaOid = "1.2.840.113549.1.1.11";

    public async Task<PythonCredentialProvisioningCandidates> PrepareAsync(
        PythonCredentialProvisioningPlan plan,
        PythonClientCredentialMaterial material,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(material);

        using var personalStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        using var trustedStore = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
        personalStore.Open(OpenFlags.ReadOnly);
        trustedStore.Open(OpenFlags.ReadOnly);

        return await PrepareAsync(
            plan,
            material,
            personalStore.Certificates.Cast<X509Certificate2>(),
            trustedStore.Certificates.Cast<X509Certificate2>(),
            cancellationToken).ConfigureAwait(false);
    }

    internal async Task<PythonCredentialProvisioningCandidates> PrepareAsync(
        PythonCredentialProvisioningPlan plan,
        PythonClientCredentialMaterial material,
        IEnumerable<X509Certificate2> personalCertificates,
        IEnumerable<X509Certificate2> trustedRoots,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(material);
        ArgumentNullException.ThrowIfNull(personalCertificates);
        ArgumentNullException.ThrowIfNull(trustedRoots);
        ValidatePlan(plan);

        byte[]? certificate = null;
        byte[]? privateKey = null;
        byte[]? sourceProfile = null;
        byte[]? sourceEnrollment = null;
        byte[]? sourcePolicy = null;
        byte[]? profile = null;
        byte[]? enrollment = null;
        byte[]? policy = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.Equals(material.CredentialId, plan.CredentialId,
                    StringComparison.Ordinal))
            {
                Fail("credential-id-mismatch");
            }

            certificate = material.CertificatePem.ToArray();
            privateKey = material.PrivateKeyPem.ToArray();
            sourceProfile = await ReadLockedAsync(
                plan.SourceProfilePath,
                plan.SourceProfileSha256,
                cancellationToken).ConfigureAwait(false);
            sourceEnrollment = await ReadLockedAsync(
                plan.EnrollmentPath,
                plan.EnrollmentSha256,
                cancellationToken).ConfigureAwait(false);
            sourcePolicy = await ReadLockedAsync(
                plan.AuthorizationPolicyPath,
                plan.AuthorizationPolicySha256,
                cancellationToken).ConfigureAwait(false);

            X509Certificate2 personalRoot = SelectExactRoot(
                plan.SigningRootThumbprint,
                personalCertificates,
                requirePrivateKey: true);
            X509Certificate2 trustedRoot = SelectExactRoot(
                plan.SigningRootThumbprint,
                trustedRoots,
                requirePrivateKey: false);
            if (!personalRoot.RawData.AsSpan().SequenceEqual(trustedRoot.RawData))
            {
                Fail("signing-root-mismatch");
            }
            if (!string.Equals(
                CalculatePlanId(plan, trustedRoot),
                plan.PlanId,
                StringComparison.Ordinal))
            {
                Fail("plan-revision-invalid");
            }

            ValidateCredential(plan, certificate, privateKey, trustedRoot);
            PythonRuntimeHostProfileDocument sourceProfileDocument =
                PythonRuntimeHostProfileDocument.Load(sourceProfile);
            _ = RuntimeHostClientCredentialEnrollmentRegistryFile.Load(sourceEnrollment);
            _ = RuntimeHostAuthorizationPolicyFile.Load(sourcePolicy);
            RejectExistingPythonAuthority(
                sourceEnrollment, sourcePolicy, plan.PrincipalId);

            profile = new PythonRuntimeHostProfileDocument(
                sourceProfileDocument.Address,
                plan.CertificatePath,
                plan.PrivateKeyPath,
                sourceProfileDocument.TrustedServerCertificatePath).Serialize();
            enrollment = CreateEnrollmentCandidate(sourceEnrollment, plan);
            policy = CreatePolicyCandidate(sourcePolicy, plan);

            _ = PythonRuntimeHostProfileDocument.Load(profile);
            _ = RuntimeHostClientCredentialEnrollmentRegistryFile.Load(enrollment);
            _ = RuntimeHostAuthorizationPolicyFile.Load(policy);

            var result = new PythonCredentialProvisioningCandidates(
                certificate,
                privateKey,
                profile,
                enrollment,
                policy);
            certificate = null;
            privateKey = null;
            profile = null;
            enrollment = null;
            policy = null;
            return result;
        }
        catch (PythonCredentialProvisioningPreparationException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException
            or UnauthorizedAccessException
            or CryptographicException
            or InvalidDataException
            or JsonException
            or ArgumentException
            or InvalidOperationException)
        {
            throw new PythonCredentialProvisioningPreparationException(
                "preparation-input-invalid");
        }
        finally
        {
            Zero(certificate);
            Zero(privateKey);
            Zero(sourceProfile);
            Zero(sourceEnrollment);
            Zero(sourcePolicy);
            Zero(profile);
            Zero(enrollment);
            Zero(policy);
        }
    }

    internal static void ValidatePlan(PythonCredentialProvisioningPlan plan)
    {
        if ((plan.PrincipalId != LocalPrincipalId
                && plan.PrincipalId != LaptopMiniPcPrincipalId)
            || plan.LeafRsaKeySize != 2048
            || plan.LeafSignatureAlgorithm != "sha256WithRSAEncryption"
            || !plan.LeafEnhancedKeyUsages.SequenceEqual([ClientAuthenticationOid])
            || !plan.AuthorizationGrants.SequenceEqual([
                RuntimeHostPermission.ReadSnapshot.Value,
                RuntimeHostPermission.ReadAuthoritativeProperty.Value])
            || plan.NotBeforeUtc.Offset != TimeSpan.Zero
            || plan.NotAfterUtc.Offset != TimeSpan.Zero
            || plan.NotAfterUtc <= plan.NotBeforeUtc)
        {
            Fail("plan-invalid");
        }
    }

    internal static string CalculatePlanId(
        PythonCredentialProvisioningPlan plan,
        X509Certificate2 signingRoot)
    {
        byte[] rootHashBytes = SHA256.HashData(signingRoot.RawData);
        string rootHash;
        try
        {
            rootHash = Convert.ToHexStringLower(rootHashBytes);
        }
        finally
        {
            Zero(rootHashBytes);
        }

        string canonical = string.Join(
            "\n",
            plan.CredentialId,
            plan.PrincipalId,
            plan.TrustPolicyId,
            plan.SourceProfilePath,
            plan.ProvisioningDirectory,
            plan.CertificatePath,
            plan.PrivateKeyPath,
            plan.ProfilePath,
            plan.EnrollmentPath,
            plan.AuthorizationPolicyPath,
            plan.SourceProfileSha256,
            plan.EnrollmentSha256,
            plan.AuthorizationPolicySha256,
            plan.NotBeforeUtc.ToString("O"),
            plan.NotAfterUtc.ToString("O"),
            plan.AllowReplacement
                ? "allowReplacement=true"
                : "allowReplacement=false",
            rootHash);
        byte[] canonicalBytes = Encoding.UTF8.GetBytes(canonical);
        byte[]? planHash = null;
        try
        {
            planHash = SHA256.HashData(canonicalBytes);
            return "python-provisioning-plan-sha256:"
                + Convert.ToHexStringLower(planHash);
        }
        finally
        {
            Zero(canonicalBytes);
            Zero(planHash);
        }
    }

    private static async Task<byte[]> ReadLockedAsync(
        string path,
        string expectedHash,
        CancellationToken cancellationToken)
    {
        byte[] bytes = await File.ReadAllBytesAsync(path, cancellationToken)
            .ConfigureAwait(false);
        byte[] hash = SHA256.HashData(bytes);
        try
        {
            if (!string.Equals(
                Convert.ToHexStringLower(hash),
                expectedHash,
                StringComparison.Ordinal))
            {
                Zero(bytes);
                Fail("input-revision-mismatch");
            }
            return bytes;
        }
        finally
        {
            Zero(hash);
        }
    }

    internal static X509Certificate2 SelectExactRoot(
        string thumbprint,
        IEnumerable<X509Certificate2> certificates,
        bool requirePrivateKey)
    {
        X509Certificate2[] matches = certificates
            .Where(certificate => string.Equals(
                certificate.Thumbprint,
                thumbprint,
                StringComparison.OrdinalIgnoreCase)
                && (!requirePrivateKey || certificate.HasPrivateKey))
            .ToArray();
        if (matches.Length != 1)
        {
            Fail("signing-root-unavailable");
        }
        return matches[0];
    }

    internal static void ValidateCredential(
        PythonCredentialProvisioningPlan plan,
        byte[] certificatePem,
        byte[] privateKeyPem,
        X509Certificate2 trustedRoot)
    {
        byte[] certificateDer = DecodePem(certificatePem, "CERTIFICATE");
        byte[] privateKeyDer = DecodePem(privateKeyPem, "PRIVATE KEY");
        try
        {
            using X509Certificate2 leaf =
                X509CertificateLoader.LoadCertificate(certificateDer);
            using RSA privateKey = RSA.Create();
            privateKey.ImportPkcs8PrivateKey(privateKeyDer, out int bytesRead);
            if (bytesRead != privateKeyDer.Length)
            {
                Fail("credential-material-invalid");
            }
            using RSA? publicKey = leaf.GetRSAPublicKey();
            if (publicKey is null
                || publicKey.KeySize != 2048
                || privateKey.KeySize != 2048
                || leaf.SignatureAlgorithm.Value != Sha256RsaOid
                || !ParametersEqual(publicKey.ExportParameters(false),
                    privateKey.ExportParameters(false))
                || !CredentialId(leaf).Equals(plan.CredentialId,
                    StringComparison.Ordinal)
                || leaf.NotBefore.ToUniversalTime() != plan.NotBeforeUtc.UtcDateTime
                || leaf.NotAfter.ToUniversalTime() != plan.NotAfterUtc.UtcDateTime)
            {
                Fail("credential-material-invalid");
            }

            X509BasicConstraintsExtension basic =
                SingleExtension<X509BasicConstraintsExtension>(leaf, "2.5.29.19");
            X509KeyUsageExtension usage =
                SingleExtension<X509KeyUsageExtension>(leaf, "2.5.29.15");
            X509EnhancedKeyUsageExtension eku =
                SingleExtension<X509EnhancedKeyUsageExtension>(leaf, "2.5.29.37");
            if (!basic.Critical
                || basic.CertificateAuthority
                || !usage.Critical
                || usage.KeyUsages != X509KeyUsageFlags.DigitalSignature
                || !eku.Critical
                || eku.EnhancedKeyUsages.Count != 1
                || eku.EnhancedKeyUsages[0]?.Value != ClientAuthenticationOid)
            {
                Fail("credential-material-invalid");
            }

            using var chain = new X509Chain();
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(trustedRoot);
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.VerificationTime =
                plan.NotBeforeUtc.AddSeconds(1).UtcDateTime;
            if (!chain.Build(leaf)
                || chain.ChainElements.Count != 2
                || !chain.ChainElements[1].Certificate.RawData.AsSpan()
                    .SequenceEqual(trustedRoot.RawData))
            {
                Fail("credential-chain-invalid");
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
        byte[] header = Encoding.ASCII.GetBytes($"-----BEGIN {label}-----");
        byte[] footer = Encoding.ASCII.GetBytes($"-----END {label}-----");
        byte[]? compact = null;
        byte[]? decoded = null;
        try
        {
            int headerIndex = pem.AsSpan().IndexOf(header);
            int contentStart = headerIndex < 0 ? -1 : headerIndex + header.Length;
            int footerOffset = contentStart < 0
                ? -1
                : pem.AsSpan(contentStart).IndexOf(footer);
            int footerEnd = footerOffset < 0
                ? -1
                : contentStart + footerOffset + footer.Length;
            if (headerIndex != 0
                || footerOffset < 0
                || HasNonLineEnding(pem.AsSpan(footerEnd)))
            {
                Fail("credential-material-invalid");
            }

            ReadOnlySpan<byte> body = pem.AsSpan(contentStart, footerOffset);
            compact = new byte[body.Length];
            int compactLength = 0;
            foreach (byte value in body)
            {
                if (value is not (byte)'\r' and not (byte)'\n'
                    and not (byte)' ' and not (byte)'\t')
                {
                    compact[compactLength++] = value;
                }
            }

            decoded = new byte[compactLength];
            OperationStatus status = Base64.DecodeFromUtf8(
                compact.AsSpan(0, compactLength),
                decoded,
                out int consumed,
                out int written);
            if (status != OperationStatus.Done || consumed != compactLength)
            {
                Fail("credential-material-invalid");
            }

            byte[] result = decoded.AsSpan(0, written).ToArray();
            return result;
        }
        finally
        {
            Zero(header);
            Zero(footer);
            Zero(compact);
            Zero(decoded);
        }
    }

    private static bool HasNonLineEnding(ReadOnlySpan<byte> bytes)
    {
        foreach (byte value in bytes)
        {
            if (value is not (byte)'\r' and not (byte)'\n')
            {
                return true;
            }
        }
        return false;
    }

    private static T SingleExtension<T>(X509Certificate2 certificate, string oid)
        where T : X509Extension
    {
        X509Extension[] matches = certificate.Extensions.Cast<X509Extension>()
            .Where(extension => extension.Oid?.Value == oid).ToArray();
        if (matches.Length != 1)
        {
            Fail("credential-material-invalid");
        }
        X509Extension source = matches[0];
        X509Extension decoded = source switch
        {
            T typed => typed,
            _ when typeof(T) == typeof(X509BasicConstraintsExtension) =>
                new X509BasicConstraintsExtension(source, source.Critical),
            _ when typeof(T) == typeof(X509KeyUsageExtension) =>
                new X509KeyUsageExtension(source, source.Critical),
            _ when typeof(T) == typeof(X509EnhancedKeyUsageExtension) =>
                new X509EnhancedKeyUsageExtension(source, source.Critical),
            _ => throw new InvalidOperationException(),
        };
        return (T)decoded;
    }

    private static bool ParametersEqual(RSAParameters left, RSAParameters right) =>
        left.Modulus.AsSpan().SequenceEqual(right.Modulus)
        && left.Exponent.AsSpan().SequenceEqual(right.Exponent);

    private static string CredentialId(X509Certificate2 certificate)
    {
        byte[] hash = SHA256.HashData(certificate.RawData);
        try
        {
            return "x509-sha256:" + Convert.ToHexStringLower(hash);
        }
        finally
        {
            Zero(hash);
        }
    }

    private static void RejectExistingPythonAuthority(
        byte[] enrollment,
        byte[] policy,
        string principalId)
    {
        using JsonDocument enrollmentDocument = JsonDocument.Parse(enrollment);
        using JsonDocument policyDocument = JsonDocument.Parse(policy);
        bool existingEnrollment = enrollmentDocument.RootElement
            .GetProperty("enrollments").EnumerateArray()
            .Any(entry => entry.GetProperty("principalId").GetString() == principalId);
        bool existingGrant = policyDocument.RootElement.GetProperty("grants")
            .EnumerateArray()
            .Any(entry => entry.GetProperty("principalId").GetString() == principalId);
        if (existingEnrollment || existingGrant)
        {
            Fail("python-authority-already-present");
        }
    }

    private static byte[] CreateEnrollmentCandidate(
        byte[] source,
        PythonCredentialProvisioningPlan plan)
    {
        using JsonDocument document = JsonDocument.Parse(source);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("formatVersion", 1);
            writer.WriteStartArray("enrollments");
            foreach (JsonElement entry in document.RootElement
                .GetProperty("enrollments").EnumerateArray())
            {
                entry.WriteTo(writer);
            }
            writer.WriteStartObject();
            writer.WriteString("credentialId", plan.CredentialId);
            writer.WriteString("principalId", plan.PrincipalId);
            writer.WriteString("trustPolicyId", plan.TrustPolicyId);
            writer.WriteEndObject();
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    private static byte[] CreatePolicyCandidate(
        byte[] source,
        PythonCredentialProvisioningPlan plan)
    {
        using JsonDocument document = JsonDocument.Parse(source);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("formatVersion", 1);
            writer.WriteStartArray("grants");
            foreach (JsonElement entry in document.RootElement.GetProperty("grants")
                .EnumerateArray())
            {
                entry.WriteTo(writer);
            }
            foreach (string permission in plan.AuthorizationGrants)
            {
                writer.WriteStartObject();
                writer.WriteString("principalId", plan.PrincipalId);
                writer.WriteString("permission", permission);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    private static void Zero(byte[]? bytes)
    {
        if (bytes is not null)
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static void Fail(string code) =>
        throw new PythonCredentialProvisioningPreparationException(code);
}
