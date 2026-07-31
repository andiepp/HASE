namespace Hase.DesktopHost.App.Views;

public sealed class SingleInstanceDesktopWindowController
{
    private readonly Func<IDesktopModelessWindow> createWindow;
    private IDesktopModelessWindow? window;

    public SingleInstanceDesktopWindowController(
        Func<IDesktopModelessWindow> createWindow)
    {
        this.createWindow =
            createWindow
            ?? throw new ArgumentNullException(
                nameof(createWindow));
    }

    public void Open()
    {
        if (window is not null)
        {
            if (window.IsMinimized)
            {
                window.Restore();
            }

            window.ActivateWindow();
            return;
        }

        window =
            createWindow()
            ?? throw new InvalidOperationException(
                "The diagnostics-window factory returned null.");

        window.Closed +=
            OnWindowClosed;
        window.ShowWindow();
        window.ActivateWindow();
    }

    private void OnWindowClosed(
        object? sender,
        EventArgs eventArgs)
    {
        if (window is null)
        {
            return;
        }

        window.Closed -=
            OnWindowClosed;
        window =
            null;
    }
}
