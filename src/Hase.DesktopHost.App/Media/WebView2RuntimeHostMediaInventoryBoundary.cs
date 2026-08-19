using System.IO;
using Hase.Runtime.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Hase.DesktopHost.App.Media;

/// <summary>
/// Enumerates video-input identities only. It denies camera and microphone
/// permission requests and never calls getUserMedia.
/// </summary>
public sealed class WebView2RuntimeHostMediaInventoryBoundary :
    IRuntimeHostMediaInventoryWebBoundary
{
    private readonly WebView2 webView;
    private readonly string assetDirectory;
    private readonly RuntimeHostMediaInventoryWebViewPolicy policy;
    private readonly RuntimeHostMediaInventoryWebMessageValidator validator;
    private bool initialized;

    public WebView2RuntimeHostMediaInventoryBoundary(
        WebView2 webView,
        string assetDirectory,
        RuntimeHostMediaInventoryWebViewPolicy? policy = null,
        RuntimeHostMediaInventoryWebMessageValidator? validator = null)
    {
        this.webView = webView ?? throw new ArgumentNullException(nameof(webView));
        ArgumentException.ThrowIfNullOrWhiteSpace(assetDirectory);
        this.assetDirectory = Path.GetFullPath(assetDirectory);
        this.policy = policy ?? new RuntimeHostMediaInventoryWebViewPolicy();
        this.validator = validator ??
            new RuntimeHostMediaInventoryWebMessageValidator();
    }

    public event Action<IReadOnlyList<RuntimeHostMediaDeviceObservation>>?
        InventoryChanged;

    public async ValueTask InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!webView.Dispatcher.CheckAccess())
        {
            await webView.Dispatcher.InvokeAsync(
                () => InitializeAsync(cancellationToken).AsTask())
                .Task.Unwrap().ConfigureAwait(false);
            return;
        }
        if (initialized)
        {
            return;
        }
        if (!Directory.Exists(assetDirectory))
        {
            throw new InvalidOperationException("Media assets are unavailable.");
        }

        await webView.EnsureCoreWebView2Async().ConfigureAwait(true);
        CoreWebView2 core = webView.CoreWebView2 ??
            throw new InvalidOperationException("WebView2 initialization failed.");
        core.SetVirtualHostNameToFolderMapping(
            RuntimeHostMediaInventoryWebViewPolicy.VirtualHostName,
            assetDirectory,
            CoreWebView2HostResourceAccessKind.DenyCors);
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.AreHostObjectsAllowed = false;
        core.Settings.IsBuiltInErrorPageEnabled = false;
        core.Settings.IsGeneralAutofillEnabled = false;
        core.Settings.IsPasswordAutosaveEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsZoomControlEnabled = false;
        core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        Attach(core);
        initialized = true;
        core.Navigate(
            RuntimeHostMediaInventoryWebViewPolicy.ApplicationUri.AbsoluteUri);
    }

    public async ValueTask DisposeAsync()
    {
        if (!webView.Dispatcher.CheckAccess())
        {
            await webView.Dispatcher.InvokeAsync(() => DisposeAsync().AsTask())
                .Task.Unwrap().ConfigureAwait(false);
            return;
        }
        if (initialized && webView.CoreWebView2 is not null)
        {
            Detach(webView.CoreWebView2);
        }
        initialized = false;
    }

    private void Attach(CoreWebView2 core)
    {
        core.NavigationStarting += OnNavigationStarting;
        core.NewWindowRequested += OnNewWindowRequested;
        core.DownloadStarting += OnDownloadStarting;
        core.PermissionRequested += OnPermissionRequested;
        core.WebResourceRequested += OnWebResourceRequested;
        core.WebMessageReceived += OnWebMessageReceived;
    }

    private void Detach(CoreWebView2 core)
    {
        core.NavigationStarting -= OnNavigationStarting;
        core.NewWindowRequested -= OnNewWindowRequested;
        core.DownloadStarting -= OnDownloadStarting;
        core.PermissionRequested -= OnPermissionRequested;
        core.WebResourceRequested -= OnWebResourceRequested;
        core.WebMessageReceived -= OnWebMessageReceived;
    }

    private void OnNavigationStarting(
        object? sender,
        CoreWebView2NavigationStartingEventArgs args) =>
        args.Cancel = !policy.IsAllowedResource(args.Uri);

    private static void OnNewWindowRequested(
        object? sender,
        CoreWebView2NewWindowRequestedEventArgs args) => args.Handled = true;

    private static void OnDownloadStarting(
        object? sender,
        CoreWebView2DownloadStartingEventArgs args) => args.Cancel = true;

    private static void OnPermissionRequested(
        object? sender,
        CoreWebView2PermissionRequestedEventArgs args)
    {
        args.State = CoreWebView2PermissionState.Deny;
        args.Handled = true;
    }

    private void OnWebResourceRequested(
        object? sender,
        CoreWebView2WebResourceRequestedEventArgs args)
    {
        if (policy.IsAllowedResource(args.Request.Uri))
        {
            return;
        }
        args.Response = webView.CoreWebView2.Environment.CreateWebResourceResponse(
            null,
            403,
            "Forbidden",
            "Content-Type: text/plain\r\nCache-Control: no-store");
    }

    private void OnWebMessageReceived(
        object? sender,
        CoreWebView2WebMessageReceivedEventArgs args)
    {
        if (policy.IsAllowedResource(args.Source) &&
            validator.TryValidate(args.WebMessageAsJson, out var observations))
        {
            InventoryChanged?.Invoke(observations!);
        }
    }
}
