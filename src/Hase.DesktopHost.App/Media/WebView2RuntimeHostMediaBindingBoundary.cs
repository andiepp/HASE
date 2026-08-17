using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.IO;

namespace Hase.DesktopHost.App.Media;

public sealed class WebView2RuntimeHostMediaBindingBoundary : IAsyncDisposable
{
    private readonly WebView2 webView;
    private readonly string assetDirectory;
    private readonly RuntimeHostMediaBindingWebViewPolicy policy;
    private readonly RuntimeHostMediaBindingWebMessageValidator validator;
    private bool initialized;

    public WebView2RuntimeHostMediaBindingBoundary(
        WebView2 webView,
        string assetDirectory,
        RuntimeHostMediaBindingWebViewPolicy? policy = null,
        RuntimeHostMediaBindingWebMessageValidator? validator = null)
    {
        this.webView = webView ?? throw new ArgumentNullException(nameof(webView));
        ArgumentException.ThrowIfNullOrWhiteSpace(assetDirectory);
        this.assetDirectory = Path.GetFullPath(assetDirectory);
        this.policy = policy ?? new RuntimeHostMediaBindingWebViewPolicy();
        this.validator = validator ??
            new RuntimeHostMediaBindingWebMessageValidator();
    }

    public event Action<RuntimeHostMediaBindingWebMessage>? ValidatedMessage;

    public async Task InitializeAsync()
    {
        if (initialized)
        {
            return;
        }
        if (!Directory.Exists(assetDirectory))
        {
            throw new InvalidOperationException(
                "Media binding assets are unavailable.");
        }

        await webView.EnsureCoreWebView2Async().ConfigureAwait(true);
        CoreWebView2 core = webView.CoreWebView2 ??
            throw new InvalidOperationException(
                "WebView2 initialization failed.");
        core.SetVirtualHostNameToFolderMapping(
            RuntimeHostMediaWebViewPolicy.VirtualHostName,
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
        core.AddWebResourceRequestedFilter(
            "*",
            CoreWebView2WebResourceContext.All);
        Attach(core);
        initialized = true;
        core.Navigate(
            RuntimeHostMediaBindingWebViewPolicy.ApplicationUri.AbsoluteUri);
    }

    public async ValueTask DisposeAsync()
    {
        policy.EndDiscovery();
        if (initialized && webView.CoreWebView2 is CoreWebView2 core)
        {
            try
            {
                core.PostWebMessageAsJson(
                    "{\"version\":1,\"kind\":\"stop-discovery\"}");
            }
            catch (InvalidOperationException)
            {
                // Browser teardown is already terminal.
            }
            Detach(core);
        }
        initialized = false;
        await Task.CompletedTask;
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

    private void OnPermissionRequested(
        object? sender,
        CoreWebView2PermissionRequestedEventArgs args)
    {
        RuntimeHostMediaPermissionKind kind = args.PermissionKind switch
        {
            CoreWebView2PermissionKind.Camera =>
                RuntimeHostMediaPermissionKind.Camera,
            CoreWebView2PermissionKind.Microphone =>
                RuntimeHostMediaPermissionKind.Microphone,
            _ => RuntimeHostMediaPermissionKind.Other
        };
        args.State = policy.IsPermissionAllowed(args.Uri, kind)
            ? CoreWebView2PermissionState.Allow
            : CoreWebView2PermissionState.Deny;
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
            Stream.Null,
            403,
            "Forbidden",
            "Content-Type: text/plain\r\nCache-Control: no-store");
    }

    private void OnWebMessageReceived(
        object? sender,
        CoreWebView2WebMessageReceivedEventArgs args)
    {
        if (!policy.IsAllowedResource(args.Source) ||
            !validator.TryValidate(
                args.WebMessageAsJson,
                out RuntimeHostMediaBindingWebMessage? message) ||
            message is null)
        {
            return;
        }

        if (message.Kind ==
            RuntimeHostMediaBindingWebMessageKind.DiscoveryRequested)
        {
            policy.ArmDiscovery();
            webView.CoreWebView2.PostWebMessageAsJson(
                "{\"version\":1,\"kind\":\"discovery-authorized\"}");
        }
        else if (message.Kind is
            RuntimeHostMediaBindingWebMessageKind.SelectionConfirmed or
            RuntimeHostMediaBindingWebMessageKind.Cancelled or
            RuntimeHostMediaBindingWebMessageKind.Faulted)
        {
            policy.EndDiscovery();
        }

        ValidatedMessage?.Invoke(message);
    }
}
