using System.ComponentModel;
using System.IO;
using System.Windows;
using Hase.Client.Wpf.AppHost.Media;
using Hase.Client.Wpf.Services;
using Hase.Client.Wpf.ViewModels;
using Hase.Client.Wpf.Views;
using Microsoft.Web.WebView2.Wpf;

namespace Hase.Client.Wpf.AppHost;

public sealed class ClientMediaWindowController : IClientMediaWindowController
{
    private readonly RuntimeHostMediaViewModel viewModel;
    private readonly WebView2 mediaWebView = new();
    private readonly WebView2ClientMediaPresentationBoundary presentationBoundary;
    private MediaWindow? window;
    private bool closing;

    public ClientMediaWindowController(RuntimeHostMediaViewModel viewModel)
    {
        this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        presentationBoundary = new WebView2ClientMediaPresentationBoundary(
            mediaWebView,
            Path.Combine(AppContext.BaseDirectory, "Media", "Assets"));
    }

    public IClientMediaPresentationBoundary PresentationBoundary =>
        presentationBoundary;

    public void Open()
    {
        // The WebView2 keeps its browser host only while it stays parented in
        // one window. Retain the window across an operator close and reuse it,
        // so the presentation boundary never outlives the browser it describes.
        closing = false;
        MediaWindow active = window ??= CreateWindow();
        if (active.WindowState == WindowState.Minimized)
        {
            active.WindowState = WindowState.Normal;
        }

        active.Show();
        active.Activate();
    }

    public void Close()
    {
        closing = true;
        MediaWindow? active = window;
        window = null;
        if (active is not null)
        {
            active.Closing -= WindowClosing;
            active.MediaPresentationSurface.Content = null;
            active.Close();
        }

        // Nothing else owns the browser host. Leaving it alive keeps the
        // client process running after every window has closed.
        mediaWebView.Dispose();
    }

    private MediaWindow CreateWindow()
    {
        Window? owner = Application.Current?.MainWindow;
        MediaWindow created = new(viewModel)
        {
            Owner = owner
        };
        created.MediaPresentationSurface.Content = mediaWebView;
        created.Closing += WindowClosing;
        created.Closed += WindowClosed;
        if (owner is not null)
        {
            // An owner close disposes owned windows. Release the retained
            // window first so shutdown is never blocked by the hide.
            owner.Closing += OwnerClosing;
        }

        return created;
    }

    private void WindowClosed(object? sender, EventArgs eventArgs)
    {
        // A real close unparents the WebView2 immediately, while the
        // application is still running normally.
        if (sender is MediaWindow closed)
        {
            closed.MediaPresentationSurface.Content = null;
        }

        window = null;
    }

    private void OwnerClosing(object? sender, CancelEventArgs eventArgs) =>
        closing = true;

    private void WindowClosing(object? sender, CancelEventArgs eventArgs)
    {
        if (closing)
        {
            return;
        }

        eventArgs.Cancel = true;
        StopActiveSession();
        ((Window)sender!).Hide();
    }

    private void StopActiveSession()
    {
        // Hiding the window must not leave the runtime host streaming an
        // invisible camera.
        if (viewModel.StopCommand.CanExecute())
        {
            viewModel.StopCommand.Execute();
        }
    }
}
