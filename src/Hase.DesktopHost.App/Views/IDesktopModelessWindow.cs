namespace Hase.DesktopHost.App.Views;

public interface IDesktopModelessWindow
{
    event EventHandler? Closed;

    bool IsMinimized
    {
        get;
    }

    void Restore();

    void ShowWindow();

    void ActivateWindow();
}
