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
    private static readonly string[] RotationPublicationValueNames =
    [
        "provisioning-directory", "certificate", "private-key", "profile",
        "enrollment", "authorization-policy", "expected-certificate-sha256",
        "expected-private-key-sha256", "expected-profile-sha256",
        "expected-enrollment-sha256",
        "expected-authorization-policy-sha256",
    ];
    private static readonly string[] RotationBeginValueNames =
        RotationPublicationValueNames.Concat(new[]
        {
            "signing-root-thumbprint", "validity-days", "principal-id",
            "trust-policy-id", "expected-grants", "expected-current-credential-id",
            "expected-trusted-server-certificate-sha256",
        }).ToArray();

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
                    ProvisionCommand command = ParseProvision(
                        args[1..], "hase-python-automation");
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
                case "provision-laptop-minipc":
                {
                    ProvisionCommand command = ParseProvision(
                        args[1..], "hase-laptop-python-minipc");
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
                case "rotate-begin":
                {
                    RotationBeginCommand command = ParseRotationBegin(args[1..]);
                    PythonCredentialRotationPublicationResult result =
                        await operations.BeginRotationAsync(command,
                            cancellationToken).ConfigureAwait(false);
                    output.WriteLine("Operation            : Begin credential rotation");
                    output.WriteLine("Outcome              : Succeeded");
                    output.WriteLine($"Transaction ID       : {result.TransactionId}");
                    output.WriteLine($"Disposition          : {result.Disposition}");
                    output.WriteLine($"Rollback retained    : {result.RollbackRetained}");
                    output.WriteLine("Sensitive values     : Withheld");
                    return 0;
                }
                case "rotate-finalize":
                {
                    RotationPublicationCommand command =
                        ParseRotationPublication(args[1..]);
                    PythonCredentialRotationPublicationResult result =
                        operations.FinalizeRotation(command);
                    output.WriteLine("Operation            : Finalize credential rotation");
                    output.WriteLine("Outcome              : Succeeded");
                    output.WriteLine($"Transaction ID       : {result.TransactionId}");
                    output.WriteLine($"Disposition          : {result.Disposition}");
                    output.WriteLine("Rollback retained    : False");
                    output.WriteLine("Sensitive values     : Withheld");
                    return 0;
                }
                case "rotate-recover":
                {
                    RotationPublicationCommand command =
                        ParseRotationPublication(args[1..]);
                    PythonCredentialRotationPublicationResult result =
                        operations.RecoverRotation(command);
                    output.WriteLine("Operation            : Recover credential rotation");
                    output.WriteLine("Outcome              : Succeeded");
                    output.WriteLine($"Disposition          : {result.Disposition}");
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
                case "authorize-laptop-minipc-command-execution":
                {
                    AuthorizeObservationCommand command =
                        ParseAuthorizeObservation(args[1..]);
                    _ = await new PythonLaptopMiniPcCommandExecutionAuthorizer()
                        .AuthorizeAsync(
                            new(
                                command.AuthorizationPolicyPath,
                                command.ExpectedAuthorizationPolicySha256,
                                command.RollbackPath),
                            cancellationToken)
                        .ConfigureAwait(false);
                    output.WriteLine("Operation            : Authorize Laptop MiniPC Command execution");
                    output.WriteLine("Outcome              : Succeeded");
                    output.WriteLine("Principal            : hase-laptop-python-minipc");
                    output.WriteLine("Permission           : command.execute");
                    output.WriteLine("Rollback retained    : True");
                    output.WriteLine("Sensitive values     : Withheld");
                    return 0;
                }

                case "authorize-laptop-minipc-property-write":
                {
                    AuthorizeObservationCommand command =
                        ParseAuthorizeObservation(args[1..]);
                    _ = await new PythonLaptopMiniPcPropertyWriteAuthorizer()
                        .AuthorizeAsync(
                            new(
                                command.AuthorizationPolicyPath,
                                command.ExpectedAuthorizationPolicySha256,
                                command.RollbackPath),
                            cancellationToken)
                        .ConfigureAwait(false);
                    output.WriteLine("Operation            : Authorize Laptop MiniPC Property write");
                    output.WriteLine("Outcome              : Succeeded");
                    output.WriteLine("Principal            : hase-laptop-python-minipc");
                    output.WriteLine("Permission           : property.write");
                    output.WriteLine("Rollback retained    : True");
                    output.WriteLine("Sensitive values     : Withheld");
                    return 0;
                }

                case "authorize-laptop-minipc-observation":
                {
                    AuthorizeObservationCommand command =
                        ParseAuthorizeObservation(args[1..]);
                    _ = await new PythonLaptopMiniPcObservationAuthorizer()
                        .AuthorizeAsync(
                            new(
                                command.AuthorizationPolicyPath,
                                command.ExpectedAuthorizationPolicySha256,
                                command.RollbackPath),
                            cancellationToken)
                        .ConfigureAwait(false);
                    output.WriteLine("Operation            : Authorize Laptop MiniPC observation");
                    output.WriteLine("Outcome              : Succeeded");
                    output.WriteLine("Principal            : hase-laptop-python-minipc");
                    output.WriteLine("Permission           : observation.subscribe");
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
        catch (PythonCredentialLifecycleInspectionException exception)
        {
            return Failure(error, exception.Code);
        }
        catch (PythonCredentialRotationPreparationException exception)
        {
            return Failure(error, exception.Code);
        }
        catch (PythonCredentialRotationPublicationException exception)
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
        catch (PythonLaptopMiniPcCommandExecutionAuthorizationException exception)
        {
            return Failure(error, exception.Code);
        }
        catch (PythonLaptopMiniPcPropertyWriteAuthorizationException exception)
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

    private static ProvisionCommand ParseProvision(
        string[] args,
        string principalId)
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
            principalId,
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

    private static RotationBeginCommand ParseRotationBegin(string[] args)
    {
        ParsedArguments parsed = Parse(args, RotationBeginValueNames, false);
        RotationPublicationCommand publication =
            CreateRotationPublication(parsed);
        string thumbprint = parsed.Values["signing-root-thumbprint"];
        if (!IsHex(thumbprint, 40, allowUppercase: true)
            || !int.TryParse(parsed.Values["validity-days"],
                NumberStyles.None, CultureInfo.InvariantCulture,
                out int validityDays)
            || validityDays is < 1 or > 90)
            throw new ArgumentException();
        string principal = RequireValue(parsed.Values["principal-id"]);
        string trust = RequireValue(parsed.Values["trust-policy-id"]);
        string[] grants = parsed.Values["expected-grants"]
            .Split(',', StringSplitOptions.None);
        if (grants.Length == 0
            || grants.Any(value => string.IsNullOrWhiteSpace(value)
                || value != value.Trim())
            || grants.Distinct(StringComparer.Ordinal).Count() != grants.Length)
            throw new ArgumentException();
        string trustedHash =
            parsed.Values["expected-trusted-server-certificate-sha256"];
        string currentCredentialId =
            parsed.Values["expected-current-credential-id"];
        if (!IsHex(trustedHash, 64, false)
            || !currentCredentialId.StartsWith("x509-sha256:",
                StringComparison.Ordinal)
            || !IsHex(currentCredentialId[12..], 64, false))
            throw new ArgumentException();
        return new(publication, thumbprint, TimeSpan.FromDays(validityDays),
            principal, trust, Array.AsReadOnly(grants), currentCredentialId,
            trustedHash);
    }

    private static RotationPublicationCommand ParseRotationPublication(
        string[] args) =>
        CreateRotationPublication(Parse(args, RotationPublicationValueNames,
            false));

    private static RotationPublicationCommand CreateRotationPublication(
        ParsedArguments parsed)
    {
        string[] hashes =
        [
            parsed.Values["expected-certificate-sha256"],
            parsed.Values["expected-private-key-sha256"],
            parsed.Values["expected-profile-sha256"],
            parsed.Values["expected-enrollment-sha256"],
            parsed.Values["expected-authorization-policy-sha256"],
        ];
        if (hashes.Any(hash => !IsHex(hash, 64, false)))
            throw new ArgumentException();
        string directory = RequireAbsolute(
            parsed.Values["provisioning-directory"]);
        string certificate = RequireAbsolute(parsed.Values["certificate"]);
        string privateKey = RequireAbsolute(parsed.Values["private-key"]);
        string profile = RequireAbsolute(parsed.Values["profile"]);
        string enrollment = RequireAbsolute(parsed.Values["enrollment"]);
        string policy = RequireAbsolute(parsed.Values["authorization-policy"]);
        RequireDistinct(certificate, privateKey, profile, enrollment, policy);
        return new(directory, certificate, privateKey, profile, enrollment,
            policy, hashes[0], hashes[1], hashes[2], hashes[3], hashes[4]);
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
        error.WriteLine("  provision-laptop-minipc --signing-root-thumbprint <value> --trust-policy-id <value> --source-profile <absolute-path> --provisioning-directory <absolute-path> --certificate <absolute-path> --private-key <absolute-path> --profile <absolute-path> --enrollment <absolute-path> --authorization-policy <absolute-path> --expected-authorization-policy-sha256 <value> --validity-days <1-90>");
        error.WriteLine("  recover --provisioning-directory <absolute-path> --certificate <absolute-path> --private-key <absolute-path> --profile <absolute-path> --enrollment <absolute-path> --authorization-policy <absolute-path>");
        error.WriteLine("  rotate-begin --signing-root-thumbprint <value> --validity-days <1-90> --principal-id <value> --trust-policy-id <value> --expected-grants <comma-separated> --expected-current-credential-id <value> --expected-trusted-server-certificate-sha256 <value> --provisioning-directory <absolute-path> --certificate <absolute-path> --private-key <absolute-path> --profile <absolute-path> --enrollment <absolute-path> --authorization-policy <absolute-path> --expected-certificate-sha256 <value> --expected-private-key-sha256 <value> --expected-profile-sha256 <value> --expected-enrollment-sha256 <value> --expected-authorization-policy-sha256 <value>");
        error.WriteLine("  rotate-finalize|rotate-recover --provisioning-directory <absolute-path> --certificate <absolute-path> --private-key <absolute-path> --profile <absolute-path> --enrollment <absolute-path> --authorization-policy <absolute-path> --expected-certificate-sha256 <value> --expected-private-key-sha256 <value> --expected-profile-sha256 <value> --expected-enrollment-sha256 <value> --expected-authorization-policy-sha256 <value>");
        error.WriteLine("  authorize-property-write --authorization-policy <absolute-path> --expected-authorization-policy-sha256 <value> --application-profile <absolute-path> --expected-application-profile-sha256 <value> --policy-rollback <absolute-path> --profile-rollback <absolute-path>");
        error.WriteLine("  authorize-command-execution --authorization-policy <absolute-path> --expected-authorization-policy-sha256 <value> --rollback <absolute-path>");
        error.WriteLine("  authorize-laptop-minipc-command-execution --authorization-policy <absolute-path> --expected-authorization-policy-sha256 <value> --rollback <absolute-path>");
        error.WriteLine("  authorize-laptop-minipc-property-write --authorization-policy <absolute-path> --expected-authorization-policy-sha256 <value> --rollback <absolute-path>");
        error.WriteLine("  authorize-laptop-minipc-observation --authorization-policy <absolute-path> --expected-authorization-policy-sha256 <value> --rollback <absolute-path>");
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
            command.PrincipalId,
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

    public async Task<PythonCredentialRotationPublicationResult>
        BeginRotationAsync(
            RotationBeginCommand command,
            CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException();
        DateTimeOffset utcNow = NormalizeCertificateTimestamp(
            DateTimeOffset.UtcNow);
        using PythonClientCredentialMaterial replacement = CreateMaterial(
            command.SigningRootThumbprint, utcNow, command.Validity);
        var inspection = new PythonCredentialLifecycleInspectionRequest(
            command.Publication.ProfilePath,
            command.Publication.EnrollmentPath,
            command.Publication.AuthorizationPolicyPath,
            command.PrincipalId,
            command.TrustPolicyId,
            command.ExpectedGrants);
        var preparation = new PythonCredentialRotationPreparationRequest(
            inspection,
            command.ExpectedCurrentCredentialId,
            command.Publication.ProfileSha256,
            command.Publication.EnrollmentSha256,
            command.Publication.AuthorizationPolicySha256,
            command.ExpectedTrustedServerCertificateSha256);
        return await new PythonCredentialRotationOrchestrator().BeginAsync(
            preparation, CreatePublicationRequest(command.Publication),
            replacement, utcNow, cancellationToken).ConfigureAwait(false);
    }

    public PythonCredentialRotationPublicationResult FinalizeRotation(
        RotationPublicationCommand command) =>
        new PythonCredentialRotationOrchestrator().Finalize(
            CreatePublicationRequest(command));

    public PythonCredentialRotationPublicationResult RecoverRotation(
        RotationPublicationCommand command) =>
        new PythonCredentialRotationOrchestrator().Recover(
            CreatePublicationRequest(command));

    private static PythonCredentialRotationPublicationRequest
        CreatePublicationRequest(RotationPublicationCommand command) =>
        new(command.ProvisioningDirectory, command.CertificatePath,
            command.PrivateKeyPath, command.ProfilePath,
            command.EnrollmentPath, command.AuthorizationPolicyPath,
            command.CertificateSha256, command.PrivateKeySha256,
            command.ProfileSha256, command.EnrollmentSha256,
            command.AuthorizationPolicySha256);

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
    Task<PythonCredentialRotationPublicationResult> BeginRotationAsync(
        RotationBeginCommand command, CancellationToken cancellationToken);
    PythonCredentialRotationPublicationResult FinalizeRotation(
        RotationPublicationCommand command);
    PythonCredentialRotationPublicationResult RecoverRotation(
        RotationPublicationCommand command);

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
    string PrincipalId,
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

internal sealed record RotationBeginCommand(
    RotationPublicationCommand Publication,
    string SigningRootThumbprint,
    TimeSpan Validity,
    string PrincipalId,
    string TrustPolicyId,
    IReadOnlyList<string> ExpectedGrants,
    string ExpectedCurrentCredentialId,
    string ExpectedTrustedServerCertificateSha256);

internal sealed record RotationPublicationCommand(
    string ProvisioningDirectory,
    string CertificatePath,
    string PrivateKeyPath,
    string ProfilePath,
    string EnrollmentPath,
    string AuthorizationPolicyPath,
    string CertificateSha256,
    string PrivateKeySha256,
    string ProfileSha256,
    string EnrollmentSha256,
    string AuthorizationPolicySha256);

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
