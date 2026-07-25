using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostRemoteApiCommandServiceTests
{
    [Fact]
    public void Constructor_IncompleteCommandDependencies_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "commandService",
            () =>
                CreateService(
                    commandService: null,
                    new TestCommandTargetMapper(
                        CreateTarget()),
                    new TestCommandResultMapper(
                        new GrpcV1.CommandOperationResult()),
                    new TestRemoteValueMapper(
                        null)));

        Assert.Throws<ArgumentNullException>(
            "commandTargetMapper",
            () =>
                CreateService(
                    new TestCommandService(
                        CreateResult()),
                    commandTargetMapper: null,
                    new TestCommandResultMapper(
                        new GrpcV1.CommandOperationResult()),
                    new TestRemoteValueMapper(
                        null)));

        Assert.Throws<ArgumentNullException>(
            "commandResultMapper",
            () =>
                CreateService(
                    new TestCommandService(
                        CreateResult()),
                    new TestCommandTargetMapper(
                        CreateTarget()),
                    commandResultMapper: null,
                    new TestRemoteValueMapper(
                        null)));

        Assert.Throws<ArgumentNullException>(
            "remoteValueMapper",
            () =>
                CreateService(
                    new TestCommandService(
                        CreateResult()),
                    new TestCommandTargetMapper(
                        CreateTarget()),
                    new TestCommandResultMapper(
                        new GrpcV1.CommandOperationResult()),
                    remoteValueMapper: null));
    }

    [Fact]
    public async Task ExecuteCommand_NullRequest_ShouldThrow()
    {
        RuntimeHostRemoteApiService service =
            CreateConfiguredService(
                new TestCommandService(
                    CreateResult()),
                new TestCommandTargetMapper(
                    CreateTarget()),
                new TestCommandResultMapper(
                    new GrpcV1.CommandOperationResult()),
                new TestRemoteValueMapper(
                    null));

        await Assert.ThrowsAsync<ArgumentNullException>(
            "request",
            () =>
                service.ExecuteCommand(
                    null!,
                    null!));
    }

    [Fact]
    public async Task ExecuteCommand_NotConfigured_ShouldThrow()
    {
        var service =
            new RuntimeHostRemoteApiService(
                new TestSnapshotProvider(),
                RuntimeHostSnapshotMapperFactory.Create());

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    service.ExecuteCommand(
                        new GrpcV1.ExecuteCommandRequest(),
                        null!));

        Assert.Equal(
            "Command execution is not configured.",
            exception.Message);
    }

    [Fact]
    public async Task ExecuteCommand_ShouldMapArgumentExecuteOnceAndMapResult()
    {
        Northbound.RuntimeHostCommandTarget target =
            CreateTarget();
        Northbound.RuntimeHostCommandOperationResult commandResult =
            CreateResult();
        var mappedResult =
            new GrpcV1.CommandOperationResult
            {
                Status =
                    GrpcV1.CommandOperationStatus.Success
            };
        var commandService =
            new TestCommandService(
                commandResult);
        var targetMapper =
            new TestCommandTargetMapper(
                target);
        var resultMapper =
            new TestCommandResultMapper(
                mappedResult);
        var valueMapper =
            new TestRemoteValueMapper(
                true);
        RuntimeHostRemoteApiService service =
            CreateConfiguredService(
                commandService,
                targetMapper,
                resultMapper,
                valueMapper);
        var remoteArgument =
            new GrpcV1.RemoteValue
            {
                BooleanValue =
                    true
            };
        var request =
            new GrpcV1.ExecuteCommandRequest
            {
                Target =
                    new GrpcV1.CommandTarget(),
                Argument =
                    remoteArgument
            };

        GrpcV1.CommandOperationResult response =
            await service.ExecuteCommand(
                request,
                null!);

        Assert.Same(
            request.Target,
            targetMapper.Input);
        Assert.Same(
            remoteArgument,
            valueMapper.Input);
        Assert.Equal(
            1,
            commandService.ExecutionCount);
        Assert.Same(
            target,
            commandService.Target);
        Assert.Equal(
            true,
            commandService.Argument);
        Assert.Equal(
            CancellationToken.None,
            commandService.CancellationToken);
        Assert.Same(
            commandResult,
            resultMapper.Input);
        Assert.Same(
            mappedResult,
            response);
    }

    [Fact]
    public async Task ExecuteCommand_CommandServiceReturnsNull_ShouldThrow()
    {
        RuntimeHostRemoteApiService service =
            CreateConfiguredService(
                new TestCommandService(
                    null!),
                new TestCommandTargetMapper(
                    CreateTarget()),
                new TestCommandResultMapper(
                    new GrpcV1.CommandOperationResult()),
                new TestRemoteValueMapper(
                    null));

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    service.ExecuteCommand(
                        new GrpcV1.ExecuteCommandRequest(),
                        null!));

        Assert.Equal(
            "The runtime-host Command service returned null.",
            exception.Message);
    }

    [Fact]
    public async Task ExecuteCommand_ResultMapperReturnsNull_ShouldThrow()
    {
        RuntimeHostRemoteApiService service =
            CreateConfiguredService(
                new TestCommandService(
                    CreateResult()),
                new TestCommandTargetMapper(
                    CreateTarget()),
                new TestCommandResultMapper(
                    null!),
                new TestRemoteValueMapper(
                    null));

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    service.ExecuteCommand(
                        new GrpcV1.ExecuteCommandRequest(),
                        null!));

        Assert.Equal(
            "The Command operation result mapper returned null.",
            exception.Message);
    }

    private static RuntimeHostRemoteApiService CreateConfiguredService(
        Northbound.IRuntimeHostCommandService commandService,
        IRuntimeHostCommandTargetMapper commandTargetMapper,
        IRuntimeHostCommandOperationResultMapper commandResultMapper,
        IRemoteValueMapper remoteValueMapper)
    {
        return CreateService(
            commandService,
            commandTargetMapper,
            commandResultMapper,
            remoteValueMapper);
    }

    private static RuntimeHostRemoteApiService CreateService(
        Northbound.IRuntimeHostCommandService? commandService,
        IRuntimeHostCommandTargetMapper? commandTargetMapper,
        IRuntimeHostCommandOperationResultMapper? commandResultMapper,
        IRemoteValueMapper? remoteValueMapper)
    {
        return new RuntimeHostRemoteApiService(
            new TestSnapshotProvider(),
            RuntimeHostSnapshotMapperFactory.Create(),
            remoteValueMapper:
                remoteValueMapper,
            commandService:
                commandService,
            commandTargetMapper:
                commandTargetMapper,
            commandResultMapper:
                commandResultMapper);
    }

    private static Northbound.RuntimeHostCommandTarget CreateTarget()
    {
        return new Northbound.RuntimeHostCommandTarget(
            new EndpointId(
                "endpoint-01"),
            new Northbound.RuntimeEndpointAttachmentGeneration(
                new Guid(
                    "868e79d4-b1a4-4a63-81cd-5a800d9ba3fd")),
            new InstrumentId(
                "environment-sensor-01"),
            new DescriptorPath(
                "Calibration",
                "Reset"));
    }

    private static Northbound.RuntimeHostCommandOperationResult CreateResult()
    {
        return Northbound.RuntimeHostCommandOperationResult.Successful();
    }

    private sealed class TestSnapshotProvider
        : Northbound.IRuntimeHostSnapshotProvider
    {
        public Northbound.PublishedRuntimeHostSnapshot Capture()
        {
            return new Northbound.PublishedRuntimeHostSnapshot(
                new Northbound.RuntimeHostId(
                    "runtime-host-1"),
                Northbound.RuntimeHostApiVersion.Current,
                Array.Empty<Northbound.PublishedRuntimeEndpointSnapshot>());
        }
    }

    private sealed class TestCommandService
        : Northbound.IRuntimeHostCommandService
    {
        private readonly Northbound.RuntimeHostCommandOperationResult result;

        public TestCommandService(
            Northbound.RuntimeHostCommandOperationResult result)
        {
            this.result =
                result;
        }

        public int ExecutionCount
        {
            get;
            private set;
        }

        public Northbound.RuntimeHostCommandTarget? Target
        {
            get;
            private set;
        }

        public object? Argument
        {
            get;
            private set;
        }

        public CancellationToken CancellationToken
        {
            get;
            private set;
        }

        public Task<Northbound.RuntimeHostCommandOperationResult> ExecuteAsync(
            Northbound.RuntimeHostCommandTarget target,
            object? argument,
            CancellationToken cancellationToken = default)
        {
            ExecutionCount++;
            Target =
                target;
            Argument =
                argument;
            CancellationToken =
                cancellationToken;

            return Task.FromResult(
                result);
        }
    }

    private sealed class TestCommandTargetMapper
        : IRuntimeHostCommandTargetMapper
    {
        private readonly Northbound.RuntimeHostCommandTarget result;

        public TestCommandTargetMapper(
            Northbound.RuntimeHostCommandTarget result)
        {
            this.result =
                result;
        }

        public GrpcV1.CommandTarget? Input
        {
            get;
            private set;
        }

        public Northbound.RuntimeHostCommandTarget Map(
            GrpcV1.CommandTarget source)
        {
            Input =
                source;

            return result;
        }
    }

    private sealed class TestCommandResultMapper
        : IRuntimeHostCommandOperationResultMapper
    {
        private readonly GrpcV1.CommandOperationResult result;

        public TestCommandResultMapper(
            GrpcV1.CommandOperationResult result)
        {
            this.result =
                result;
        }

        public Northbound.RuntimeHostCommandOperationResult? Input
        {
            get;
            private set;
        }

        public GrpcV1.CommandOperationResult Map(
            Northbound.RuntimeHostCommandOperationResult result)
        {
            Input =
                result;

            return this.result;
        }
    }

    private sealed class TestRemoteValueMapper
        : IRemoteValueMapper
    {
        private readonly object? result;

        public TestRemoteValueMapper(
            object? result)
        {
            this.result =
                result;
        }

        public GrpcV1.RemoteValue? Input
        {
            get;
            private set;
        }

        public GrpcV1.RemoteValue Map(
            object value)
        {
            throw new NotSupportedException();
        }

        public object? MapToClr(
            GrpcV1.RemoteValue value)
        {
            Input =
                value;

            return result;
        }
    }
}
