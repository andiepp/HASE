using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Northbound;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;

namespace Hase.Runtime.Tests.Northbound;

public sealed class RuntimeHostCommandServiceTests
{
    private static readonly InstrumentId InstrumentId =
        new(
            "controller-one");

    private static readonly DescriptorPath CommandPath =
        new(
            "Controller",
            "ToggleLed");

    [Fact]
    public void Constructor_NullProjection_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeHostCommandService(
                null!));
    }

    [Fact]
    public async Task ExecuteAsync_Success_PassesArgumentAndReturnValue()
    {
        var commandOperations =
            new TestCommandOperations(
                EndpointAttachmentCommandOperationResult.Successful(
                    "confirmed"));

        TestContext context =
            CreateContext(
                commandOperations);

        object argument =
            42;

        RuntimeHostCommandOperationResult result =
            await context.Service.ExecuteAsync(
                context.Target,
                argument);

        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            "confirmed",
            result.ReturnValue);

        Assert.Equal(
            1,
            commandOperations.ExecuteCallCount);

        Assert.Same(
            argument,
            commandOperations.LastArgument);
    }

    [Fact]
    public async Task ExecuteAsync_StaleGeneration_DoesNotReachAttachment()
    {
        var commandOperations =
            new TestCommandOperations(
                EndpointAttachmentCommandOperationResult.Successful());

        TestContext context =
            CreateContext(
                commandOperations);

        var staleTarget =
            new RuntimeHostCommandTarget(
                context.Target.EndpointId,
                RuntimeEndpointAttachmentGeneration.CreateNew(),
                context.Target.InstrumentId,
                context.Target.CommandPath);

        RuntimeHostCommandOperationResult result =
            await context.Service.ExecuteAsync(
                staleTarget,
                argument: null);

        Assert.Equal(
            RuntimeHostCommandOperationStatus.AttachmentNotCurrent,
            result.Status);

        Assert.Equal(
            0,
            commandOperations.ExecuteCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_MissingInstrument_DoesNotReachAttachment()
    {
        var commandOperations =
            new TestCommandOperations(
                EndpointAttachmentCommandOperationResult.Successful());

        TestContext context =
            CreateContext(
                commandOperations);

        var target =
            new RuntimeHostCommandTarget(
                context.Target.EndpointId,
                context.Target.AttachmentGeneration,
                new InstrumentId(
                    "missing"),
                context.Target.CommandPath);

        RuntimeHostCommandOperationResult result =
            await context.Service.ExecuteAsync(
                target,
                argument: null);

        Assert.Equal(
            RuntimeHostCommandOperationStatus.InstrumentNotFound,
            result.Status);

        Assert.Equal(
            0,
            commandOperations.ExecuteCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_MissingCommand_DoesNotReachAttachment()
    {
        var commandOperations =
            new TestCommandOperations(
                EndpointAttachmentCommandOperationResult.Successful());

        TestContext context =
            CreateContext(
                commandOperations);

        var target =
            new RuntimeHostCommandTarget(
                context.Target.EndpointId,
                context.Target.AttachmentGeneration,
                context.Target.InstrumentId,
                new DescriptorPath(
                    "Controller",
                    "Missing"));

        RuntimeHostCommandOperationResult result =
            await context.Service.ExecuteAsync(
                target,
                argument: null);

        Assert.Equal(
            RuntimeHostCommandOperationStatus.CommandNotFound,
            result.Status);

        Assert.Equal(
            0,
            commandOperations.ExecuteCallCount);
    }

    [Theory]
    [InlineData(
        EndpointAttachmentCommandOperationStatus.ArgumentNotSupported,
        RuntimeHostCommandOperationStatus.ArgumentNotSupported)]
    [InlineData(
        EndpointAttachmentCommandOperationStatus.Rejected,
        RuntimeHostCommandOperationStatus.EndpointRejected)]
    [InlineData(
        EndpointAttachmentCommandOperationStatus.Failure,
        RuntimeHostCommandOperationStatus.EndpointFailure)]
    [InlineData(
        EndpointAttachmentCommandOperationStatus.Unavailable,
        RuntimeHostCommandOperationStatus.EndpointUnavailable)]
    [InlineData(
        EndpointAttachmentCommandOperationStatus.TimedOut,
        RuntimeHostCommandOperationStatus.TimedOut)]
    public async Task ExecuteAsync_MapsAttachmentFailure(
        EndpointAttachmentCommandOperationStatus attachmentStatus,
        RuntimeHostCommandOperationStatus expectedStatus)
    {
        var commandOperations =
            new TestCommandOperations(
                EndpointAttachmentCommandOperationResult.Failed(
                    attachmentStatus,
                    " attachment diagnostic "));

        TestContext context =
            CreateContext(
                commandOperations);

        RuntimeHostCommandOperationResult result =
            await context.Service.ExecuteAsync(
                context.Target,
                argument: null);

        Assert.Equal(
            expectedStatus,
            result.Status);

        Assert.Equal(
            "attachment diagnostic",
            result.Diagnostic);

        Assert.Null(
            result.ReturnValue);
    }

    [Fact]
    public async Task ExecuteAsync_PreCancelled_DoesNotReachAttachment()
    {
        var commandOperations =
            new TestCommandOperations(
                EndpointAttachmentCommandOperationResult.Successful());

        TestContext context =
            CreateContext(
                commandOperations);

        using var cancellationSource =
            new CancellationTokenSource();

        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => context.Service.ExecuteAsync(
                context.Target,
                argument: null,
                cancellationSource.Token));

        Assert.Equal(
            0,
            commandOperations.ExecuteCallCount);
    }

    private static TestContext CreateContext(
        TestCommandOperations commandOperations)
    {
        RuntimeEndpointAttachmentInventoryEntry entry =
            CreateEntry(
                commandOperations);

        var projection =
            new RuntimeHostAttachmentProjection(
                new TestAttachmentInventory(
                    entry));

        RuntimeHostPublishedAttachment attachment =
            Assert.Single(
                projection.List());

        var target =
            new RuntimeHostCommandTarget(
                entry.EndpointId,
                attachment.Generation,
                InstrumentId,
                CommandPath);

        IRuntimeHostCommandService service =
            new RuntimeHostCommandService(
                projection);

        return new TestContext(
            service,
            target);
    }

    private static RuntimeEndpointAttachmentInventoryEntry CreateEntry(
        TestCommandOperations commandOperations)
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

        var runtimeEndpoint =
            new RuntimeEndpoint(
                new RuntimeContext(),
                new EndpointDescriptor(
                    new EndpointId(
                        "endpoint-one"),
                    [
                        instrumentDescriptor
                    ]));

        return new RuntimeEndpointAttachmentInventoryEntry(
            new TestEndpointAttachmentSession(
                runtimeEndpoint,
                commandOperations));
    }

    private sealed record TestContext(
        IRuntimeHostCommandService Service,
        RuntimeHostCommandTarget Target);

    private sealed class TestAttachmentInventory
        : IRuntimeEndpointAttachmentInventory
    {
        private readonly IReadOnlyList<
            RuntimeEndpointAttachmentInventoryEntry>
            _entries;

        public TestAttachmentInventory(
            params RuntimeEndpointAttachmentInventoryEntry[] entries)
        {
            _entries =
                entries.ToArray();
        }

        public Task<RuntimeEndpointAttachmentInventoryEntry> AttachAsync(
            EndpointAttachmentRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public RuntimeEndpointAttachmentInventoryEntry? Find(
            EndpointId endpointId)
        {
            return _entries.FirstOrDefault(
                entry =>
                    entry.EndpointId
                    == endpointId);
        }

        public IReadOnlyList<RuntimeEndpointAttachmentInventoryEntry> List()
        {
            return _entries.ToArray();
        }

        public Task<bool> DetachAsync(
            EndpointId endpointId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestEndpointAttachmentSession
        : IEndpointAttachmentSession
    {
        public TestEndpointAttachmentSession(
            RuntimeEndpoint runtimeEndpoint,
            IEndpointAttachmentCommandOperations commandOperations)
        {
            RuntimeEndpoint =
                runtimeEndpoint;

            CommandOperations =
                commandOperations;

            Request =
                null!;
        }

        public EndpointAttachmentRequest Request
        {
            get;
        }

        public RuntimeEndpoint RuntimeEndpoint
        {
            get;
        }

        public IEndpointAttachmentCommandOperations CommandOperations
        {
            get;
        }

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

    private sealed class TestCommandOperations
        : IEndpointAttachmentCommandOperations
    {
        private readonly EndpointAttachmentCommandOperationResult
            _result;

        public TestCommandOperations(
            EndpointAttachmentCommandOperationResult result)
        {
            _result =
                result;
        }

        public int ExecuteCallCount
        {
            get;
            private set;
        }

        public object? LastArgument
        {
            get;
            private set;
        }

        public Task<EndpointAttachmentCommandOperationResult> ExecuteAsync(
            InstrumentId instrumentId,
            DescriptorPath commandPath,
            object? argument,
            CancellationToken cancellationToken = default)
        {
            ExecuteCallCount++;

            LastArgument =
                argument;

            return Task.FromResult(
                _result);
        }
    }
}