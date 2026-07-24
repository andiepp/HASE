using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class EndpointAttachmentCommandPortTests
{
    [Fact]
    public void AttachmentSession_ExposesAttachmentBoundCommandOperations()
    {
        Type? commandOperationsType =
            typeof(IEndpointAttachmentSession)
                .GetProperty(
                    nameof(
                        IEndpointAttachmentSession.CommandOperations))
                ?.PropertyType;

        Assert.Equal(
            typeof(IEndpointAttachmentCommandOperations),
            commandOperationsType);
    }

    [Fact]
    public async Task DefaultCommandOperations_ReturnUnavailable()
    {
        IEndpointAttachmentSession session =
            new TestEndpointAttachmentSession();

        EndpointAttachmentCommandOperationResult result =
            await session.CommandOperations.ExecuteAsync(
                new InstrumentId(
                    "instrument-one"),
                new DescriptorPath(
                    "Controller",
                    "ToggleLed"),
                argument: null);

        Assert.Equal(
            EndpointAttachmentCommandOperationStatus.Unavailable,
            result.Status);

        Assert.False(
            result.IsSuccess);

        Assert.Null(
            result.ReturnValue);
    }

    [Fact]
    public async Task DefaultCommandOperations_PreCancelled_Throws()
    {
        IEndpointAttachmentSession session =
            new TestEndpointAttachmentSession();

        using var cancellationSource =
            new CancellationTokenSource();

        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => session.CommandOperations.ExecuteAsync(
                new InstrumentId(
                    "instrument-one"),
                new DescriptorPath(
                    "Controller",
                    "ToggleLed"),
                argument: null,
                cancellationSource.Token));
    }

    [Fact]
    public async Task DefaultCommandOperations_NullInstrumentId_Throws()
    {
        IEndpointAttachmentSession session =
            new TestEndpointAttachmentSession();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => session.CommandOperations.ExecuteAsync(
                null!,
                new DescriptorPath(
                    "Controller",
                    "ToggleLed"),
                argument: null));
    }

    [Fact]
    public async Task DefaultCommandOperations_NullCommandPath_Throws()
    {
        IEndpointAttachmentSession session =
            new TestEndpointAttachmentSession();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => session.CommandOperations.ExecuteAsync(
                new InstrumentId(
                    "instrument-one"),
                null!,
                argument: null));
    }

    private sealed class TestEndpointAttachmentSession
        : IEndpointAttachmentSession
    {
        public EndpointAttachmentRequest Request =>
            null!;

        public RuntimeEndpoint RuntimeEndpoint =>
            null!;

        public Task ShutdownAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}