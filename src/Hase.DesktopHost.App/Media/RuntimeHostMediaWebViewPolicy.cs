namespace Hase.DesktopHost.App.Media;

public enum RuntimeHostMediaPermissionKind
{
    Camera,
    Microphone,
    Other
}

/// <summary>
/// Pure policy used by the WebView2 adapter. Browser callbacks are treated as
/// untrusted input and evaluated against the fixed repository-owned origin.
/// </summary>
public sealed class RuntimeHostMediaWebViewPolicy
{
    public const string VirtualHostName = "hase-media.local";
    public static readonly Uri ApplicationUri =
        new($"https://{VirtualHostName}/index.html", UriKind.Absolute);

    private readonly object sync = new();
    private bool captureActive;
    private bool audioAllowed;

    public void BeginCapture(bool includeAudio)
    {
        lock (sync)
        {
            captureActive = true;
            audioAllowed = includeAudio;
        }
    }

    public void EndCapture()
    {
        lock (sync)
        {
            captureActive = false;
            audioAllowed = false;
        }
    }

    public bool IsAllowedResource(string? uriText)
    {
        if (!Uri.TryCreate(uriText, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Scheme == Uri.UriSchemeHttps &&
            string.Equals(
                uri.Host,
                VirtualHostName,
                StringComparison.OrdinalIgnoreCase) &&
            uri.IsDefaultPort &&
            string.IsNullOrEmpty(uri.UserInfo);
    }

    public bool IsPermissionAllowed(
        string? origin,
        RuntimeHostMediaPermissionKind kind)
    {
        if (!IsAllowedOrigin(origin))
        {
            return false;
        }

        lock (sync)
        {
            return captureActive && kind switch
            {
                RuntimeHostMediaPermissionKind.Camera => true,
                RuntimeHostMediaPermissionKind.Microphone => audioAllowed,
                _ => false
            };
        }
    }

    private static bool IsAllowedOrigin(string? origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Scheme == Uri.UriSchemeHttps &&
            string.Equals(
                uri.Host,
                VirtualHostName,
                StringComparison.OrdinalIgnoreCase) &&
            uri.IsDefaultPort &&
            string.IsNullOrEmpty(uri.UserInfo);
    }
}
