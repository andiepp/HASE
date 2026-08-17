using System.IO;
using System.Text.Json;
using Hase.Client.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Hase.Client.Wpf.AppHost.Media;

/// <summary>
/// Hardened receiver-only presentation boundary. Application composition
/// initializes it only after an explicit Client Start action.
/// </summary>
public sealed class WebView2ClientMediaPresentationBoundary :
    IClientMediaPresentationBoundary
{
    private readonly WebView2 webView;
    private readonly string assetDirectory;
    private readonly ClientMediaWebViewPolicy policy;
    private readonly ClientMediaWebMessageValidator validator;
    private bool initialized;
    private bool presentationActive;

    public WebView2ClientMediaPresentationBoundary(
        WebView2 webView,
        string assetDirectory,
        ClientMediaWebViewPolicy? policy = null,
        ClientMediaWebMessageValidator? validator = null)
    {
        this.webView = webView ?? throw new ArgumentNullException(nameof(webView));
        if (string.IsNullOrWhiteSpace(assetDirectory))
        {
            throw new ArgumentException(
                "A repository-owned media asset directory is required.",
                nameof(assetDirectory));
        }
        this.assetDirectory = Path.GetFullPath(assetDirectory);
        this.policy = policy ?? new ClientMediaWebViewPolicy();
        this.validator = validator ?? new ClientMediaWebMessageValidator();
    }

    public event Action<ClientMediaWebMessage>? ValidatedMessage;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!webView.Dispatcher.CheckAccess())
        {
            await webView.Dispatcher.InvokeAsync(
                () => InitializeAsync(cancellationToken)).Task.Unwrap()
                .ConfigureAwait(false);
            return;
        }
        EnsureUiThread();
        if (initialized)
        {
            return;
        }
        if (!Directory.Exists(assetDirectory))
        {
            throw new InvalidOperationException("Media presentation assets are unavailable.");
        }

        await webView.EnsureCoreWebView2Async().ConfigureAwait(true);
        var core = webView.CoreWebView2 ??
            throw new InvalidOperationException("WebView2 initialization failed.");
        core.SetVirtualHostNameToFolderMapping(
            ClientMediaWebViewPolicy.VirtualHostName,
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
        core.Navigate(ClientMediaWebViewPolicy.ApplicationUri.AbsoluteUri);
    }

    public void ClearPresentation()
    {
        if (!webView.Dispatcher.CheckAccess())
        {
            webView.Dispatcher.Invoke(ClearPresentation);
            return;
        }
        EnsureUiThread();
        if (webView.CoreWebView2 is not null)
        {
            webView.CoreWebView2.PostWebMessageAsJson(
                "{\"version\":1,\"kind\":\"clear-presentation\"}");
        }
        presentationActive = false;
    }

    public async Task BeginAsync(
        bool includeAudio,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!webView.Dispatcher.CheckAccess())
        {
            await webView.Dispatcher.InvokeAsync(
                () => BeginAsync(includeAudio, cancellationToken)).Task.Unwrap()
                .ConfigureAwait(false);
            return;
        }
        EnsureUiThread();
        await InitializeAsync(cancellationToken).ConfigureAwait(true);
        if (presentationActive)
        {
            throw new InvalidOperationException(
                "Media presentation is already active.");
        }

        presentationActive = true;
        webView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new
        {
            version = 1,
            kind = "begin-presentation",
            includeAudio
        }));
    }

    public void SubmitNegotiation(RemoteMediaNegotiationMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!webView.Dispatcher.CheckAccess())
        {
            webView.Dispatcher.Invoke(() => SubmitNegotiation(message));
            return;
        }
        EnsureUiThread();
        if (!presentationActive || webView.CoreWebView2 is null)
        {
            throw new InvalidOperationException(
                "Media presentation is not active for negotiation.");
        }
        if (message.Kind == RemoteMediaNegotiationKind.Answer)
        {
            throw new ArgumentException(
                "The Client is the only WebRTC answerer.", nameof(message));
        }

        webView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new
        {
            version = 1,
            kind = "apply-negotiation",
            sequence = message.Sequence,
            negotiationKind = ToWireKind(message.Kind),
            sensitivePayload = message.SensitivePayload
        }));
    }

    public async ValueTask DisposeAsync()
    {
        if (!webView.Dispatcher.CheckAccess())
        {
            await webView.Dispatcher.InvokeAsync(
                () => DisposeAsync().AsTask()).Task.Unwrap()
                .ConfigureAwait(false);
            return;
        }
        EnsureUiThread();
        if (initialized && webView.CoreWebView2 is not null)
        {
            webView.CoreWebView2.PostWebMessageAsJson(
                "{\"version\":1,\"kind\":\"clear-presentation\"}");
            Detach(webView.CoreWebView2);
        }
        presentationActive = false;
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

    private void OnNavigationStarting(object? sender,
        CoreWebView2NavigationStartingEventArgs args) =>
        args.Cancel = !policy.IsAllowedResource(args.Uri);

    private static void OnNewWindowRequested(object? sender,
        CoreWebView2NewWindowRequestedEventArgs args) => args.Handled = true;

    private static void OnDownloadStarting(object? sender,
        CoreWebView2DownloadStartingEventArgs args) => args.Cancel = true;

    private void OnPermissionRequested(object? sender,
        CoreWebView2PermissionRequestedEventArgs args)
    {
        args.State = CoreWebView2PermissionState.Deny;
        args.Handled = true;
    }

    private void OnWebResourceRequested(object? sender,
        CoreWebView2WebResourceRequestedEventArgs args)
    {
        if (policy.IsAllowedResource(args.Request.Uri))
        {
            return;
        }
        args.Response = webView.CoreWebView2.Environment.CreateWebResourceResponse(
            null, 403, "Forbidden",
            "Content-Type: text/plain\r\nCache-Control: no-store");
    }

    private void OnWebMessageReceived(object? sender,
        CoreWebView2WebMessageReceivedEventArgs args)
    {
        if (policy.IsAllowedResource(args.Source) &&
            validator.TryValidate(args.WebMessageAsJson, out var message))
        {
            ValidatedMessage?.Invoke(message!);
        }
    }

    private void EnsureUiThread()
    {
        if (!webView.Dispatcher.CheckAccess())
        {
            throw new InvalidOperationException(
                "The WebView2 presentation boundary requires its owning UI thread.");
        }
    }

    private static string ToWireKind(RemoteMediaNegotiationKind kind) =>
        kind switch
        {
            RemoteMediaNegotiationKind.Offer => "offer",
            RemoteMediaNegotiationKind.IceCandidate => "ice-candidate",
            RemoteMediaNegotiationKind.IceComplete => "ice-complete",
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind), kind, "A Runtime Host negotiation message is required.")
        };
}
