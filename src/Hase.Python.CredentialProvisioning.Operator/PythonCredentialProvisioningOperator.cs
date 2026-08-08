using System.Globalization;
using System.Security.Cryptography.X509Certificates;

namespace Hase.Python.CredentialProvisioning.Operator;

internal static class PythonCredentialProvisioningOperator
{
    private static readonly string[] ProvisionValueNames =
    [
        "signing-root-thumbprint",
        "trust-policy-id",
        "source-profile",
        "provisioning-directory",
        "certificate",
        "private-key",
        "profile",
        "enrollment",
        "authorization-policy",
        "expected-authorization-policy-sha256",
        "validity-days",
    ];

    private static readonly string[] RecoveryValueNames =
    [
        "provisioning-directory",
        "certificate",
        "private-key",
        "profile",
        "enrollment",
        "authorization-policy",
    ];

    internal static async Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        IPythonCredentialProvisioningOperations operations,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(operations);

        try
        {
            if (args.Length == 0)
            {
                return Usage(error);
            }

            switch (args[0])
            {
                case "provision":
                {
                    ProvisionCommand command = ParseProvision(args[1..]);
                    OperatorProvisioningResult result =
                        await operations.ProvisionAsync(command, cancellationToken)
                            .ConfigureAwait(false);
                    output.WriteLine("Operation            : Provision");
                    output.WriteLine("Outcome              : Succeeded");
                    output.WriteLine($"Plan ID              : {result.PlanId}");
                    output.WriteLine(
                        $"Transaction ID       : {result.TransactionId}");
                    output.WriteLine(
                        $"Replaced outputs     : {result.ReplacedOutputs}");
                    output.WriteLine("Sensitive values     : Withheld");
                    return 0;
                }
                case "recover":
                {
                    RecoveryCommand command = ParseRecovery(args[1..]);
                    PythonCredentialProvisioningRecoveryResult result =
                        operations.Recover(command);
                    output.WriteLine("Operation            : Recover");
                    output.WriteLine($"Outcome              : {result.Disposition}");
                    output.WriteLine("Sensitive values     : Withheld");
                    return 0;
                }
                default:
                    return Usage(error);
            }
        }
        catch (OperationCanceledException)
        {
            error.WriteLine("Operation canceled.");
            return 4;
        }
        catch (ArgumentException)
        {
            error.WriteLine("Invalid command arguments.");
            return 2;
        }
        catch (PythonCredentialProvisioningPlanException exception)
        {
            return Failure(error, exception.Code);
        }
        catch (PythonCredentialProvisioningPreparationException exception)
        {
            return Failure(error, exception.Code);
        }
        catch (PythonCredentialProvisioningPublicationException exception)
        {
            return Failure(error, exception.Code);
        }
        catch (PythonCredentialProvisioningRecoveryException exception)
        {
            return Failure(error, exception.Code);
        }
        catch (Exception exception) when (exception is SystemException)
        {
            return Failure(error, "operation-failed");
        }
    }

    private static ProvisionCommand ParseProvision(string[] args)
    {
        ParsedArguments parsed = Parse(
            args, ProvisionValueNames, allowReplacementSwitch: true);
        string thumbprint = parsed.Values["signing-root-thumbprint"];
        string policyHash =
            parsed.Values["expected-authorization-policy-sha256"];
        if (!IsHex(thumbprint, 40, allowUppercase: true)
            || !IsHex(policyHash, 64, allowUppercase: false))
        {
            throw new ArgumentException();
        }
        if (!int.TryParse(parsed.Values["validity-days"],
                NumberStyles.None, CultureInfo.InvariantCulture,
                out int validityDays)
            || validityDays < 1
            || validityDays > 90)
        {
            throw new ArgumentException();
        }

        string sourceProfile = RequireAbsolute(parsed.Values["source-profile"]);
        string directory =
            RequireAbsolute(parsed.Values["provisioning-directory"]);
        string certificate = RequireAbsolute(parsed.Values["certificate"]);
        string privateKey = RequireAbsolute(parsed.Values["private-key"]);
        string profile = RequireAbsolute(parsed.Values["profile"]);
        string enrollment = RequireAbsolute(parsed.Values["enrollment"]);
        string policy = RequireAbsolute(parsed.Values["authorization-policy"]);
        RequireDistinct(
            sourceProfile, certificate, privateKey, profile, enrollment, policy);

        return new ProvisionCommand(
            thumbprint,
            parsed.Values["trust-policy-id"],
            sourceProfile,
            directory,
            certificate,
            privateKey,
            profile,
            enrollment,
            policy,
            policyHash,
            TimeSpan.FromDays(validityDays),
            parsed.AllowReplacement);
    }

    private static RecoveryCommand ParseRecovery(string[] args)
    {
        ParsedArguments parsed = Parse(
            args, RecoveryValueNames, allowReplacementSwitch: false);
        string certificate = RequireAbsolute(parsed.Values["certificate"]);
        string privateKey = RequireAbsolute(parsed.Values["private-key"]);
        string profile = RequireAbsolute(parsed.Values["profile"]);
        string enrollment = RequireAbsolute(parsed.Values["enrollment"]);
        string policy = RequireAbsolute(parsed.Values["authorization-policy"]);
        RequireDistinct(certificate, privateKey, profile, enrollment, policy);
        return new RecoveryCommand(
            RequireAbsolute(parsed.Values["provisioning-directory"]),
            certificate,
            privateKey,
            profile,
            enrollment,
            policy);
    }

    private static ParsedArguments Parse(
        string[] args,
        IReadOnlyCollection<string> valueNames,
        bool allowReplacementSwitch)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        bool replacement = false;
        for (int index = 0; index < args.Length; index++)
        {
            string token = args[index];
            if (token == "--allow-replacement" && allowReplacementSwitch)
            {
                if (replacement) throw new ArgumentException();
                replacement = true;
                continue;
            }
            if (!token.StartsWith("--", StringComparison.Ordinal)
                || !valueNames.Contains(token[2..], StringComparer.Ordinal)
                || index + 1 >= args.Length
                || args[index + 1].StartsWith("--", StringComparison.Ordinal)
                || !values.TryAdd(token[2..], RequireValue(args[++index])))
            {
                throw new ArgumentException();
            }
        }
        if (values.Count != valueNames.Count)
        {
            throw new ArgumentException();
        }
        return new ParsedArguments(values, replacement);
    }

    private static string RequireValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
        {
            throw new ArgumentException();
        }
        return value;
    }

    private static string RequireAbsolute(string path)
    {
        if (!Path.IsPathFullyQualified(path)) throw new ArgumentException();
        return Path.GetFullPath(path);
    }

    private static void RequireDistinct(params string[] paths)
    {
        StringComparer comparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        if (paths.Distinct(comparer).Count() != paths.Length)
        {
            throw new ArgumentException();
        }
    }

    private static bool IsHex(
        string value,
        int length,
        bool allowUppercase) =>
        value.Length == length
        && value.All(character =>
            character is >= '0' and <= '9'
            || character is >= 'a' and <= 'f'
            || (allowUppercase && character is >= 'A' and <= 'F'));

    private static int Usage(TextWriter error)
    {
        error.WriteLine("Usage:");
        error.WriteLine("  provision --signing-root-thumbprint <value> --trust-policy-id <value> --source-profile <absolute-path> --provisioning-directory <absolute-path> --certificate <absolute-path> --private-key <absolute-path> --profile <absolute-path> --enrollment <absolute-path> --authorization-policy <absolute-path> --expected-authorization-policy-sha256 <value> --validity-days <1-90> [--allow-replacement]");
        error.WriteLine("  recover --provisioning-directory <absolute-path> --certificate <absolute-path> --private-key <absolute-path> --profile <absolute-path> --enrollment <absolute-path> --authorization-policy <absolute-path>");
        return 2;
    }

    private static int Failure(TextWriter error, string code)
    {
        error.WriteLine($"Operation failed. Error code: {code}");
        return 3;
    }

    private sealed record ParsedArguments(
        IReadOnlyDictionary<string, string> Values,
        bool AllowReplacement);
}

