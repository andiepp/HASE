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

    private static readonly string[] AuthorizePropertyWriteValueNames =
    [
        "authorization-policy",
        "expected-authorization-policy-sha256",
        "application-profile",
        "expected-application-profile-sha256",
        "policy-rollback",
        "profile-rollback",
    ];
    private static readonly string[] AuthorizeCommandValueNames =
    ["authorization-policy", "expected-authorization-policy-sha256", "rollback"];
    private static readonly string[] AuthorizeObservationValueNames =
    ["authorization-policy", "expected-authorization-policy-sha256", "rollback"];

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
                case "authorize-property-write":
                {
                    AuthorizePropertyWriteCommand command =
                        ParseAuthorizePropertyWrite(args[1..]);
                    _ = await operations.AuthorizePropertyWriteAsync(
                            command, cancellationToken)
                        .ConfigureAwait(false);
                    output.WriteLine("Operation            : Authorize Property write");
                    output.WriteLine("Outcome              : Succeeded");
                    output.WriteLine("Permission           : property.write");
                    output.WriteLine("Rollback retained    : True");
                    output.WriteLine("Sensitive values     : Withheld");
                    return 0;
                }
                case "authorize-command-execution":
                {
                    AuthorizeCommandExecutionCommand command =
                        ParseAuthorizeCommandExecution(args[1..]);
                    _ = await operations.AuthorizeCommandExecutionAsync(
                        command, cancellationToken).ConfigureAwait(false);
                    output.WriteLine("Operation            : Authorize command execution");
                    output.WriteLine("Outcome              : Succeeded");
                    output.WriteLine("Permission           : command.execute");
                    output.WriteLine("Rollback retained    : True");
                    output.WriteLine("Sensitive values     : Withheld");
                    return 0;
                }
                case "authorize-observation":
                {
                    AuthorizeObservationCommand command =
                        ParseAuthorizeObservation(args[1..]);
                    _ = await operations.AuthorizeObservationAsync(command,
                        cancellationToken).ConfigureAwait(false);
                    output.WriteLine("Operation            : Authorize observation");
                    output.WriteLine("Outcome              : Succeeded");
                    output.WriteLine("Permission           : observation.subscribe");
                    output.WriteLine("Rollback retained    : True");
                    output.WriteLine("Sensitive values     : Withheld");
                    return 0;
                }
                case "authorize-cached-property":
                {
                    ParsedArguments parsed=Parse(args[1..], AuthorizeObservationValueNames,false);
                    string hash=parsed.Values["expected-authorization-policy-sha256"];
                    if(!IsHex(hash,64,false)) throw new ArgumentException();
                    var command=new AuthorizeCachedPropertyCommand(
                        RequireAbsolute(parsed.Values["authorization-policy"]),hash,
                        RequireAbsolute(parsed.Values["rollback"]));
                    _=await operations.AuthorizeCachedPropertyAsync(command,cancellationToken);
                    output.WriteLine("Operation            : Authorize cached Property read");
                    output.WriteLine("Outcome              : Succeeded");
                    output.WriteLine("Permission           : property.cached.read");
                    output.WriteLine("Rollback retained    : True"); return 0;
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
        catch (PythonPropertyWriteAuthorizationException exception)
        {
            return Failure(error, exception.Code);
        }
        catch (PythonCommandExecutionAuthorizationException exception)
        {
            return Failure(error, exception.Code);
        }
        catch (PythonObservationAuthorizationException exception)
        {
            return Failure(error, exception.Code);
        }
        catch (PythonCachedPropertyAuthorizationException exception)
        { return Failure(error,exception.Code); }
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

    private static AuthorizePropertyWriteCommand ParseAuthorizePropertyWrite(
        string[] args)
    {
        ParsedArguments parsed = Parse(
            args, AuthorizePropertyWriteValueNames,
            allowReplacementSwitch: false);
        string hash = parsed.Values["expected-authorization-policy-sha256"];
        string profileHash = parsed.Values["expected-application-profile-sha256"];
        if (!IsHex(hash, 64, allowUppercase: false)
            || !IsHex(profileHash, 64, allowUppercase: false))
        {
            throw new ArgumentException();
        }
        string policy = RequireAbsolute(parsed.Values["authorization-policy"]);
        string profile = RequireAbsolute(parsed.Values["application-profile"]);
        string policyRollback = RequireAbsolute(parsed.Values["policy-rollback"]);
        string profileRollback = RequireAbsolute(parsed.Values["profile-rollback"]);
        RequireDistinct(policy, profile, policyRollback, profileRollback);
        return new AuthorizePropertyWriteCommand(policy, hash, profile,
            profileHash, policyRollback, profileRollback);
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

    private static AuthorizeCommandExecutionCommand ParseAuthorizeCommandExecution(
        string[] args)
    {
        ParsedArguments parsed = Parse(args, AuthorizeCommandValueNames, false);
        string hash = parsed.Values["expected-authorization-policy-sha256"];
        if (!IsHex(hash, 64, false)) throw new ArgumentException();
        string policy = RequireAbsolute(parsed.Values["authorization-policy"]);
        string rollback = RequireAbsolute(parsed.Values["rollback"]);
        RequireDistinct(policy, rollback);
        return new(policy, hash, rollback);
    }

    private static AuthorizeObservationCommand ParseAuthorizeObservation(string[] args)
    {
        ParsedArguments parsed = Parse(args, AuthorizeObservationValueNames, false);
        string hash = parsed.Values["expected-authorization-policy-sha256"];
        if (!IsHex(hash, 64, false)) throw new ArgumentException();
        string policy = RequireAbsolute(parsed.Values["authorization-policy"]);
        string rollback = RequireAbsolute(parsed.Values["rollback"]);
        RequireDistinct(policy, rollback); return new(policy, hash, rollback);
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
        error.WriteLine("  authorize-property-write --authorization-policy <absolute-path> --expected-authorization-policy-sha256 <value> --application-profile <absolute-path> --expected-application-profile-sha256 <value> --policy-rollback <absolute-path> --profile-rollback <absolute-path>");
        error.WriteLine("  authorize-command-execution --authorization-policy <absolute-path> --expected-authorization-policy-sha256 <value> --rollback <absolute-path>");
        error.WriteLine("  authorize-observation --authorization-policy <absolute-path> --expected-authorization-policy-sha256 <value> --rollback <absolute-path>");
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
        DateTimeOffset utcNow = NormalizeCertificateTimestamp(
            DateTimeOffset.UtcNow);
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

    public Task<PythonPropertyWriteAuthorizationResult>
        AuthorizePropertyWriteAsync(
            AuthorizePropertyWriteCommand command,
            CancellationToken cancellationToken) =>
        new PythonPropertyWriteAuthorizer().AuthorizeAsync(
            new PythonPropertyWriteAuthorizationRequest(
                command.AuthorizationPolicyPath,
                command.ExpectedAuthorizationPolicySha256,
                command.ApplicationProfilePath,
                command.ExpectedApplicationProfileSha256,
                command.PolicyRollbackPath,
                command.ProfileRollbackPath),
            cancellationToken);

    public Task<PythonCommandExecutionAuthorizationResult>
        AuthorizeCommandExecutionAsync(AuthorizeCommandExecutionCommand command,
            CancellationToken cancellationToken) =>
        new PythonCommandExecutionAuthorizer().AuthorizeAsync(
            new(command.AuthorizationPolicyPath,
                command.ExpectedAuthorizationPolicySha256,
                command.RollbackPath), cancellationToken);

    public Task<PythonObservationAuthorizationResult> AuthorizeObservationAsync(
        AuthorizeObservationCommand command, CancellationToken cancellationToken) =>
        new PythonObservationAuthorizer().AuthorizeAsync(new(
            command.AuthorizationPolicyPath,
            command.ExpectedAuthorizationPolicySha256,
            command.RollbackPath), cancellationToken);
    public Task<PythonCachedPropertyAuthorizationResult> AuthorizeCachedPropertyAsync(
        AuthorizeCachedPropertyCommand command,CancellationToken token)=>
        new PythonCachedPropertyAuthorizer().AuthorizeAsync(new(command.PolicyPath,
            command.ExpectedSha256,command.RollbackPath),token);

    internal static DateTimeOffset NormalizeCertificateTimestamp(
        DateTimeOffset timestamp)
    {
        DateTimeOffset utc = timestamp.ToUniversalTime();
        long ticks = utc.Ticks - utc.Ticks % TimeSpan.TicksPerSecond;
        return new DateTimeOffset(ticks, TimeSpan.Zero);
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

    Task<PythonPropertyWriteAuthorizationResult> AuthorizePropertyWriteAsync(
        AuthorizePropertyWriteCommand command,
        CancellationToken cancellationToken);
    Task<PythonCommandExecutionAuthorizationResult> AuthorizeCommandExecutionAsync(
        AuthorizeCommandExecutionCommand command,
        CancellationToken cancellationToken);
    Task<PythonObservationAuthorizationResult> AuthorizeObservationAsync(
        AuthorizeObservationCommand command, CancellationToken cancellationToken);
    Task<PythonCachedPropertyAuthorizationResult> AuthorizeCachedPropertyAsync(
        AuthorizeCachedPropertyCommand command,CancellationToken cancellationToken);
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

internal sealed record AuthorizePropertyWriteCommand(
    string AuthorizationPolicyPath,
    string ExpectedAuthorizationPolicySha256,
    string ApplicationProfilePath,
    string ExpectedApplicationProfileSha256,
    string PolicyRollbackPath,
    string ProfileRollbackPath);

internal sealed record AuthorizeCommandExecutionCommand(
    string AuthorizationPolicyPath,
    string ExpectedAuthorizationPolicySha256,
    string RollbackPath);

internal sealed record AuthorizeObservationCommand(
    string AuthorizationPolicyPath,
    string ExpectedAuthorizationPolicySha256,
    string RollbackPath);
internal sealed record AuthorizeCachedPropertyCommand(string PolicyPath,
    string ExpectedSha256,string RollbackPath);

internal sealed record OperatorProvisioningResult(
    string PlanId,
    string TransactionId,
    bool ReplacedOutputs);
