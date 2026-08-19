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
        if (window is not null)
        {
            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }

            window.Activate();
            return;
        }

        MediaWindow created = new(viewModel)
        {
            Owner = Application.Current?.MainWindow
        };
        created.MediaPresentationSurface.Content = mediaWebView;
        created.Closed += (_, _) =>
        {
            created.MediaPresentationSurface.Content = null;
            window = null;
        };
        window = created;
        created.Show();
    }

    public void Close()
    {
        MediaWindow? active = window;
        window = null;
        if (active is null)
        {
            return;
        }

        active.MediaPresentationSurface.Content = null;
        active.Close();
    }
}
