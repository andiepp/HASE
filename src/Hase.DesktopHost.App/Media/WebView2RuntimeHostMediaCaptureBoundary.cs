using System.IO;
using System.Text.Json;
using Hase.Runtime.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Hase.DesktopHost.App.Media;

/// <summary>
/// Hardened Windows capture adapter. Application composition is intentionally
/// deferred: constructing this type does not initialize WebView2 or open a
/// device. Application composition initializes it only for an authorized
/// media-session Start operation.
/// </summary>
public sealed class WebView2RuntimeHostMediaCaptureBoundary :
    IRuntimeHostMediaWebBoundary
{
    private readonly WebView2 webView;
    private readonly string assetDirectory;
    private readonly RuntimeHostMediaWebViewPolicy policy;
    private readonly RuntimeHostMediaWebMessageValidator validator;
    private bool initialized;
    private bool captureActive;

    public WebView2RuntimeHostMediaCaptureBoundary(
        WebView2 webView,
        string assetDirectory,
        RuntimeHostMediaWebViewPolicy? policy = null,
        RuntimeHostMediaWebMessageValidator? validator = null)
    {
        this.webView = webView ?? throw new ArgumentNullException(nameof(webView));
        if (string.IsNullOrWhiteSpace(assetDirectory))
        {
            throw new ArgumentException(
                "A repository-owned media asset directory is required.",
                nameof(assetDirectory));
        }

        this.assetDirectory = Path.GetFullPath(assetDirectory);
        this.policy = policy ?? new RuntimeHostMediaWebViewPolicy();
        this.validator = validator ?? new RuntimeHostMediaWebMessageValidator();
    }

    public event Action<RuntimeHostMediaWebMessage>? ValidatedMessage;

    public async ValueTask OpenAsync(
        RuntimeHostMediaSourceConfiguration source,
        bool includeAudio,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        if (!webView.Dispatcher.CheckAccess())
        {
            await webView.Dispatcher.InvokeAsync(
                () => OpenAsync(source, includeAudio, cancellationToken).AsTask())
                .Task.Unwrap().ConfigureAwait(false);
            return;
        }
        EnsureUiThread();
        if (!Directory.Exists(assetDirectory))
        {
            throw new InvalidOperationException("Media assets are unavailable.");
        }

        await EnsureInitializedAsync().ConfigureAwait(true);
        if (captureActive)
        {
            throw new InvalidOperationException("Media capture is already active.");
        }

        policy.BeginCapture(includeAudio);
        captureActive = true;
        var command = new
        {
            version = 1,
            kind = "start-capture",
            videoDeviceId = source.VideoDeviceId,
            audioDeviceId = includeAudio ? source.AudioDeviceId : null,
            includeAudio
        };
        webView.CoreWebView2.PostWebMessageAsJson(
            JsonSerializer.Serialize(command));
    }

    public async ValueTask SubmitNegotiationAsync(
        RuntimeHostMediaNegotiationMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();
        if (!webView.Dispatcher.CheckAccess())
        {
            await webView.Dispatcher.InvokeAsync(
                () => SubmitNegotiationAsync(message, cancellationToken).AsTask())
                .Task.Unwrap().ConfigureAwait(false);
            return;
        }
        EnsureUiThread();
        if (!captureActive || webView.CoreWebView2 is null)
        {
            throw new InvalidOperationException(
                "Media capture is not active for negotiation.");
        }
        if (message.Kind is RuntimeHostMediaNegotiationKind.Offer)
        {
            throw new ArgumentException(
                "The Runtime Host is the only WebRTC offerer.",
                nameof(message));
        }

        var command = new
        {
            version = 1,
            kind = "apply-negotiation",
            sequence = message.Sequence,
            negotiationKind = ToWireKind(message.Kind),
            sensitivePayload = message.SensitivePayload
        };
        webView.CoreWebView2.PostWebMessageAsJson(
            JsonSerializer.Serialize(command));
    }

    public async ValueTask CloseAsync(CancellationToken cancellationToken)
    {
        if (!webView.Dispatcher.CheckAccess())
        {
            await webView.Dispatcher.InvokeAsync(
                () => CloseAsync(cancellationToken).AsTask())
                .Task.Unwrap().ConfigureAwait(false);
            return;
        }
        EnsureUiThread();
        if (captureActive && webView.CoreWebView2 is not null)
        {
            webView.CoreWebView2.PostWebMessageAsJson(
                "{\"version\":1,\"kind\":\"stop-capture\"}");
        }

        captureActive = false;
        policy.EndCapture();
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
        captureActive = false;
        policy.EndCapture();
        if (initialized && webView.CoreWebView2 is not null)
        {
            Detach(webView.CoreWebView2);
        }

        initialized = false;
    }

    private async Task EnsureInitializedAsync()
    {
        if (initialized)
        {
            return;
        }

        await webView.EnsureCoreWebView2Async().ConfigureAwait(true);
        var core = webView.CoreWebView2 ??
            throw new InvalidOperationException("WebView2 initialization failed.");

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
        core.Navigate(RuntimeHostMediaWebViewPolicy.ApplicationUri.AbsoluteUri);
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
        CoreWebView2NavigationStartingEventArgs args)
    {
        args.Cancel = !policy.IsAllowedResource(args.Uri);
    }

    private static void OnNewWindowRequested(
        object? sender,
        CoreWebView2NewWindowRequestedEventArgs args)
    {
        args.Handled = true;
    }

    private static void OnDownloadStarting(
        object? sender,
        CoreWebView2DownloadStartingEventArgs args)
    {
        args.Cancel = true;
    }

    private void OnPermissionRequested(
        object? sender,
        CoreWebView2PermissionRequestedEventArgs args)
    {
        var kind = args.PermissionKind switch
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
            null,
            403,
            "Forbidden",
            "Content-Type: text/plain\r\nCache-Control: no-store");
    }

    private void OnWebMessageReceived(
        object? sender,
        CoreWebView2WebMessageReceivedEventArgs args)
    {
        if (!policy.IsAllowedResource(args.Source) ||
            !validator.TryValidate(args.WebMessageAsJson, out var message))
        {
            return;
        }

        ValidatedMessage?.Invoke(message!);
    }

    private void EnsureUiThread()
    {
        if (!webView.Dispatcher.CheckAccess())
        {
            throw new InvalidOperationException(
                "The WebView2 media boundary requires its owning UI thread.");
        }
    }

    private static string ToWireKind(RuntimeHostMediaNegotiationKind kind) =>
        kind switch
        {
            RuntimeHostMediaNegotiationKind.Answer => "answer",
            RuntimeHostMediaNegotiationKind.IceCandidate => "ice-candidate",
            RuntimeHostMediaNegotiationKind.IceComplete => "ice-complete",
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind), kind, "A Client negotiation message is required.")
        };
}
