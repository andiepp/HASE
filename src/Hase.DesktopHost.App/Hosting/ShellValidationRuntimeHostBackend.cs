namespace Hase.DesktopHost.App.Hosting;

public sealed class ShellValidationRuntimeHostBackend : IDesktopRuntimeHostBackend
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
