using Hase.Runtime.Northbound;

namespace Hase.DesktopHost;

/// <summary>
/// Provides the UI-independent operator boundary for the Desktop Runtime Host.
/// </summary>
public sealed class DesktopRuntimeHostOperator : IDesktopRuntimeHostOperator
{
    private readonly IRuntimeHostPropertyService propertyService;
    private readonly IRuntimeHostCommandService commandService;

    public DesktopRuntimeHostOperator(
        IRuntimeHostPropertyService propertyService,
        IRuntimeHostCommandService commandService)
    {
        this.propertyService = propertyService
            ?? throw new ArgumentNullException(nameof(propertyService));

        this.commandService = commandService
            ?? throw new ArgumentNullException(nameof(commandService));
    }

    public Task<RuntimeHostPropertyOperationResult> ReadPropertyAsync(
        RuntimeHostPropertyTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);

        return propertyService.ReadAsync(
            target,
            cancellationToken);
    }

    public Task<RuntimeHostPropertyOperationResult> WritePropertyAsync(
        RuntimeHostPropertyTarget target,
        object? requestedValue,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);

        return propertyService.WriteAsync(
            target,
            requestedValue,
            cancellationToken);
    }

    public Task<RuntimeHostCommandOperationResult> ExecuteCommandAsync(
        RuntimeHostCommandTarget target,
        object? argument,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);

        return commandService.ExecuteAsync(
            target,
            argument,
            cancellationToken);
    }
}
