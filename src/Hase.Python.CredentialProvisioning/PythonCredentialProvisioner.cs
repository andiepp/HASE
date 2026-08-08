using System.Net;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace Hase.Python.CredentialProvisioning;

public sealed class PythonCredentialProvisioner
{
    private readonly Action<PythonCredentialProvisioningStep>? stepReached;

    public PythonCredentialProvisioner()
    {
    }

    internal PythonCredentialProvisioner(
        Action<PythonCredentialProvisioningStep> stepReached)
    {
        this.stepReached = stepReached;
    }

    public PythonCredentialProvisioningResult Provision(
        PythonCredentialProvisioningRequest request,
        PythonClientCredentialMaterial material)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(material);

        ValidatedRequest validated = Validate(request);
        ProfileTemplate template = ReadProfileTemplate(validated.SourceProfilePath);
        byte[] profileBytes = CreateProfileBytes(validated, template);
        string transactionId = Guid.NewGuid().ToString("N");
        var entries = new[]
        {
            new TransactionEntry(
                validated.CertificatePath,
                validated.CertificatePath + ".stage-" + transactionId,
                validated.CertificatePath + ".backup-" + transactionId,
                material.CertificatePem,
                sensitive: false),
            new TransactionEntry(
                validated.PrivateKeyPath,
                validated.PrivateKeyPath + ".stage-" + transactionId,
                validated.PrivateKeyPath + ".backup-" + transactionId,
                material.PrivateKeyPem,
                sensitive: true),
            new TransactionEntry(
                validated.ProfilePath,
                validated.ProfilePath + ".stage-" + transactionId,
                validated.ProfilePath + ".backup-" + transactionId,
                profileBytes,
                sensitive: false),
        };

        bool replaced = entries.Any(entry => File.Exists(entry.TargetPath));
        bool publicationCommitted = false;

