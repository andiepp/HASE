using Hase.Python.CredentialProvisioning.Operator;

namespace Hase.Python.CredentialProvisioning.Tests;

public sealed class PythonCredentialProvisioningOperatorTests
{
    [Fact]
    public void NormalizeCertificateTimestamp_FractionalOffsetValue_ReturnsWholeUtcSecond()
    {
        var timestamp = new DateTimeOffset(
            2026, 8, 8, 17, 23, 41, 987,
            TimeSpan.FromHours(2)).AddTicks(6543);

        DateTimeOffset normalized =
            SystemPythonCredentialProvisioningOperations
                .NormalizeCertificateTimestamp(timestamp);

        Assert.Equal(
            new DateTimeOffset(
                2026, 8, 8, 15, 23, 41,
                TimeSpan.Zero),
            normalized);
        Assert.Equal(0, normalized.Ticks % TimeSpan.TicksPerSecond);
    }

    [Fact]
    public async Task RunAsync_Provision_DelegatesAndWithholdsInputs()
    {
        string[] args = ProvisionArguments(allowReplacement: true);
        var operations = new StubOperations
        {
            ProvisioningResult = new OperatorProvisioningResult(
                "python-provisioning-plan-sha256:" + new string('1', 64),
                "0123456789abcdef0123456789abcdef",
                true),
        };
        var output = new StringWriter();
        var error = new StringWriter();

        int exitCode = await PythonCredentialProvisioningOperator.RunAsync(
            args, output, error, operations, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.NotNull(operations.ProvisionCommand);
        Assert.True(operations.ProvisionCommand.AllowReplacement);
        Assert.Equal(TimeSpan.FromDays(30),
            operations.ProvisionCommand.Validity);
        Assert.Contains("Outcome              : Succeeded", output.ToString());
        Assert.Contains(operations.ProvisioningResult.PlanId, output.ToString());
        Assert.Contains(operations.ProvisioningResult.TransactionId,
            output.ToString());
        Assert.Equal(string.Empty, error.ToString());
        AssertInputsWithheld(args, output.ToString());
    }

    [Fact]
    public async Task RunAsync_ProvisionWithoutReplacement_PreservesFalse()
    {
        string[] args = ProvisionArguments(allowReplacement: false);
        var operations = new StubOperations();

        int exitCode = await PythonCredentialProvisioningOperator.RunAsync(
            args, TextWriter.Null, TextWriter.Null, operations,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.False(operations.ProvisionCommand!.AllowReplacement);
    }

    [Fact]
    public async Task RunAsync_ProvisionLaptopMiniPc_UsesDistinctFixedPrincipal()
    {
        string[] args = ProvisionArguments(allowReplacement: false);
        args[0] = "provision-laptop-minipc";
        var operations = new StubOperations();

        int exitCode = await PythonCredentialProvisioningOperator.RunAsync(
            args, TextWriter.Null, TextWriter.Null, operations,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal("hase-laptop-python-minipc",
            operations.ProvisionCommand!.PrincipalId);
        Assert.False(operations.ProvisionCommand.AllowReplacement);
    }

    [Fact]
    public async Task RunAsync_Recover_DelegatesExactTargetsAndWithholdsInputs()
    {
        string[] args = RecoveryArguments();
        var operations = new StubOperations
        {
            RecoveryResult = new PythonCredentialProvisioningRecoveryResult(
                PythonCredentialProvisioningRecoveryDisposition.RolledBack,
                "fedcba9876543210fedcba9876543210"),
        };
        var output = new StringWriter();

        int exitCode = await PythonCredentialProvisioningOperator.RunAsync(
            args, output, TextWriter.Null, operations, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.NotNull(operations.RecoveryCommand);
        Assert.Equal(P("certificate.pem"),
            operations.RecoveryCommand.CertificatePath);
        Assert.Contains("Outcome              : RolledBack", output.ToString());
        Assert.DoesNotContain(operations.RecoveryResult.TransactionId!,
            output.ToString(), StringComparison.Ordinal);
        AssertInputsWithheld(args, output.ToString());
    }

    [Fact]
    public async Task RunAsync_AuthorizePropertyWrite_DelegatesAndWithholdsInputs()
    {
        string[] args = AuthorizePropertyWriteArguments();
        var operations = new StubOperations();
        var output = new StringWriter();

        int exitCode = await PythonCredentialProvisioningOperator.RunAsync(
            args, output, TextWriter.Null, operations, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.NotNull(operations.AuthorizeCommand);
        Assert.Equal(P("authorization.json"),
            operations.AuthorizeCommand.AuthorizationPolicyPath);
        Assert.Equal(P("desktop-runtime-host.json"),
            operations.AuthorizeCommand.ApplicationProfilePath);
        Assert.Contains("Permission           : property.write",
            output.ToString());
        Assert.Contains("Rollback retained    : True", output.ToString());
        AssertInputsWithheld(args, output.ToString());
    }

    [Fact]
    public async Task RunAsync_AuthorizeCommandExecution_Delegates()
    {
        string[] args = ["authorize-command-execution",
            "--authorization-policy", P("authorization.json"),
            "--expected-authorization-policy-sha256", new string('a', 64),
            "--rollback", P("authorization.command.rollback.json")];
        var operations = new StubOperations();
        var output = new StringWriter();
        int code = await PythonCredentialProvisioningOperator.RunAsync(args,
            output, TextWriter.Null, operations, CancellationToken.None);
        Assert.Equal(0, code);
        Assert.NotNull(operations.CommandExecutionCommand);
        Assert.Contains("Permission           : command.execute", output.ToString());
        AssertInputsWithheld(args, output.ToString());
    }

    [Fact]
    public async Task RunAsync_AuthorizeObservation_Delegates()
    {
        string[] args = ["authorize-observation", "--authorization-policy",
            P("authorization.json"), "--expected-authorization-policy-sha256",
            new string('a', 64), "--rollback", P("observation.rollback.json")];
        var operations = new StubOperations(); var output = new StringWriter();
        int code = await PythonCredentialProvisioningOperator.RunAsync(args,
            output, TextWriter.Null, operations, CancellationToken.None);
        Assert.Equal(0, code); Assert.NotNull(operations.ObservationCommand);
        Assert.Contains("Permission           : observation.subscribe",
            output.ToString()); AssertInputsWithheld(args, output.ToString());
    }

    [Theory]
    [MemberData(nameof(InvalidCommands))]
    public async Task RunAsync_InvalidArguments_DoNotInvokeOperations(string[] args)
    {
        var operations = new StubOperations();
        var error = new StringWriter();

        int exitCode = await PythonCredentialProvisioningOperator.RunAsync(
            args, TextWriter.Null, error, operations, CancellationToken.None);

        Assert.Equal(2, exitCode);
        Assert.Null(operations.ProvisionCommand);
        Assert.Null(operations.RecoveryCommand);
        Assert.NotEqual(string.Empty, error.ToString());
    }

    [Fact]
    public async Task RunAsync_KnownFailure_PrintsOnlySanitizedCode()
    {
        string[] args = ProvisionArguments(allowReplacement: false);
        var operations = new StubOperations
        {
            ProvisionException =
                new PythonCredentialProvisioningPublicationException(
                    "replacement-not-authorized"),
        };
        var output = new StringWriter();
        var error = new StringWriter();

        int exitCode = await PythonCredentialProvisioningOperator.RunAsync(
            args, output, error, operations, CancellationToken.None);

        Assert.Equal(3, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Equal(
            "Operation failed. Error code: replacement-not-authorized"
                + Environment.NewLine,
            error.ToString());
        AssertInputsWithheld(args, error.ToString());
    }

    [Fact]
    public async Task RunAsync_UnexpectedFailure_DoesNotPrintExceptionMessage()
    {
        string[] args = ProvisionArguments(allowReplacement: false);
        const string secret = "private-material-must-not-escape";
        var operations = new StubOperations
        {
            ProvisionException = new IOException(secret),
        };
        var error = new StringWriter();

        int exitCode = await PythonCredentialProvisioningOperator.RunAsync(
            args, TextWriter.Null, error, operations, CancellationToken.None);

        Assert.Equal(3, exitCode);
        Assert.Equal(
            "Operation failed. Error code: operation-failed"
                + Environment.NewLine,
            error.ToString());
        Assert.DoesNotContain(secret, error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_Cancellation_ReturnsDedicatedExitCode()
    {
        string[] args = ProvisionArguments(allowReplacement: false);
        var operations = new StubOperations
        {
            ProvisionException = new OperationCanceledException(),
        };
        var error = new StringWriter();

        int exitCode = await PythonCredentialProvisioningOperator.RunAsync(
            args, TextWriter.Null, error, operations,
            new CancellationToken(canceled: true));

        Assert.Equal(4, exitCode);
        Assert.Equal("Operation canceled." + Environment.NewLine,
            error.ToString());
    }

    public static TheoryData<string[]> InvalidCommands => new()
    {
        Array.Empty<string>(),
        new[] { "unknown" },
        new[] { "provision", "--signing-root-thumbprint", "value" },
        ProvisionArguments(allowReplacement: false)
            .Append("--trust-policy-id").Append("duplicate").ToArray(),
        ProvisionArguments(allowReplacement: false)
            .Select(value => value == "30" ? "0" : value).ToArray(),
        ProvisionArguments(allowReplacement: false)
            .Select(value => value == P("source-profile.json")
                ? "relative.json" : value).ToArray(),
        ProvisionArguments(allowReplacement: false)
            .Select(value => value == P("private-key.pem")
                ? P("certificate.pem") : value).ToArray(),
        ProvisionArguments(allowReplacement: false)
            .Select(value => value == new string('a', 64)
                ? new string('A', 64) : value).ToArray(),
        RecoveryArguments().Append("--allow-replacement").ToArray(),
        AuthorizePropertyWriteArguments()
            .Append("--policy-rollback").Append(P("duplicate.json")).ToArray(),
    };

    private static string[] ProvisionArguments(bool allowReplacement)
    {
        var values = new List<string>
        {
            "provision",
            "--signing-root-thumbprint", "0123456789ABCDEF0123456789ABCDEF01234567",
            "--trust-policy-id", "runtime-host-client-v1",
            "--source-profile", P("source-profile.json"),
            "--provisioning-directory", P("provisioning"),
            "--certificate", P("certificate.pem"),
            "--private-key", P("private-key.pem"),
            "--profile", P("profile.json"),
            "--enrollment", P("enrollment.json"),
            "--authorization-policy", P("authorization.json"),
            "--expected-authorization-policy-sha256", new string('a', 64),
            "--validity-days", "30",
        };
        if (allowReplacement) values.Add("--allow-replacement");
        return values.ToArray();
    }

    private static string[] RecoveryArguments() =>
    [
        "recover",
        "--provisioning-directory", P("provisioning"),
        "--certificate", P("certificate.pem"),
        "--private-key", P("private-key.pem"),
        "--profile", P("profile.json"),
        "--enrollment", P("enrollment.json"),
        "--authorization-policy", P("authorization.json"),
    ];

    private static string[] AuthorizePropertyWriteArguments() =>
    [
        "authorize-property-write",
        "--authorization-policy", P("authorization.json"),
        "--expected-authorization-policy-sha256", new string('a', 64),
        "--application-profile", P("desktop-runtime-host.json"),
        "--expected-application-profile-sha256", new string('b', 64),
        "--policy-rollback", P("authorization.rollback.json"),
        "--profile-rollback", P("desktop-runtime-host.rollback.json"),
    ];

    private static string P(string name) => Path.GetFullPath(
        Path.Combine(Path.GetTempPath(), "hase-operator-tests", name));

    private static void AssertInputsWithheld(
        IReadOnlyList<string> args,
        string text)
    {
        for (int index = 1; index < args.Count; index++)
        {
            if (args[index].StartsWith("--", StringComparison.Ordinal)) continue;
            Assert.DoesNotContain(args[index], text, StringComparison.Ordinal);
        }
    }

    private sealed class StubOperations : IPythonCredentialProvisioningOperations
    {
        public ProvisionCommand? ProvisionCommand { get; private set; }
        public RecoveryCommand? RecoveryCommand { get; private set; }
        public AuthorizePropertyWriteCommand? AuthorizeCommand { get; private set; }
        public AuthorizeCommandExecutionCommand? CommandExecutionCommand
            { get; private set; }
        public AuthorizeObservationCommand? ObservationCommand { get; private set; }
        public Exception? ProvisionException { get; init; }
        public OperatorProvisioningResult ProvisioningResult { get; init; } =
            new("python-provisioning-plan-sha256:" + new string('1', 64),
                "0123456789abcdef0123456789abcdef", false);
        public PythonCredentialProvisioningRecoveryResult RecoveryResult
            { get; init; } = new(
                PythonCredentialProvisioningRecoveryDisposition.NoTransaction,
                null);
        public PythonPropertyWriteAuthorizationResult AuthorizationResult
            { get; init; } = new(
                "0123456789abcdef0123456789abcdef",
                new string('b', 64),
                new string('c', 64),
                P("authorization.rollback.json"),
                P("desktop-runtime-host.rollback.json"));

        public Task<OperatorProvisioningResult> ProvisionAsync(
            ProvisionCommand command,
            CancellationToken cancellationToken)
        {
            ProvisionCommand = command;
            if (ProvisionException is not null) throw ProvisionException;
            return Task.FromResult(ProvisioningResult);
        }

        public PythonCredentialProvisioningRecoveryResult Recover(
            RecoveryCommand command)
        {
            RecoveryCommand = command;
            return RecoveryResult;
        }

        public Task<PythonPropertyWriteAuthorizationResult>
            AuthorizePropertyWriteAsync(
                AuthorizePropertyWriteCommand command,
                CancellationToken cancellationToken)
        {
            AuthorizeCommand = command;
            return Task.FromResult(AuthorizationResult);
        }

        public Task<PythonCommandExecutionAuthorizationResult>
            AuthorizeCommandExecutionAsync(
                AuthorizeCommandExecutionCommand command,
                CancellationToken cancellationToken)
        {
            CommandExecutionCommand = command;
            return Task.FromResult(new PythonCommandExecutionAuthorizationResult(
                "0123456789abcdef0123456789abcdef", new string('d', 64),
                command.RollbackPath));
        }

        public Task<PythonObservationAuthorizationResult> AuthorizeObservationAsync(
            AuthorizeObservationCommand command,
            CancellationToken cancellationToken)
        {
            ObservationCommand = command;
            return Task.FromResult(new PythonObservationAuthorizationResult(
                "0123456789abcdef0123456789abcdef", new string('e', 64),
                command.RollbackPath));
        }
        public Task<PythonCachedPropertyAuthorizationResult> AuthorizeCachedPropertyAsync(
            AuthorizeCachedPropertyCommand command,CancellationToken token)=>
            Task.FromResult(new PythonCachedPropertyAuthorizationResult("id",new string('f',64),command.RollbackPath));
    }
}