internal sealed class SystemPythonCredentialProvisioningOperations
    : IPythonCredentialProvisioningOperations
{
    public async Task<OperatorProvisioningResult> ProvisionAsync(
        ProvisionCommand command,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException();
        }
        cancellationToken.ThrowIfCancellationRequested();
        DateTimeOffset utcNow = DateTimeOffset.UtcNow;
        using PythonClientCredentialMaterial material = CreateMaterial(
            command.SigningRootThumbprint, utcNow, command.Validity);
        var request = new PythonCredentialProvisioningPlanRequest(
            command.SigningRootThumbprint,
            material.CredentialId,
            "hase-python-automation",
            command.TrustPolicyId,
            command.SourceProfilePath,
            command.ProvisioningDirectory,
            command.CertificatePath,
            command.PrivateKeyPath,
            command.ProfilePath,
            command.EnrollmentPath,
            command.AuthorizationPolicyPath,
            command.ExpectedAuthorizationPolicySha256,
            command.Validity,
            command.AllowReplacement);
        PythonCredentialProvisioningPlan plan =
            await new PythonCredentialProvisioningPlanBuilder()
                .CreateAsync(request, utcNow, cancellationToken)
                .ConfigureAwait(false);
        using PythonCredentialProvisioningCandidates candidates =
            await new PythonCredentialProvisioningPreparer()
                .PrepareAsync(plan, material, cancellationToken)
                .ConfigureAwait(false);
        PythonCredentialProvisioningPublicationResult publication =
            await new PythonCredentialProvisioningPublisher()
                .PublishAsync(plan, candidates, cancellationToken)
                .ConfigureAwait(false);
        return new OperatorProvisioningResult(
            plan.PlanId,
            publication.TransactionId,
            publication.ReplacedCredentialOutputs);
    }

    public PythonCredentialProvisioningRecoveryResult Recover(
        RecoveryCommand command)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException();
        }
        return new PythonCredentialProvisioningRecoverer().Recover(
            new PythonCredentialProvisioningRecoveryRequest(
                command.ProvisioningDirectory,
                command.CertificatePath,
                command.PrivateKeyPath,
                command.ProfilePath,
                command.EnrollmentPath,
                command.AuthorizationPolicyPath));
    }

    private static PythonClientCredentialMaterial CreateMaterial(
        string thumbprint,
        DateTimeOffset utcNow,
        TimeSpan validity)
    {
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);
        X509Certificate2[] matches = store.Certificates
            .Find(X509FindType.FindByThumbprint, thumbprint, validOnly: false)
            .OfType<X509Certificate2>()
            .Where(certificate => certificate.HasPrivateKey)
            .ToArray();
        try
        {
            if (matches.Length != 1)
            {
                throw new InvalidOperationException();
            }
            return PythonClientCredentialFactory.Create(
                matches[0], utcNow, validity);
        }
        finally
        {
            foreach (X509Certificate2 certificate in matches)
            {
                certificate.Dispose();
            }
        }
    }
}

internal interface IPythonCredentialProvisioningOperations
{
    Task<OperatorProvisioningResult> ProvisionAsync(
        ProvisionCommand command,
        CancellationToken cancellationToken);

    PythonCredentialProvisioningRecoveryResult Recover(RecoveryCommand command);
}

internal sealed record ProvisionCommand(
    string SigningRootThumbprint,
    string TrustPolicyId,
    string SourceProfilePath,
    string ProvisioningDirectory,
    string CertificatePath,
    string PrivateKeyPath,
    string ProfilePath,
    string EnrollmentPath,
    string AuthorizationPolicyPath,
    string ExpectedAuthorizationPolicySha256,
    TimeSpan Validity,
    bool AllowReplacement);

internal sealed record RecoveryCommand(
    string ProvisioningDirectory,
    string CertificatePath,
    string PrivateKeyPath,
    string ProfilePath,
    string EnrollmentPath,
    string AuthorizationPolicyPath);

internal sealed record OperatorProvisioningResult(
    string PlanId,
    string TransactionId,
    bool ReplacedOutputs);
