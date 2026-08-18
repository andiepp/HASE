namespace Hase.DesktopHost.App.Media;

internal sealed class RuntimeHostMediaWebViewReadiness
{
    public static readonly TimeSpan DefaultTimeout =
        TimeSpan.FromSeconds(10);

    private readonly TimeSpan timeout;
    private readonly TaskCompletionSource ready =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public RuntimeHostMediaWebViewReadiness(TimeSpan? timeout = null)
    {
        this.timeout = timeout ?? DefaultTimeout;
        if (this.timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                "The Runtime Host media WebView readiness timeout must be positive.");
        }
    }

    public bool IsReady => ready.Task.IsCompletedSuccessfully;

    public void Signal() => ready.TrySetResult();

    public async ValueTask WaitAsync(CancellationToken cancellationToken)
    {
        await ready.Task.WaitAsync(timeout, cancellationToken)
            .ConfigureAwait(false);
    }
}