        try
        {
            Stage(entries);
            Publish(entries);
            publicationCommitted = true;
            DeleteBackups(entries);

            return new PythonCredentialProvisioningResult(
                validated.CertificatePath,
                validated.PrivateKeyPath,
                validated.ProfilePath,
                material.CredentialId,
                replaced);
        }
        catch (PythonCredentialProvisioningException)
        {
            if (!publicationCommitted)
            {
                RollBack(entries);
            }
            throw;
        }
        catch (Exception exception) when (exception is SystemException)
        {
            if (!publicationCommitted)
            {
                RollBack(entries);
            }
            throw new PythonCredentialProvisioningException(
                publicationCommitted
                    ? "backup-cleanup-failed"
                    : "transaction-failed");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(profileBytes);
            DeleteArtifacts(entries);
        }
    }

    private void Stage(IEnumerable<TransactionEntry> entries)
    {
        foreach (TransactionEntry entry in entries)
        {
            if (entry.Sensitive)
            {
                using (new FileStream(
                    entry.StagePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None))
                {
                }
                RestrictPrivateKey(entry.StagePath);
                WriteStagedFile(entry, FileMode.Open);
            }
            else
            {
                WriteStagedFile(entry, FileMode.CreateNew);
            }
        }

        Reach(PythonCredentialProvisioningStep.Staged);
    }

    private static void WriteStagedFile(
        TransactionEntry entry,
        FileMode mode)
    {
        using var stream = new FileStream(
            entry.StagePath,
            mode,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.WriteThrough);
        stream.Write(entry.Content.Span);
        stream.Flush(flushToDisk: true);
    }

    private void Publish(IReadOnlyList<TransactionEntry> entries)
    {
        for (int index = 0; index < entries.Count; index++)
        {
            TransactionEntry entry = entries[index];

            if (File.Exists(entry.TargetPath))
            {
                File.Move(entry.TargetPath, entry.BackupPath);
                entry.BackupCreated = true;
            }

            File.Move(entry.StagePath, entry.TargetPath);
            entry.Published = true;
            Reach((PythonCredentialProvisioningStep)(index + 1));
        }
    }

    private static void RestrictPrivateKey(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            SecurityIdentifier owner =
                WindowsIdentity.GetCurrent().User
                ?? throw new InvalidOperationException();
            var security = new FileSecurity();
            security.SetOwner(owner);
            security.SetAccessRuleProtection(
                isProtected: true,
                preserveInheritance: false);
            security.AddAccessRule(
                new FileSystemAccessRule(
                    owner,
                    FileSystemRights.FullControl,
                    AccessControlType.Allow));
            new FileInfo(path).SetAccessControl(security);
            return;
        }

        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    private void Reach(PythonCredentialProvisioningStep step)
    {
        stepReached?.Invoke(step);
    }

    private static void RollBack(IEnumerable<TransactionEntry> entries)
    {
        foreach (TransactionEntry entry in entries.Reverse())
        {
            try
            {
                if (entry.Published && File.Exists(entry.TargetPath))
                {
                    File.Delete(entry.TargetPath);
                }

                if (entry.BackupCreated && File.Exists(entry.BackupPath))
                {
                    File.Move(entry.BackupPath, entry.TargetPath);
                    entry.BackupCreated = false;
                }
            }
            catch (Exception)
            {
                // Best-effort rollback continues so every original can be restored.
            }
        }
    }

    private static void DeleteBackups(IEnumerable<TransactionEntry> entries)
    {
        foreach (TransactionEntry entry in entries)
        {
            if (entry.BackupCreated)
            {
                File.Delete(entry.BackupPath);
                entry.BackupCreated = false;
            }
        }
    }

    private static void DeleteArtifacts(IEnumerable<TransactionEntry> entries)
    {
        foreach (TransactionEntry entry in entries)
        {
            TryDelete(entry.StagePath);
            if (!entry.BackupCreated)
            {
                TryDelete(entry.BackupPath);
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception)
        {
        }
    }

    private static ValidatedRequest Validate(
        PythonCredentialProvisioningRequest request)
    {
        string root = RequireAbsolute(request.ProvisioningDirectory);
        string source = RequireAbsolute(request.SourceProfilePath);
        string certificate = RequireTarget(root, request.CertificatePath);
        string privateKey = RequireTarget(root, request.PrivateKeyPath);
        string profile = RequireTarget(root, request.ProfilePath);

        if (!Directory.Exists(root) || IsReparsePoint(root))
        {
            Fail("provisioning-directory-invalid");
        }

        if (!File.Exists(source) || IsReparsePoint(source))
        {
            Fail("source-profile-invalid");
        }

        string[] targets = [certificate, privateKey, profile];
        if (targets.Distinct(PathComparer).Count() != targets.Length)
        {
            Fail("target-paths-not-distinct");
        }

        foreach (string target in targets)
        {
            if (Directory.Exists(target)
                || (File.Exists(target) && IsReparsePoint(target))
                || PathContainsReparsePoint(root, target))
            {
                Fail("target-path-invalid");
            }

            if (File.Exists(target) && !request.AllowReplacement)
            {
                Fail("replacement-not-authorized");
            }
        }

        return new ValidatedRequest(
            root,
            source,
            certificate,
            privateKey,
            profile);
    }

    private static string RequireTarget(string root, string value)
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

    private static bool PathContainsReparsePoint(string root, string target)
    {
        string? current = Path.GetDirectoryName(target);
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

    private static ProfileTemplate ReadProfileTemplate(string path)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            try
            {
                using JsonDocument document = JsonDocument.Parse(bytes);
                RejectDuplicateProperties(document.RootElement);
                JsonElement root = document.RootElement;
                RequireProperties(
                    root,
                    "formatVersion",
                    "address",
                    "clientCertificate",
                    "trustedServerCertificate");

                if (root.GetProperty("formatVersion").ValueKind
                        != JsonValueKind.Number
                    || root.GetProperty("formatVersion").GetInt32() != 1)
                {
                    Fail("source-profile-invalid");
                }

                JsonElement client = root.GetProperty("clientCertificate");
                RequireProperties(client, "certificateChainPath", "privateKeyPath");
                JsonElement trusted = root.GetProperty("trustedServerCertificate");
                RequireProperties(trusted, "certificatePath");

                string address = RequireJsonString(root.GetProperty("address"));
                string oldCertificatePath = RequireJsonString(
                    client.GetProperty("certificateChainPath"));
                string oldPrivateKeyPath = RequireJsonString(
                    client.GetProperty("privateKeyPath"));
                string trustedPath = RequireJsonString(trusted.GetProperty("certificatePath"));
                if (!IsStrictAddress(address)
                    || !IsAvailableAbsoluteFile(oldCertificatePath)
                    || !IsAvailableAbsoluteFile(oldPrivateKeyPath)
                    || !IsAvailableAbsoluteFile(trustedPath)
                    || new[] { oldCertificatePath, oldPrivateKeyPath, trustedPath }
                        .Select(Path.GetFullPath)
                        .Distinct(PathComparer)
                        .Count() != 3)
                {
                    Fail("source-profile-invalid");
                }

                return new ProfileTemplate(address, Path.GetFullPath(trustedPath));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
            }
        }
        catch (PythonCredentialProvisioningException)
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
            throw new PythonCredentialProvisioningException(
                "source-profile-invalid");
        }
    }

    private static byte[] CreateProfileBytes(
        ValidatedRequest request,
        ProfileTemplate template)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(
            stream,
            new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("formatVersion", 1);
            writer.WriteString("address", template.Address);
            writer.WriteStartObject("clientCertificate");
            writer.WriteString("certificateChainPath", request.CertificatePath);
            writer.WriteString("privateKeyPath", request.PrivateKeyPath);
            writer.WriteEndObject();
            writer.WriteStartObject("trustedServerCertificate");
            writer.WriteString("certificatePath", template.TrustedServerCertificatePath);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    Fail("source-profile-invalid");
                }
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                RejectDuplicateProperties(item);
            }
        }
    }

    private static void RequireProperties(
        JsonElement element,
        params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.EnumerateObject()
                .Select(property => property.Name)
                .Order(StringComparer.Ordinal)
                .SequenceEqual(names.Order(StringComparer.Ordinal)))
        {
            Fail("source-profile-invalid");
        }
    }

    private static string RequireJsonString(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(element.GetString()))
        {
            Fail("source-profile-invalid");
        }
        return element.GetString()!;
    }

    private static bool IsStrictAddress(string address)
    {
        if (address != address.Trim()
            || !Uri.TryCreate(address, UriKind.Absolute, out Uri? uri)
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

    private static bool IsAvailableAbsoluteFile(string path) =>
        Path.IsPathFullyQualified(path)
        && File.Exists(path)
        && !IsReparsePoint(path);

    private static void Fail(string code) =>
        throw new PythonCredentialProvisioningException(code);

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private sealed record ValidatedRequest(
        string ProvisioningDirectory,
        string SourceProfilePath,
        string CertificatePath,
        string PrivateKeyPath,
        string ProfilePath);

    private sealed record ProfileTemplate(
        string Address,
        string TrustedServerCertificatePath);

    private sealed class TransactionEntry(
        string targetPath,
        string stagePath,
        string backupPath,
        ReadOnlyMemory<byte> content,
        bool sensitive)
    {
        public string TargetPath { get; } = targetPath;
        public string StagePath { get; } = stagePath;
        public string BackupPath { get; } = backupPath;
        public ReadOnlyMemory<byte> Content { get; } = content;
        public bool Sensitive { get; } = sensitive;
        public bool Published { get; set; }
        public bool BackupCreated { get; set; }
    }
}

internal enum PythonCredentialProvisioningStep
{
    CertificatePublished = 1,
    PrivateKeyPublished = 2,
    ProfilePublished = 3,
    Staged = 4,
}
