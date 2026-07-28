using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Northbound;

namespace Hase.DesktopHost.Tests;

public sealed class DesktopRuntimeHostOperatorTests
{
    [Fact]
    public void Constructor_WithNullPropertyService_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "propertyService",
            () => new DesktopRuntimeHostOperator(
                null!,
                new RecordingCommandService()));
    }

    [Fact]
    public void Constructor_WithNullCommandService_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "commandService",
            () => new DesktopRuntimeHostOperator(
                new RecordingPropertyService(),
                null!));
    }

    [Fact]
    public async Task ReadPropertyAsync_ShouldDelegateExactlyOnceAndReturnSameResult()
    {
        RuntimeHostPropertyOperationResult expected =
            RuntimeHostPropertyOperationResult.Failed(
                RuntimeHostPropertyOperationStatus.EndpointUnavailable);
        var propertyService =
            new RecordingPropertyService
            {
                Result =
                    expected
            };
        var service =
            new DesktopRuntimeHostOperator(
                propertyService,
                new RecordingCommandService());
        RuntimeHostPropertyTarget target =
            CreatePropertyTarget();
        using var cancellationSource =
            new CancellationTokenSource();

        RuntimeHostPropertyOperationResult actual =
            await service.ReadPropertyAsync(
                target,
                cancellationSource.Token);

        Assert.Same(
            expected,
            actual);
        Assert.Equal(
            1,
            propertyService.ReadCount);
        Assert.Same(
            target,
            propertyService.ReadTarget);
        Assert.Equal(
            cancellationSource.Token,
            propertyService.ReadCancellationToken);
    }

    [Fact]
    public async Task ReadPropertyAsync_WhenServiceThrows_ShouldPropagateWithoutRetry()
    {
        var expected =
            new InvalidOperationException(
                "Read failed.");
        var propertyService =
            new RecordingPropertyService
            {
                Exception =
                    expected
            };
        var service =
            new DesktopRuntimeHostOperator(
                propertyService,
                new RecordingCommandService());

        InvalidOperationException actual =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ReadPropertyAsync(
                    CreatePropertyTarget()));

        Assert.Same(
            expected,
            actual);
        Assert.Equal(
            1,
            propertyService.ReadCount);
    }

    [Fact]
    public async Task ReadPropertyAsync_WithNullTarget_ShouldThrowBeforeDelegation()
    {
        var propertyService =
            new RecordingPropertyService();
        var service =
            new DesktopRuntimeHostOperator(
                propertyService,
                new RecordingCommandService());

        await Assert.ThrowsAsync<ArgumentNullException>(
            "target",
            () => service.ReadPropertyAsync(
                null!));

        Assert.Equal(
            0,
            propertyService.ReadCount);
    }

    [Fact]
    public async Task WritePropertyAsync_ShouldDelegateExactlyOnceAndReturnSameResult()
    {
        RuntimeHostPropertyOperationResult expected =
            RuntimeHostPropertyOperationResult.Failed(
                RuntimeHostPropertyOperationStatus.EndpointRejected,
                "Rejected by endpoint.");

        var propertyService = new RecordingPropertyService
        {
            Result = expected
        };

        var service = new DesktopRuntimeHostOperator(
            propertyService,
            new RecordingCommandService());

        RuntimeHostPropertyTarget target = CreatePropertyTarget();
        object requestedValue = true;
        using var cancellationSource = new CancellationTokenSource();

        RuntimeHostPropertyOperationResult actual =
            await service.WritePropertyAsync(
                target,
                requestedValue,
                cancellationSource.Token);

        Assert.Same(expected, actual);
        Assert.Equal(1, propertyService.WriteCount);
        Assert.Same(target, propertyService.Target);
        Assert.Same(requestedValue, propertyService.RequestedValue);
        Assert.Equal(
            cancellationSource.Token,
            propertyService.CancellationToken);
    }

    [Fact]
    public async Task WritePropertyAsync_WhenServiceThrows_ShouldPropagateWithoutRetry()
    {
        var expected = new InvalidOperationException("Write failed.");
        var propertyService = new RecordingPropertyService
        {
            Exception = expected
        };

        var service = new DesktopRuntimeHostOperator(
            propertyService,
            new RecordingCommandService());

        InvalidOperationException actual =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.WritePropertyAsync(
                    CreatePropertyTarget(),
                    requestedValue: false));

        Assert.Same(expected, actual);
        Assert.Equal(1, propertyService.WriteCount);
    }

    [Fact]
    public async Task WritePropertyAsync_WithNullTarget_ShouldThrowBeforeDelegation()
    {
        var propertyService = new RecordingPropertyService();
        var service = new DesktopRuntimeHostOperator(
            propertyService,
            new RecordingCommandService());

        await Assert.ThrowsAsync<ArgumentNullException>(
            "target",
            () => service.WritePropertyAsync(
                null!,
                requestedValue: true));

        Assert.Equal(0, propertyService.WriteCount);
    }

    [Fact]
    public async Task ExecuteCommandAsync_ShouldDelegateExactlyOnceAndReturnSameResult()
    {
        RuntimeHostCommandOperationResult expected =
            RuntimeHostCommandOperationResult.Failed(
                RuntimeHostCommandOperationStatus.EndpointRejected,
                "Rejected by endpoint.");

        var commandService = new RecordingCommandService
        {
            Result = expected
        };

        var service = new DesktopRuntimeHostOperator(
            new RecordingPropertyService(),
            commandService);

        RuntimeHostCommandTarget target = CreateCommandTarget();
        object argument = 42;
        using var cancellationSource = new CancellationTokenSource();

        RuntimeHostCommandOperationResult actual =
            await service.ExecuteCommandAsync(
                target,
                argument,
                cancellationSource.Token);

        Assert.Same(expected, actual);
        Assert.Equal(1, commandService.ExecuteCount);
        Assert.Same(target, commandService.Target);
        Assert.Same(argument, commandService.Argument);
        Assert.Equal(
            cancellationSource.Token,
            commandService.CancellationToken);
    }

    [Fact]
    public async Task ExecuteCommandAsync_WhenServiceThrows_ShouldPropagateWithoutRetry()
    {
        var expected = new InvalidOperationException("Command failed.");
        var commandService = new RecordingCommandService
        {
            Exception = expected
        };

        var service = new DesktopRuntimeHostOperator(
            new RecordingPropertyService(),
            commandService);

        InvalidOperationException actual =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ExecuteCommandAsync(
                    CreateCommandTarget(),
                    argument: null));

        Assert.Same(expected, actual);
        Assert.Equal(1, commandService.ExecuteCount);
    }

    [Fact]
    public async Task ExecuteCommandAsync_WithNullTarget_ShouldThrowBeforeDelegation()
    {
        var commandService = new RecordingCommandService();
        var service = new DesktopRuntimeHostOperator(
            new RecordingPropertyService(),
            commandService);

        await Assert.ThrowsAsync<ArgumentNullException>(
            "target",
            () => service.ExecuteCommandAsync(
                null!,
                argument: null));

        Assert.Equal(0, commandService.ExecuteCount);
    }

    private static RuntimeHostPropertyTarget CreatePropertyTarget()
    {
        return new RuntimeHostPropertyTarget(
            new EndpointId("endpoint-01"),
            new RuntimeEndpointAttachmentGeneration(
                Guid.Parse("0a342b4d-22ef-46c9-8a45-8ab7f2671474")),
            new InstrumentId("instrument-01"),
            new PropertyId("property-01"));
    }

    private static RuntimeHostCommandTarget CreateCommandTarget()
    {
        return new RuntimeHostCommandTarget(
            new EndpointId("endpoint-01"),
            new RuntimeEndpointAttachmentGeneration(
                Guid.Parse("8f12f275-43c5-4a85-a922-5ad845b72df8")),
            new InstrumentId("instrument-01"),
            new DescriptorPath("Controller", "Toggle"));
    }

    private sealed class RecordingPropertyService : IRuntimeHostPropertyService
    {
        public RuntimeHostPropertyOperationResult Result { get; init; } =
            RuntimeHostPropertyOperationResult.Failed(
                RuntimeHostPropertyOperationStatus.EndpointUnavailable);

        public Exception? Exception { get; init; }

        public int WriteCount { get; private set; }

        public int ReadCount { get; private set; }

        public RuntimeHostPropertyTarget? ReadTarget { get; private set; }

        public CancellationToken ReadCancellationToken { get; private set; }

        public RuntimeHostPropertyTarget? Target { get; private set; }

        public object? RequestedValue { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public RuntimeHostCachedPropertyResult GetCached(
            RuntimeHostPropertyTarget target)
        {
            throw new NotSupportedException();
        }

        public Task<RuntimeHostPropertyOperationResult> ReadAsync(
            RuntimeHostPropertyTarget target,
            CancellationToken cancellationToken = default)
        {
            ReadCount++;
            ReadTarget =
                target;
            ReadCancellationToken =
                cancellationToken;

            return Exception is null
                ? Task.FromResult(
                    Result)
                : Task.FromException<RuntimeHostPropertyOperationResult>(
                    Exception);
        }

        public Task<RuntimeHostPropertyOperationResult> WriteAsync(
            RuntimeHostPropertyTarget target,
            object? requestedValue,
            CancellationToken cancellationToken = default)
        {
            WriteCount++;
            Target = target;
            RequestedValue = requestedValue;
            CancellationToken = cancellationToken;

            return Exception is null
                ? Task.FromResult(Result)
                : Task.FromException<RuntimeHostPropertyOperationResult>(
                    Exception);
        }
    }

    private sealed class RecordingCommandService : IRuntimeHostCommandService
    {
        public RuntimeHostCommandOperationResult Result { get; init; } =
            RuntimeHostCommandOperationResult.Failed(
                RuntimeHostCommandOperationStatus.EndpointUnavailable);

        public Exception? Exception { get; init; }

        public int ExecuteCount { get; private set; }

        public RuntimeHostCommandTarget? Target { get; private set; }

        public object? Argument { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task<RuntimeHostCommandOperationResult> ExecuteAsync(
            RuntimeHostCommandTarget target,
            object? argument,
            CancellationToken cancellationToken = default)
        {
            ExecuteCount++;
            Target = target;
            Argument = argument;
            CancellationToken = cancellationToken;

            return Exception is null
                ? Task.FromResult(Result)
                : Task.FromException<RuntimeHostCommandOperationResult>(
                    Exception);
        }
    }
}
