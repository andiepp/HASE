using System.Windows.Threading;

namespace Hase.Client.Wpf.Services;

/// <summary>
/// Posts client presentation updates through one WPF dispatcher.
/// </summary>
public sealed class WpfClientUiDispatcher
    : IClientUiDispatcher
{
    private readonly Dispatcher dispatcher;

    public WpfClientUiDispatcher(
        Dispatcher dispatcher)
    {
        this.dispatcher =
            dispatcher
            ?? throw new ArgumentNullException(
                nameof(dispatcher));
    }

    public void Post(
        Action action)
    {
        ArgumentNullException.ThrowIfNull(
            action);

        _ =
            dispatcher.BeginInvoke(
                action);
    }
}
