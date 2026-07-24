using Hase.CompactProtocol;
using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Transport.Attachment;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class CompactEndpointAttachmentCommandOperationsTests
{
    private static readonly InstrumentId InstrumentId =
        new(
            "controller-one");

    private static readonly DescriptorPath CommandPath =
        new(
            "Controller",
            "ToggleLed");

    [Fact]
    public async Task ExecuteAsync_NullArgument_UsesMappedIdentifierOnce()
    {
        byte? capturedCommandId =
            null;

        int executeCallCount =
            0;

        var operations =
            new CompactEndpointAttachmentCommandOperations(
                CreateCommandMap(),
                (commandId, cancellationToken) =>
                {
                    executeCallCount++;

                    capturedCommandId =
                        commandId;

                    return Task.FromResult(
                        CompactCommandExecutionStatus.Success);
                });

        EndpointAttachmentCommandOperationResult result =
            await operations.ExecuteAsync(
                InstrumentId,
                CommandPath,
                argument: null);

        Assert.True(
            result.IsSuccess);

        Assert.Null(
            result.ReturnValue);

        Assert.True(
            capturedCommandId.HasValue);

        Assert.Equal(
            (byte)0x01,
            capturedCommandId.Value);

        Assert.Equal(
            1,
            executeCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_NonNullArgument_DoesNotExecute()
    {
        int executeCallCount =
            0;

        var operations =
            new CompactEndpointAttachmentCommandOperations(
                CreateCommandMap(),
                (commandId, cancellationToken) =>
                {
                    executeCallCount++;

                    return Task.FromResult(
                        CompactCommandExecutionStatus.Success);
                });

        EndpointAttachmentCommandOperationResult result =
            await operations.ExecuteAsync(
                InstrumentId,
                CommandPath,
                argument: true);

        Assert.Equal(
            EndpointAttachmentCommandOperationStatus.ArgumentNotSupported,
            result.Status);

        Assert.Equal(
            0,
            executeCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnmappedCommand_DoesNotExecute()
    {
        int executeCallCount =
            0;

        var operations =
            new CompactEndpointAttachmentCommandOperations(
                CreateCommandMap(),
                (commandId, cancellationToken) =>
                {
                    executeCallCount++;

                    return Task.FromResult(
                        CompactCommandExecutionStatus.Success);
                });

        EndpointAttachmentCommandOperationResult result =
            await operations.ExecuteAsync(
                InstrumentId,
                new DescriptorPath(
                    "Controller",
                    "Missing"),
                argument: null);

        Assert.Equal(
            EndpointAttachmentCommandOperationStatus.Failure,
            result.Status);

        Assert.Equal(
            0,
            executeCallCount);
    }

    [Theory]
    [InlineData(
        (int)CompactCommandExecutionStatus.UnknownCommand)]
    [InlineData(
        (int)CompactCommandExecutionStatus.ExecutionFailed)]
    public async Task ExecuteAsync_EndpointFailure_IsNormalized(
        int compactStatusValue)
    {
        CompactCommandExecutionStatus compactStatus =
            (CompactCommandExecutionStatus)compactStatusValue;

        var operations =
            new CompactEndpointAttachmentCommandOperations(
                CreateCommandMap(),
                (commandId, cancellationToken) =>
                    Task.FromResult(
                        compactStatus));

        EndpointAttachmentCommandOperationResult result =
            await operations.ExecuteAsync(
                InstrumentId,
                CommandPath,
                argument: null);

        Assert.Equal(
            EndpointAttachmentCommandOperationStatus.Failure,
            result.Status);

        Assert.Null(
            result.ReturnValue);
    }

    [Theory]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(IOException))]
    public async Task ExecuteAsync_ConnectionFailure_IsUnavailable(
        Type exceptionType)
    {
        Exception exception =
            (Exception)Activator.CreateInstance(
                exceptionType)!;

        var operations =
            new CompactEndpointAttachmentCommandOperations(
                CreateCommandMap(),
                (commandId, cancellationToken) =>
                    Task.FromException<
                        CompactCommandExecutionStatus>(
                            exception));

        EndpointAttachmentCommandOperationResult result =
            await operations.ExecuteAsync(
                InstrumentId,
                CommandPath,
                argument: null);

        Assert.Equal(
            EndpointAttachmentCommandOperationStatus.Unavailable,
            result.Status);
    }

    [Fact]
    public async Task ExecuteAsync_Timeout_IsTimedOut()
    {
        var operations =
            new CompactEndpointAttachmentCommandOperations(
                CreateCommandMap(),
                (commandId, cancellationToken) =>
                    Task.FromException<
                        CompactCommandExecutionStatus>(
                            new TimeoutException()));

        EndpointAttachmentCommandOperationResult result =
            await operations.ExecuteAsync(
                InstrumentId,
                CommandPath,
                argument: null);

        Assert.Equal(
            EndpointAttachmentCommandOperationStatus.TimedOut,
            result.Status);
    }

    [Fact]
    public async Task ExecuteAsync_PreCancelled_DoesNotExecute()
    {
        int executeCallCount =
            0;

        var operations =
            new CompactEndpointAttachmentCommandOperations(
                CreateCommandMap(),
                (commandId, cancellationToken) =>
                {
                    executeCallCount++;

                    return Task.FromResult(
                        CompactCommandExecutionStatus.Success);
                });

        using var cancellationSource =
            new CancellationTokenSource();

        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => operations.ExecuteAsync(
                InstrumentId,
                CommandPath,
                argument: null,
                cancellationSource.Token));

        Assert.Equal(
            0,
            executeCallCount);
    }

    private static CompactCommandMap CreateCommandMap()
    {
        var commandDescriptor =
            new CommandDescriptor(
                CommandPath,
                "Toggle LED");

        var instrumentDescriptor =
            new InstrumentDescriptor(
                InstrumentId,
                "Controller",
                new InstrumentKind(
                    "test"))
            {
                Interface =
                    new InstrumentInterface(
                        commands:
                        [
                            commandDescriptor
                        ])
            };

        var descriptorDefinition =
            new EndpointDescriptorDefinition(
                new EndpointMetadata(),
                [
                    instrumentDescriptor
                ]);

        return new CompactCommandMap(
            descriptorDefinition,
            [
                new CompactCommandMapping(
                    compactCommandId: 0x01,
                    InstrumentId,
                    CommandPath)
            ]);
    }
}