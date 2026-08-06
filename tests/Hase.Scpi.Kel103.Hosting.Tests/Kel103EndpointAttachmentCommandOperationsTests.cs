using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Hase.Scpi;
using Hase.Scpi.Kel103.Runtime;

namespace Hase.Scpi.Kel103.Hosting.Tests;

public sealed class Kel103EndpointAttachmentCommandOperationsTests
{
    [Fact]
    public async Task Execute_ForwardsExactParameterlessCommandAndReturnsSuccess()
    {
        InstrumentId? observedInstrument = null;
        DescriptorPath? observedPath = null;
        object? observedArgument = new();
        var operations = CreateOperations((instrument, path, argument, token) =>
        {
            observedInstrument = instrument;
            observedPath = path;
            observedArgument = argument;
            return Task.FromResult(RuntimeCommand(path));
        });

        EndpointAttachmentCommandOperationResult result = await operations.ExecuteAsync(
            InstrumentId(),
            Kel103ModeSelectionMapping.ConstantVoltage.CommandPath,
            argument: null);

        Assert.True(result.IsSuccess);
        Assert.Null(result.ReturnValue);
        Assert.Null(result.Diagnostic);
        Assert.Equal(InstrumentId(), observedInstrument);
        Assert.Equal(Kel103ModeSelectionMapping.ConstantVoltage.CommandPath, observedPath);
        Assert.Null(observedArgument);
    }

    [Fact]
    public async Task Execute_InvalidNonNullArgumentIsForwardedThenNormalized()
    {
        var called = false;
        object? observedArgument = null;
        var operations = CreateOperations((instrument, path, argument, token) =>
        {
            called = true;
            observedArgument = argument;
            return Task.FromException<RuntimeCommand>(new ArgumentException("sensitive"));
        });

        EndpointAttachmentCommandOperationResult result = await operations.ExecuteAsync(
            InstrumentId(),
            Kel103ModeSelectionMapping.ConstantCurrent.CommandPath,
            argument: true);

        Assert.Equal(EndpointAttachmentCommandOperationStatus.ArgumentNotSupported, result.Status);
        Assert.True(called);
        Assert.True(Assert.IsType<bool>(observedArgument));
        Assert.DoesNotContain("sensitive", result.Diagnostic ?? string.Empty, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(1, null)]
    [InlineData(2, true)]
    public async Task Execute_ForwardsExactInputControlArgumentsOnce(
        int mappingIndex,
        object? argument)
    {
        var calls = 0;
        object? observedArgument = new();
        DescriptorPath? observedPath = null;
        var operations = CreateOperations((instrument, path, actualArgument, token) =>
        {
            calls++;
            observedArgument = actualArgument;
            observedPath = path;
            return Task.FromResult(RuntimeCommand(path));
        });
        Kel103InputControlMapping mapping = Kel103InputControlMapping.All[mappingIndex];

        EndpointAttachmentCommandOperationResult result = await operations.ExecuteAsync(
            InstrumentId(),
            mapping.CommandPath,
            argument);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, calls);
        Assert.Equal(mapping.CommandPath, observedPath);
        Assert.Equal(argument, observedArgument);
    }

    [Theory]
    [InlineData(0, EndpointAttachmentCommandOperationStatus.Failure)]
    [InlineData(1, EndpointAttachmentCommandOperationStatus.ArgumentNotSupported)]
    [InlineData(2, EndpointAttachmentCommandOperationStatus.TimedOut)]
    [InlineData(3, EndpointAttachmentCommandOperationStatus.Failure)]
    [InlineData(4, EndpointAttachmentCommandOperationStatus.Rejected)]
    [InlineData(5, EndpointAttachmentCommandOperationStatus.Unavailable)]
    [InlineData(6, EndpointAttachmentCommandOperationStatus.Unavailable)]
    public async Task Execute_MapsSafeFailureOutcomes(
        int failure,
        EndpointAttachmentCommandOperationStatus expectedStatus)
    {
        const string sensitive = "sensitive Command failure detail";
        Exception exception = failure switch
        {
            0 => new KeyNotFoundException(sensitive),
            1 => new ArgumentException(sensitive),
            2 => new TimeoutException(sensitive),
            3 => new InvalidDataException(sensitive),
            4 => new InvalidOperationException(sensitive),
            5 => new InvalidOperationException(sensitive),
            _ => new IOException(sensitive)
        };
        var operations = CreateOperations(
            (instrument, path, argument, token) =>
                Task.FromException<RuntimeCommand>(exception),
            isSessionFaulted: () => failure != 4);

        EndpointAttachmentCommandOperationResult result = await operations.ExecuteAsync(
            InstrumentId(),
            Kel103ModeSelectionMapping.ConstantResistance.CommandPath,
            argument: null);

        Assert.Equal(expectedStatus, result.Status);
        Assert.Null(result.ReturnValue);
        Assert.DoesNotContain(
            sensitive,
            result.Diagnostic ?? string.Empty,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Execute_MapsPostTransmissionUncertaintyExplicitly(int failure)
    {
        const string sensitive = "sensitive uncertain detail";
        Exception exception = failure == 0
            ? new Kel103MutationOutcomeUncertainException(
                sensitive,
                new InvalidDataException(sensitive))
            : new ScpiCommandTransmissionException(
                sensitive,
                executionMayHaveOccurred: true,
                new IOException(sensitive));
        var operations = CreateOperations(
            (instrument, path, argument, token) =>
                Task.FromException<RuntimeCommand>(exception),
            isSessionFaulted: static () => true);

        EndpointAttachmentCommandOperationResult result = await operations.ExecuteAsync(
            InstrumentId(),
            Kel103ModeSelectionMapping.ShortCircuit.CommandPath,
            argument: null);

        Assert.Equal(EndpointAttachmentCommandOperationStatus.Unavailable, result.Status);
        Assert.Contains(
            "outcome is uncertain",
            result.Diagnostic ?? string.Empty,
            StringComparison.Ordinal);
        Assert.DoesNotContain(sensitive, result.Diagnostic ?? string.Empty, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, "mode selection requires authoritative input OFF")]
    [InlineData(1, "input activation rejects SHORT")]
    [InlineData(2, "input-deactivation operation was rejected")]
    [InlineData(3, "SHORT activation requires authoritative input OFF and SHORT mode")]
    public async Task Execute_UsesCapabilitySpecificSanitizedRejection(
        int category,
        string expected)
    {
        const string sensitive = "raw rejection detail";
        DescriptorPath path = InputOrModePath(category);
        var operations = CreateOperations(
            (instrument, commandPath, argument, token) =>
                Task.FromException<RuntimeCommand>(new InvalidOperationException(sensitive)),
            isSessionFaulted: static () => false);

        EndpointAttachmentCommandOperationResult result = await operations.ExecuteAsync(
            InstrumentId(),
            path,
            category == 3 ? true : null);

        Assert.Equal(EndpointAttachmentCommandOperationStatus.Rejected, result.Status);
        Assert.Contains(expected, result.Diagnostic ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain(sensitive, result.Diagnostic ?? string.Empty, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, "mode-selection operation")]
    [InlineData(1, "input-activation operation")]
    [InlineData(2, "input-deactivation operation")]
    [InlineData(3, "confirmed SHORT-activation operation")]
    public async Task Execute_UsesCapabilitySpecificSanitizedTimeout(
        int category,
        string expected)
    {
        const string sensitive = "raw timeout detail";
        var operations = CreateOperations(
            (instrument, commandPath, argument, token) =>
                Task.FromException<RuntimeCommand>(new TimeoutException(sensitive)),
            isSessionFaulted: static () => true);

        EndpointAttachmentCommandOperationResult result = await operations.ExecuteAsync(
            InstrumentId(),
            InputOrModePath(category),
            category == 3 ? true : null);

        Assert.Equal(EndpointAttachmentCommandOperationStatus.TimedOut, result.Status);
        Assert.Contains(expected, result.Diagnostic ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain(sensitive, result.Diagnostic ?? string.Empty, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, "mode-selection operation")]
    [InlineData(1, "input-activation operation")]
    [InlineData(2, "input-deactivation operation")]
    [InlineData(3, "confirmed SHORT-activation operation")]
    public async Task Execute_UsesCapabilitySpecificSanitizedUnavailable(
        int category,
        string expected)
    {
        const string sensitive = "raw unavailable detail";
        var operations = CreateOperations(
            (instrument, commandPath, argument, token) =>
                Task.FromException<RuntimeCommand>(new IOException(sensitive)),
            isSessionFaulted: static () => true);

        EndpointAttachmentCommandOperationResult result = await operations.ExecuteAsync(
            InstrumentId(),
            InputOrModePath(category),
            category == 3 ? true : null);

        Assert.Equal(EndpointAttachmentCommandOperationStatus.Unavailable, result.Status);
        Assert.Contains(expected, result.Diagnostic ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain(sensitive, result.Diagnostic ?? string.Empty, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, "mode-selection outcome is uncertain", "operating mode")]
    [InlineData(1, "input-activation operation outcome is uncertain", "input state")]
    [InlineData(2, "input-deactivation operation outcome is uncertain", "input state")]
    [InlineData(3, "confirmed SHORT-activation operation outcome is uncertain", "input state")]
    public async Task Execute_UsesCapabilitySpecificSanitizedUncertainty(
        int category,
        string expected,
        string verification)
    {
        const string sensitive = "raw uncertain detail";
        var operations = CreateOperations(
            (instrument, commandPath, argument, token) =>
                Task.FromException<RuntimeCommand>(
                    new Kel103MutationOutcomeUncertainException(
                        sensitive,
                        new InvalidDataException(sensitive))),
            isSessionFaulted: static () => true);

        EndpointAttachmentCommandOperationResult result = await operations.ExecuteAsync(
            InstrumentId(),
            InputOrModePath(category),
            category == 3 ? true : null);

        Assert.Equal(EndpointAttachmentCommandOperationStatus.Unavailable, result.Status);
        Assert.Contains(expected, result.Diagnostic ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains(verification, result.Diagnostic ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain(sensitive, result.Diagnostic ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Execute_FaultedSessionProjectsSanitizedRuntimeFault()
    {
        const string sensitive = "sensitive transport detail";
        RuntimeEndpoint endpoint = ReadyEndpoint();
        var operations = CreateOperations(
            (instrument, path, argument, token) =>
                Task.FromException<RuntimeCommand>(new IOException(sensitive)),
            isSessionFaulted: static () => true,
            runtimeEndpoint: endpoint,
            timeProvider: new FixedTimeProvider());

        EndpointAttachmentCommandOperationResult result = await operations.ExecuteAsync(
            InstrumentId(),
            Kel103ModeSelectionMapping.ConstantPower.CommandPath,
            argument: null);

        Assert.Equal(EndpointAttachmentCommandOperationStatus.Unavailable, result.Status);
        Assert.Equal(EndpointConnectionState.Faulted, endpoint.ConnectionStatus.State);
        Assert.Equal(FixedTimeProvider.Timestamp, endpoint.ConnectionStatus.ChangedAtUtc);
        Assert.Equal(
            "The KEL-103 communication session is faulted.",
            endpoint.ConnectionStatus.Detail);
        Assert.DoesNotContain(
            sensitive,
            endpoint.ConnectionStatus.Detail ?? string.Empty,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Execute_InputControlUncertaintyProjectsSanitizedRuntimeFault()
    {
        RuntimeEndpoint endpoint = ReadyEndpoint();
        var operations = CreateOperations(
            (instrument, path, argument, token) =>
                Task.FromException<RuntimeCommand>(
                    new Kel103MutationOutcomeUncertainException(
                        "sensitive",
                        new InvalidDataException("sensitive"))),
            isSessionFaulted: static () => true,
            runtimeEndpoint: endpoint,
            timeProvider: new FixedTimeProvider());

        EndpointAttachmentCommandOperationResult result = await operations.ExecuteAsync(
            InstrumentId(),
            Kel103InputControlMapping.Activate.CommandPath,
            argument: null);

        Assert.Equal(EndpointAttachmentCommandOperationStatus.Unavailable, result.Status);
        Assert.Equal(EndpointConnectionState.Faulted, endpoint.ConnectionStatus.State);
        Assert.Equal(
            "The KEL-103 communication session is faulted.",
            endpoint.ConnectionStatus.Detail);
        Assert.DoesNotContain("sensitive", result.Diagnostic ?? string.Empty, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Execute_InputControlLocalRejectionsDoNotProjectFault(int failure)
    {
        RuntimeEndpoint endpoint = ReadyEndpoint();
        Exception exception = failure == 0
            ? new ArgumentException("sensitive")
            : new InvalidOperationException("sensitive");
        var operations = CreateOperations(
            (instrument, path, argument, token) =>
                Task.FromException<RuntimeCommand>(exception),
            isSessionFaulted: static () => false,
            runtimeEndpoint: endpoint,
            timeProvider: new FixedTimeProvider());

        EndpointAttachmentCommandOperationResult result = await operations.ExecuteAsync(
            InstrumentId(),
            failure == 0
                ? Kel103InputControlMapping.ShortCircuitActivate.CommandPath
                : Kel103InputControlMapping.Activate.CommandPath,
            failure == 0 ? false : null);

        Assert.Equal(
            failure == 0
                ? EndpointAttachmentCommandOperationStatus.ArgumentNotSupported
                : EndpointAttachmentCommandOperationStatus.Rejected,
            result.Status);
        Assert.Equal(EndpointConnectionState.Ready, endpoint.ConnectionStatus.State);
    }

    [Fact]
    public async Task Execute_LocalRejectionsDoNotProjectFault()
    {
        RuntimeEndpoint endpoint = ReadyEndpoint();
        Exception[] exceptions =
        [
            new KeyNotFoundException(),
            new ArgumentException(),
            new InvalidOperationException()
        ];

        foreach (Exception exception in exceptions)
        {
            var operations = CreateOperations(
                (instrument, path, argument, token) =>
                    Task.FromException<RuntimeCommand>(exception),
                isSessionFaulted: () => exception is not InvalidOperationException,
                runtimeEndpoint: endpoint,
                timeProvider: new FixedTimeProvider());

            await operations.ExecuteAsync(
                InstrumentId(),
                Kel103ModeSelectionMapping.ConstantCurrent.CommandPath,
                argument: null);

            Assert.Equal(EndpointConnectionState.Ready, endpoint.ConnectionStatus.State);
        }
    }

    [Fact]
    public async Task Execute_PreCancellationDoesNotCallAdapterOrProjectFault()
    {
        RuntimeEndpoint endpoint = ReadyEndpoint();
        var called = false;
        var operations = CreateOperations(
            (instrument, path, argument, token) =>
            {
                called = true;
                return Task.FromResult(RuntimeCommand(path));
            },
            isSessionFaulted: static () => true,
            runtimeEndpoint: endpoint,
            timeProvider: new FixedTimeProvider());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            operations.ExecuteAsync(
                InstrumentId(),
                Kel103ModeSelectionMapping.ConstantCurrent.CommandPath,
                argument: null,
                cancellationToken: cancellation.Token));

        Assert.False(called);
        Assert.Equal(EndpointConnectionState.Ready, endpoint.ConnectionStatus.State);
    }

    [Fact]
    public async Task Execute_InFlightCancellationProjectsFaultOnlyAfterSessionFaults()
    {
        RuntimeEndpoint endpoint = ReadyEndpoint();
        using var cancellation = new CancellationTokenSource();
        var operations = CreateOperations(
            (instrument, path, argument, token) =>
            {
                cancellation.Cancel();
                return Task.FromCanceled<RuntimeCommand>(token);
            },
            isSessionFaulted: static () => true,
            runtimeEndpoint: endpoint,
            timeProvider: new FixedTimeProvider());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            operations.ExecuteAsync(
                InstrumentId(),
                Kel103ModeSelectionMapping.ConstantCurrent.CommandPath,
                argument: null,
                cancellationToken: cancellation.Token));

        Assert.Equal(EndpointConnectionState.Faulted, endpoint.ConnectionStatus.State);
    }

    [Fact]
    public async Task Execute_NullArgumentsAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new Kel103EndpointAttachmentCommandOperations(null!));
        Assert.Throws<ArgumentNullException>(() =>
            new Kel103EndpointAttachmentCommandOperations(
                null!,
                static () => false,
                null,
                TimeProvider.System));
        Assert.Throws<ArgumentNullException>(() =>
            new Kel103EndpointAttachmentCommandOperations(
                (instrument, path, argument, token) =>
                    Task.FromResult(RuntimeCommand(path)),
                null!,
                null,
                TimeProvider.System));
        Assert.Throws<ArgumentNullException>(() =>
            new Kel103EndpointAttachmentCommandOperations(
                (instrument, path, argument, token) =>
                    Task.FromResult(RuntimeCommand(path)),
                static () => false,
                null,
                null!));

        var operations = CreateOperations((instrument, path, argument, token) =>
            Task.FromResult(RuntimeCommand(path)));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            operations.ExecuteAsync(
                null!,
                Kel103ModeSelectionMapping.ConstantCurrent.CommandPath,
                argument: null));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            operations.ExecuteAsync(
                InstrumentId(),
                null!,
                argument: null));
    }

    [Fact]
    public void Assembly_DoesNotReferencePresentationOrRemoteLayers()
    {
        string[] references = typeof(Kel103EndpointAttachmentCommandOperations).Assembly
            .GetReferencedAssemblies()
            .Select(value => value.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(references, name => name.Contains("Grpc", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.Contains("Wpf", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name == "Hase.Client");
        Assert.DoesNotContain(references, name => name == "Hase.DesktopHost");
    }

    private static Kel103EndpointAttachmentCommandOperations CreateOperations(
        Func<InstrumentId, DescriptorPath, object?, CancellationToken, Task<RuntimeCommand>> executeAsync,
        Func<bool>? isSessionFaulted = null,
        RuntimeEndpoint? runtimeEndpoint = null,
        TimeProvider? timeProvider = null) =>
        new(
            executeAsync,
            isSessionFaulted ?? SessionIsUsable,
            runtimeEndpoint,
            timeProvider ?? TimeProvider.System);

    private static bool SessionIsUsable() => false;

    private static InstrumentId InstrumentId() => new("electronic-load-01");

    private static DescriptorPath InputOrModePath(int category) =>
        category switch
        {
            0 => Kel103ModeSelectionMapping.ConstantCurrent.CommandPath,
            1 => Kel103InputControlMapping.Activate.CommandPath,
            2 => Kel103InputControlMapping.Deactivate.CommandPath,
            _ => Kel103InputControlMapping.ShortCircuitActivate.CommandPath
        };

    private static RuntimeCommand RuntimeCommand(DescriptorPath path)
    {
        bool inputControl = Kel103InputControlMapping.All.Any(
            mapping => mapping.CommandPath == path);
        RuntimeEndpoint endpoint = new RuntimeContext().CreateEndpoint(
            (inputControl
                ? Kel103ControlledInputDefinition.EndpointDefinition
                : Kel103ControlledSetpointDefinition.EndpointDefinition)
            .Materialize(new EndpointId("test-endpoint")));
        return endpoint.Instruments.Single().Commands.Single(
            command => command.Descriptor.Path == path);
    }

    private static RuntimeEndpoint ReadyEndpoint()
    {
        RuntimeEndpoint endpoint = new RuntimeContext().CreateEndpoint(
            Kel103ControlledSetpointDefinition.EndpointDefinition.Materialize(
                new EndpointId("fault-projection-test")));
        endpoint.UpdateConnectionStatus(
            new EndpointConnectionStatus(EndpointConnectionState.Ready));
        return endpoint;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public static DateTimeOffset Timestamp { get; } =
            new(2026, 8, 5, 20, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Timestamp;
    }
}
